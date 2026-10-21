using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Velin.Models;

namespace Velin.Services
{
    public static class InstanceManager
    {
        public static List<Instance> LoadAll()
        {
            PathService.EnsureAll();
            var result = new List<Instance>();
            foreach (var dir in Directory.GetDirectories(PathService.InstancesDir))
            {
                var inst = Instance.Load(dir) ?? TryAdoptCustomJar(dir);
                if (inst != null) result.Add(inst);
            }
            return result.OrderBy(i => i.Name).ToList();
        }

        // Una carpeta de data\instances SIN instance.json pero con un .jar adentro se toma como una instancia con
        // cliente propio. Si el .jar no dice su version (los anteriores a 1.14), la version queda vacia y Velin la pregunta al jugar.
        internal static Instance? TryAdoptCustomJar(string dir)
        {
            try
            {
                var name = Path.GetFileName(dir);
                if (!IsValidName(name)) return null;
                var jar = CustomJar.FindJarIn(dir);
                if (jar == null) return null;

                var settings = SettingsManager.Load();
                var inst = new Instance
                {
                    Name = name,
                    CustomJar = Path.GetFileName(jar),
                    McVersion = CustomJar.TryReadVersion(jar) ?? "",
                    RamMB = settings.DefaultRamMB,
                    Username = settings.DefaultUsername
                };
                inst.Save();
                return inst;
            }
            catch { return null; }
        }

        public static bool Exists(string name) => Directory.Exists(Path.Combine(PathService.InstancesDir, name));

        public static void Delete(Instance instance)
        {
            if (Directory.Exists(instance.InstanceDir))
                Directory.Delete(instance.InstanceDir, true);
        }

        // --- Nombres -------------------------------------------------------------------

        public static bool IsValidName(string? name) =>
            !string.IsNullOrWhiteSpace(name)
            && name.Trim() == name
            && !name.EndsWith('.')
            && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

        // Si "Mi mundo" ya existe devuelve "Mi mundo (2)", "Mi mundo (3)", etc.
        public static string UniqueName(string baseName)
        {
            var name = baseName;
            int n = 2;
            while (Exists(name)) name = $"{baseName} ({n++})";
            return name;
        }

        public static string SanitizeName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            name = name.Trim().TrimEnd('.');
            if (name.Length > 60) name = name.Substring(0, 60).TrimEnd();
            return name.Length == 0 ? "Instancia" : name;
        }

        // --- Duplicar / renombrar --------------------------------------------------------

        public static async Task<Instance> DuplicateAsync(Instance source, string newName)
        {
            if (!IsValidName(newName)) throw new ArgumentException("El nombre no es válido.");
            if (Exists(newName)) throw new IOException("Ya existe una instancia con ese nombre.");

            var dest = Path.Combine(PathService.InstancesDir, newName);
            try
            {
                await Task.Run(() => CopyDirectory(source.InstanceDir, dest,
                    name => name.Equals("natives", StringComparison.OrdinalIgnoreCase)
                         || name.Equals("velin-launch.log", StringComparison.OrdinalIgnoreCase)));

                var copy = Instance.Load(dest) ?? throw new InvalidDataException("No se pudo leer la instancia copiada.");
                copy.Name = newName;
                copy.LastPlayedUtc = null;
                copy.TotalPlaySeconds = 0;
                copy.Save();
                return copy;
            }
            catch
            {
                try { if (Directory.Exists(dest)) Directory.Delete(dest, true); } catch { }
                throw;
            }
        }

        public static Instance Rename(Instance instance, string newName)
        {
            if (!IsValidName(newName)) throw new ArgumentException("El nombre no es válido.");

            var oldName = instance.Name;
            var oldDir = instance.InstanceDir;
            var newDir = Path.Combine(PathService.InstancesDir, newName);

            bool onlyCaseChange = string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase);
            if (!onlyCaseChange && Exists(newName)) throw new IOException("Ya existe una instancia con ese nombre.");

            if (onlyCaseChange)
            {
                // En Windows, renombrar sólo cambiando mayúsculas/minúsculas pide pasar por un nombre temporal.
                var temp = oldDir + "." + Guid.NewGuid().ToString("N").Substring(0, 6) + ".tmp";
                Directory.Move(oldDir, temp);
                Directory.Move(temp, newDir);
            }
            else
            {
                Directory.Move(oldDir, newDir);
            }

            instance.Name = newName;
            instance.Save();

            // Que los backups de mundos acompañen a la instancia.
            try
            {
                var oldBackups = Path.Combine(PathService.BackupsDir, SanitizeName(oldName));
                var newBackups = Path.Combine(PathService.BackupsDir, SanitizeName(newName));
                if (!onlyCaseChange && Directory.Exists(oldBackups) && !Directory.Exists(newBackups))
                    Directory.Move(oldBackups, newBackups);
            }
            catch { }

            return instance;
        }

        // --- Cambiar versión / loader ------------------------------------------------------

        // Cambia la versión de Minecraft y/o el mod loader de una instancia existente, sin tocar
        // mundos, mods, configuración ni nada de la carpeta "minecraft". Lo que cambia son los archivos
        // de versión que se regeneran solos al jugar (cliente, perfil del loader, librerías, nativos).
        public static void ChangeVersion(Instance instance, string mcVersion, ModLoaderType loader, string? loaderVersion)
        {
            bool mcChanged = !string.Equals(instance.McVersion, mcVersion, StringComparison.OrdinalIgnoreCase);
            bool loaderChanged = instance.Loader != loader
                || !string.Equals(instance.LoaderVersion ?? "", loaderVersion ?? "", StringComparison.OrdinalIgnoreCase);
            if (!mcChanged && !loaderChanged) return;

            // Nativos: se vuelven a extraer en el próximo lanzamiento.
            TryClearDirectory(instance.NativesDir);

            // Archivos de versión: si cambió Minecraft se borra todo (incluido el .jar del cliente viejo);
            // si sólo cambió el loader, se conserva el .jar y se borran los perfiles del loader anterior.
            if (Directory.Exists(instance.VersionsDir))
            {
                foreach (var dir in Directory.GetDirectories(instance.VersionsDir))
                    try { Directory.Delete(dir, true); } catch { }
                if (mcChanged)
                    foreach (var file in Directory.GetFiles(instance.VersionsDir))
                        try { File.Delete(file); } catch { }
            }

            // Librerías propias del loader (Forge/NeoForge) y marcas de "ya instalado".
            var libs = Path.Combine(instance.InstanceDir, "libraries");
            try { if (Directory.Exists(libs)) Directory.Delete(libs, true); } catch { }
            try
            {
                foreach (var marker in Directory.GetFiles(instance.InstanceDir, ".velin-loader-*"))
                    File.Delete(marker);
            }
            catch { }

            instance.McVersion = mcVersion;
            instance.Loader = loader;
            instance.LoaderVersion = loader == ModLoaderType.Vanilla ? null : loaderVersion;
            instance.Save();
        }

        private static void TryClearDirectory(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (var f in Directory.GetFiles(dir)) { try { File.Delete(f); } catch { } }
                foreach (var d in Directory.GetDirectories(dir)) { try { Directory.Delete(d, true); } catch { } }
            }
            catch { }
        }

        // --- Exportar / importar -----------------------------------------------------------

        // Exporta la instancia entera a un .zip (sin los nativos ni el log, que se regeneran solos).
        public static Task ExportAsync(Instance instance, string zipPath)
        {
            return Task.Run(() =>
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
                using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
                var root = instance.InstanceDir;

                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(root, file);
                    var first = rel.Split(Path.DirectorySeparatorChar)[0];
                    if (first.Equals("natives", StringComparison.OrdinalIgnoreCase)) continue;
                    if (rel.Equals("velin-launch.log", StringComparison.OrdinalIgnoreCase)) continue;

                    zip.CreateEntryFromFile(file, rel.Replace('\\', '/'), CompressionLevel.Fastest);
                }
            });
        }

        // Importa un .zip exportado por Velin. Si el nombre ya existe, agrega un número.
        public static Task<Instance> ImportExportedAsync(string zipPath)
        {
            return Task.Run(() =>
            {
                using var zip = ZipFile.OpenRead(zipPath);
                var metaEntry = zip.GetEntry("instance.json")
                    ?? throw new InvalidDataException("Este .zip no es una instancia exportada por Velin (falta instance.json).");

                Instance? meta;
                using (var s = metaEntry.Open()) meta = JsonSerializer.Deserialize<Instance>(s);
                if (meta == null || string.IsNullOrWhiteSpace(meta.Name))
                    throw new InvalidDataException("El instance.json del .zip está vacío o dañado.");

                PathService.EnsureAll();
                var name = UniqueName(SanitizeName(meta.Name));
                var dest = Path.Combine(PathService.InstancesDir, name);
                var destRoot = Path.GetFullPath(dest).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                try
                {
                    Directory.CreateDirectory(dest);
                    foreach (var entry in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue; // carpetas

                        // Defensa: ninguna entrada puede escribir fuera de la carpeta de la instancia.
                        var target = Path.GetFullPath(Path.Combine(dest, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                        if (!target.StartsWith(destRoot, StringComparison.OrdinalIgnoreCase)) continue;

                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        entry.ExtractToFile(target, true);
                    }

                    var inst = Instance.Load(dest) ?? throw new InvalidDataException("No se pudo leer la instancia importada.");
                    inst.Name = name;
                    inst.Save();
                    return inst;
                }
                catch
                {
                    try { if (Directory.Exists(dest)) Directory.Delete(dest, true); } catch { }
                    throw;
                }
            });
        }

        private static void CopyDirectory(string src, string dest, Func<string, bool>? skipTopLevel = null, bool isTop = true)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(src))
            {
                var name = Path.GetFileName(file);
                if (isTop && skipTopLevel != null && skipTopLevel(name)) continue;
                File.Copy(file, Path.Combine(dest, name), true);
            }
            foreach (var dir in Directory.GetDirectories(src))
            {
                var name = Path.GetFileName(dir);
                if (isTop && skipTopLevel != null && skipTopLevel(name)) continue;
                CopyDirectory(dir, Path.Combine(dest, name), skipTopLevel, false);
            }
        }
    }
}
