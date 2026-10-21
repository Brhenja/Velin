using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Velin.Models;

namespace Velin.Services
{
    // Copias de seguridad de los mundos (carpeta "saves") de una instancia. Se guardan en
    // data\backups\<instancia>\ — fuera de la carpeta de la instancia, así sobreviven si se la elimina.
    public static class BackupService
    {
        public const int KeepPerInstance = 5; // se conservan los 5 más recientes por instancia

        public static string BackupDirFor(Instance instance) =>
            Path.Combine(PathService.BackupsDir, SafeName(instance.Name));

        public static string SavesDir(Instance instance) => Path.Combine(instance.GameDir, "saves");

        public static bool HasWorlds(Instance instance)
        {
            var dir = SavesDir(instance);
            return Directory.Exists(dir) && Directory.EnumerateFileSystemEntries(dir).Any();
        }

        // Devuelve la ruta del .zip creado, o null si la instancia no tenía mundos.
        // Lanza excepción si no se pudo copiar (por ejemplo, si el juego tiene archivos abiertos).
        public static async Task<string?> BackupWorldsAsync(Instance instance, string reason)
        {
            if (!HasWorlds(instance)) return null;

            var dir = BackupDirFor(instance);
            Directory.CreateDirectory(dir);

            var cleanReason = new string(reason.Where(char.IsLetterOrDigit).ToArray());
            // Milisegundos en el nombre (y un contador si aun así existiera): dos backups seguidos
            // no pueden pisarse ni fallar por "el archivo ya existe".
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            var file = Path.Combine(dir, $"saves-{stamp}-{cleanReason}.zip");
            for (int n = 2; File.Exists(file); n++)
                file = Path.Combine(dir, $"saves-{stamp}-{cleanReason}-{n}.zip");
            var saves = SavesDir(instance);

            await Task.Run(() =>
            {
                try
                {
                    // Compresión rápida: es lo más liviano para una PC justa.
                    ZipFile.CreateFromDirectory(saves, file, CompressionLevel.Fastest, true);
                }
                catch
                {
                    try { File.Delete(file); } catch { }
                    throw;
                }
                Prune(dir);
            });
            return file;
        }

        public class BackupInfo
        {
            public string Path { get; set; } = "";
            public DateTime Created { get; set; }
            public long SizeBytes { get; set; }
            public string Reason { get; set; } = "";
        }

        // Backups existentes de la instancia, del más nuevo al más viejo.
        public static List<BackupInfo> ListBackups(Instance instance)
        {
            var dir = BackupDirFor(instance);
            var result = new List<BackupInfo>();
            if (!Directory.Exists(dir)) return result;
            foreach (var f in new DirectoryInfo(dir).GetFiles("saves-*.zip"))
            {
                // Nombre: saves-yyyyMMdd-HHmmss-fff-motivo[-n].zip
                var core = System.IO.Path.GetFileNameWithoutExtension(f.Name).Substring("saves-".Length);
                DateTime created = f.LastWriteTime;
                string reason = "";
                if (core.Length >= 19 && DateTime.TryParseExact(core.Substring(0, 19), "yyyyMMdd-HHmmss-fff",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    created = parsed;
                    reason = core.Length > 20 ? core.Substring(20) : "";
                }
                result.Add(new BackupInfo { Path = f.FullName, Created = created, SizeBytes = f.Length, Reason = reason });
            }
            return result.OrderByDescending(b => b.Created).ThenByDescending(b => b.Path, StringComparer.Ordinal).ToList();
        }

        // Restaura los mundos desde un backup. Antes de tocar nada guarda una copia de lo actual
        // (motivo "prerestore"), y extrae a una carpeta temporal: si algo falla, los mundos actuales quedan intactos.
        public static async Task RestoreAsync(Instance instance, string zipPath)
        {
            if (!File.Exists(zipPath)) throw new FileNotFoundException("No se encontró el backup.", zipPath);

            if (HasWorlds(instance))
                await BackupWorldsAsync(instance, "prerestore");

            await Task.Run(() =>
            {
                var saves = SavesDir(instance);
                var stamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                var temp = saves + ".restoring-" + stamp;
                var old = saves + ".old-" + stamp;
                try
                {
                    Directory.CreateDirectory(temp);
                    var root = System.IO.Path.GetFullPath(temp) + System.IO.Path.DirectorySeparatorChar;
                    using (var zip = ZipFile.OpenRead(zipPath))
                    {
                        foreach (var entry in zip.Entries)
                        {
                            var dest = System.IO.Path.GetFullPath(System.IO.Path.Combine(temp, entry.FullName));
                            // Protección contra rutas maliciosas dentro del .zip (zip slip).
                            if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidDataException("El backup contiene una ruta no válida: " + entry.FullName);
                            if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(dest); continue; }
                            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest)!);
                            entry.ExtractToFile(dest, true);
                        }
                    }

                    // Los backups se guardan CON la carpeta "saves" adentro del .zip: si al extraer hay una unica
                    // carpeta "saves", el contenido real es esa (si no, quedaria saves\saves\<mundo>).
                    var source = temp;
                    var inner = Path.Combine(temp, Path.GetFileName(saves));
                    if (Directory.Exists(inner) && Directory.GetFileSystemEntries(temp).Length == 1) source = inner;

                    if (Directory.Exists(saves)) Directory.Move(saves, old);
                    Directory.Move(source, saves);
                    try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
                    try { if (Directory.Exists(old)) Directory.Delete(old, true); } catch { }
                }
                catch
                {
                    try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
                    // Si ya habíamos apartado los mundos actuales y falló el cambio, los devolvemos.
                    try { if (!Directory.Exists(saves) && Directory.Exists(old)) Directory.Move(old, saves); } catch { }
                    throw;
                }
            });
        }

        public static void Delete(string zipPath)
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }

        private static void Prune(string dir)
        {
            // El nombre empieza con la fecha y hora, así que ordenarlo por nombre es ordenarlo por antigüedad.
            var old = new DirectoryInfo(dir).GetFiles("saves-*.zip")
                .OrderByDescending(f => f.Name, StringComparer.Ordinal)
                .Skip(KeepPerInstance);
            foreach (var f in old)
            {
                try { f.Delete(); } catch { }
            }
        }

        private static string SafeName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }
    }
}
