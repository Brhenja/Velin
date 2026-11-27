using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Velin.Models;

namespace Velin.Services
{
    public class WorldInfo
    {
        public string Name { get; set; } = "";          // nombre de la carpeta
        public string Path { get; set; } = "";
        public DateTime LastModified { get; set; }
        public long SizeBytes { get; set; } = -1;       // -1 = todavía no calculado
    }

    // Administra los mundos (saves) de una instancia. Siempre sobre la carpeta del mundo, sin tocar level.dat.
    public static class WorldService
    {
        public static string SavesDir(Instance instance) => System.IO.Path.Combine(instance.GameDir, "saves");

        // Una carpeta cuenta como mundo si tiene level.dat.
        public static List<WorldInfo> ListWorlds(Instance instance)
        {
            var result = new List<WorldInfo>();
            var saves = SavesDir(instance);
            if (!Directory.Exists(saves)) return result;
            foreach (var dir in Directory.GetDirectories(saves))
            {
                var level = System.IO.Path.Combine(dir, "level.dat");
                if (!File.Exists(level)) continue;
                result.Add(new WorldInfo
                {
                    Name = System.IO.Path.GetFileName(dir),
                    Path = dir,
                    LastModified = File.GetLastWriteTime(level)
                });
            }
            return result.OrderByDescending(w => w.LastModified).ToList();
        }

        public static long DirectorySize(string dir)
        {
            long total = 0;
            try
            {
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    try { total += new FileInfo(f).Length; } catch { }
            }
            catch { }
            return total;
        }

        // Copia de seguridad de UN mundo, en data\backups\<instancia>\world-<mundo>-<fecha>.zip
        public static async Task<string> BackupWorldAsync(Instance instance, WorldInfo world)
        {
            var dir = BackupService.BackupDirFor(instance);
            Directory.CreateDirectory(dir);
            var safe = ContentService.SafeFileName(world.Name);
            var file = System.IO.Path.Combine(dir, $"world-{safe}-{DateTime.Now:yyyyMMdd-HHmmss-fff}.zip");
            await Task.Run(() => ZipFile.CreateFromDirectory(world.Path, file, CompressionLevel.Fastest, true));
            return file;
        }

        public static async Task<string> DuplicateAsync(Instance instance, WorldInfo world)
        {
            var saves = SavesDir(instance);
            var name = world.Name + " (copia)";
            int n = 2;
            while (Directory.Exists(System.IO.Path.Combine(saves, name))) name = $"{world.Name} (copia {n++})";
            var dest = System.IO.Path.Combine(saves, name);
            await Task.Run(() => CopyDirectory(world.Path, dest));
            return name;
        }

        public static void Delete(WorldInfo world)
        {
            if (Directory.Exists(world.Path)) Directory.Delete(world.Path, true);
        }

        public static Task ExportAsync(WorldInfo world, string zipPath) => Task.Run(() =>
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            ZipFile.CreateFromDirectory(world.Path, zipPath, CompressionLevel.Fastest, true);
        });

        // Importa un mundo desde un .zip (con la carpeta del mundo adentro, o con level.dat suelto en la raíz).
        // Devuelve el nombre con el que quedó en saves.
        public static async Task<string> ImportAsync(Instance instance, string zipPath)
        {
            return await Task.Run(() =>
            {
                var saves = SavesDir(instance);
                Directory.CreateDirectory(saves);
                var temp = System.IO.Path.Combine(saves, ".importando-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                try
                {
                    Directory.CreateDirectory(temp);
                    var root = System.IO.Path.GetFullPath(temp) + System.IO.Path.DirectorySeparatorChar;
                    using (var zip = ZipFile.OpenRead(zipPath))
                    {
                        foreach (var entry in zip.Entries)
                        {
                            var dest = System.IO.Path.GetFullPath(System.IO.Path.Combine(temp, entry.FullName));
                            if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                                throw new InvalidDataException("El .zip contiene una ruta no válida: " + entry.FullName);
                            if (string.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(dest); continue; }
                            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest)!);
                            entry.ExtractToFile(dest, true);
                        }
                    }

                    // ¿Dónde quedó level.dat? En la raíz, o dentro de una única carpeta.
                    string worldRoot;
                    string baseName;
                    if (File.Exists(System.IO.Path.Combine(temp, "level.dat")))
                    {
                        worldRoot = temp;
                        baseName = System.IO.Path.GetFileNameWithoutExtension(zipPath);
                    }
                    else
                    {
                        var sub = Directory.GetDirectories(temp).FirstOrDefault(d => File.Exists(System.IO.Path.Combine(d, "level.dat")))
                            ?? throw new InvalidDataException("El .zip no parece un mundo de Minecraft (no tiene level.dat).");
                        worldRoot = sub;
                        baseName = System.IO.Path.GetFileName(sub);
                    }

                    baseName = ContentService.SafeFileName(baseName).Trim().TrimEnd('.');
                    if (baseName.Length == 0) baseName = "Mundo importado";
                    var name = baseName;
                    int n = 2;
                    while (Directory.Exists(System.IO.Path.Combine(saves, name))) name = $"{baseName} ({n++})";

                    var finalPath = System.IO.Path.Combine(saves, name);
                    if (worldRoot == temp) Directory.Move(temp, finalPath);
                    else Directory.Move(worldRoot, finalPath);
                    return name;
                }
                finally
                {
                    try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
                }
            });
        }

        private static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, System.IO.Path.Combine(dest, System.IO.Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(source))
                CopyDirectory(dir, System.IO.Path.Combine(dest, System.IO.Path.GetFileName(dir)));
        }
    }
}
