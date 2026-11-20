using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Velin.Models;

namespace Velin.Services
{
    // Actualiza un modpack de Modrinth o de CurseForge YA instalado a otra versión, sobre la misma instancia.
    //  - Mundos, capturas, opciones y todo lo que agregó el usuario se conservan.
    //  - Los mods/resource packs/shaders que el pack viejo traía y el nuevo ya no, NO se borran: se mueven a
    //    modpack-update-trash\<fecha>\ dentro de la instancia (se pueden recuperar).
    //  - Los archivos de configuración que trae el pack (overrides) sólo se agregan si todavía no existen: no se pisan tus cambios.
    public static class ModpackUpdater
    {
        private static readonly string[] ManagedFolders = { "mods", "resourcepacks", "shaderpacks" };

        public static bool CanUpdate(Instance instance) =>
            (instance.ModpackSource == "modrinth" || instance.ModpackSource == "curseforge")
            && instance.ModpackProjectId.Length > 0 && ModpackImporter.ReadFileList(instance) != null;

        // Qué carpetas de la lista vieja son "del pack" y se pueden retirar con seguridad.
        public static bool IsManagedPath(string relativePath)
        {
            var p = relativePath.Replace('\\', '/').TrimStart('/');
            return ManagedFolders.Any(f => p.StartsWith(f + "/", StringComparison.OrdinalIgnoreCase));
        }

        // Archivos que estaban en el pack viejo y ya no están en el nuevo (sólo carpetas administradas).
        public static List<string> FindRemoved(IEnumerable<string> oldFiles, IEnumerable<string> newFiles)
        {
            var keep = new HashSet<string>(newFiles.Select(Norm), StringComparer.OrdinalIgnoreCase);
            return oldFiles.Where(f => IsManagedPath(f) && !keep.Contains(Norm(f))).ToList();
        }

        private static string Norm(string p) => p.Replace('\\', '/').TrimStart('/');

        public static async Task ApplyMrpackAsync(Instance instance, string mrpackPath, ContentVersion version,
            IProgress<string>? progress, IProgress<double>? percent, Action<string>? onWarning)
        {
            var oldFiles = ModpackImporter.ReadFileList(instance)
                ?? throw new NotSupportedException("Esta instancia se instaló antes de que Velin recordara los archivos del modpack, así que no se puede actualizar de forma segura.");

            using var zip = ZipFile.OpenRead(mrpackPath);
            var index = await ModpackImporter.ReadIndexAsync(zip);
            if (!string.Equals(index.Game, "minecraft", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Este modpack no es de Minecraft: Java Edition.");
            var (mc, loader, loaderVersion) = ModpackImporter.ReadDependencies(index);

            var newFiles = ModpackImporter.ListPackFiles(zip, index);
            var gameRoot = Path.GetFullPath(instance.GameDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            // 1) Lo que ya no está en el pack nuevo se aparta (no se borra).
            progress?.Report("Apartando los archivos que ya no están en el pack...");
            TrashRemoved(instance, oldFiles, newFiles, gameRoot);
            percent?.Report(0.1);

            // 2) Archivos del índice: sólo se baja lo que falta o cambió (por SHA1).
            var files = (index.Files ?? new List<ModpackImporter.MrpackFile>())
                .Where(f => !string.Equals(f.Env?.Client, "unsupported", StringComparison.OrdinalIgnoreCase)).ToList();
            var failures = new ConcurrentBag<string>();
            int total = files.Count, done = 0;
            using (var gate = new SemaphoreSlim(6))
            {
                var tasks = files.Select(async f =>
                {
                    await gate.WaitAsync();
                    try
                    {
                        if (string.IsNullOrWhiteSpace(f.Path)) return;
                        var dest = SafeCombine(gameRoot, f.Path!);
                        if (dest == null) { failures.Add(f.Path!); return; }

                        var sha1 = f.Hashes?.Sha1;
                        if (File.Exists(dest) && !string.IsNullOrEmpty(sha1)
                            && string.Equals(await FileDownloader.Sha1Async(dest), sha1, StringComparison.OrdinalIgnoreCase)) return;   // ya está igual

                        var url = (f.Downloads ?? new List<string>()).FirstOrDefault(ModpackImporter.IsAllowedPackUrl);
                        if (url == null) { failures.Add(f.Path!); return; }
                        try { await FileDownloader.DownloadAsync(url, dest, sha1, f.FileSize); }
                        catch { failures.Add(f.Path!); }
                    }
                    finally
                    {
                        gate.Release();
                        var d = Interlocked.Increment(ref done);
                        progress?.Report($"Actualizando archivos del modpack ({d}/{total})...");
                        percent?.Report(0.1 + 0.8 * (total == 0 ? 1.0 : (double)d / total));
                    }
                }).ToList();
                await Task.WhenAll(tasks);
            }

            // 3) Overrides: lo nuevo se agrega; lo que ya existe sólo se reemplaza dentro de mods\ (la configuración del usuario no se pisa).
            progress?.Report("Aplicando archivos incluidos en el modpack...");
            ApplyOverrides(zip, new[] { "overrides/", "client-overrides/" }, gameRoot);

            // 4) Versión de Minecraft / loader y datos del pack.
            InstanceManager.ChangeVersion(instance, mc, loader, loaderVersion);
            instance.ModpackVersionId = version.Id;
            instance.ModpackVersionName = version.Name;
            instance.Save();
            ModpackImporter.WriteFileList(instance, newFiles);
            percent?.Report(1.0);

            if (!failures.IsEmpty)
            {
                var list = failures.Distinct().OrderBy(x => x).ToList();
                onWarning?.Invoke($"No se pudieron descargar {list.Count} archivo(s) del modpack: " +
                                  string.Join(", ", list.Take(5)) + (list.Count > 5 ? $" y {list.Count - 5} más" : "") + ".");
            }
        }

        // Aparta (no borra) lo que el pack viejo traía y el nuevo ya no: va a modpack-update-trash\<fecha>\ dentro de la instancia.
        private static void TrashRemoved(Instance instance, IEnumerable<string> oldFiles, IEnumerable<string> newFiles, string gameRoot)
        {
            var trash = Path.Combine(instance.InstanceDir, "modpack-update-trash", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            foreach (var rel in FindRemoved(oldFiles, newFiles))
            {
                var src = SafeCombine(gameRoot, rel);
                if (src == null || !File.Exists(src)) continue;
                var dst = Path.Combine(trash, rel.Replace('/', Path.DirectorySeparatorChar));
                try { Directory.CreateDirectory(Path.GetDirectoryName(dst)!); File.Move(src, dst, true); }
                catch { /* si está en uso se deja donde está */ }
            }
        }

        // Overrides del pack: lo nuevo se agrega; lo que ya existe sólo se reemplaza dentro de mods\ (la configuración del usuario no se pisa).
        private static void ApplyOverrides(ZipArchive zip, IEnumerable<string> prefixes, string gameRoot)
        {
            foreach (var prefix in prefixes)
            {
                foreach (var entry in zip.Entries)
                {
                    var full = entry.FullName.Replace('\\', '/');
                    if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    var rel = full.Substring(prefix.Length);
                    if (rel.Length == 0 || rel.EndsWith('/')) continue;
                    var target = SafeCombine(gameRoot, rel);
                    if (target == null) continue;
                    if (File.Exists(target) && !IsManagedPath(rel)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    entry.ExtractToFile(target, true);
                }
            }
        }

        // Modpack de CurseForge: mismo criterio que con Modrinth, con los archivos que lista su manifest.json.
        public static async Task ApplyCurseForgeAsync(Instance instance, string zipPath, ContentVersion version, CurseForgeApi api,
            IProgress<string>? progress, IProgress<double>? percent, Action<string>? onWarning)
        {
            var oldFiles = ModpackImporter.ReadFileList(instance)
                ?? throw new NotSupportedException("Esta instancia se instaló antes de que Velin recordara los archivos del modpack, así que no se puede actualizar de forma segura.");

            using var zip = ZipFile.OpenRead(zipPath);
            var manifest = await CurseForgeService.ReadManifestAsync(zip);

            progress?.Report("Consultando los archivos del modpack en CurseForge...");
            var fileInfos = await CurseForgeService.GetFileInfosAsync(api, manifest.FileRefs);
            var modInfos = await CurseForgeService.GetModInfosAsync(api, fileInfos);
            var newFiles = CurseForgeService.PackFilePaths(fileInfos, modInfos, zip, manifest.Overrides);
            var gameRoot = Path.GetFullPath(instance.GameDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            // 1) Lo que ya no está en el pack nuevo se aparta (no se borra).
            progress?.Report("Apartando los archivos que ya no están en el pack...");
            TrashRemoved(instance, oldFiles, newFiles, gameRoot);
            percent?.Report(0.1);

            // 2) Sólo se baja lo que falta o cambió (por SHA1, o por tamaño si CurseForge no informa el hash).
            var failures = new ConcurrentBag<string>();
            int total = fileInfos.Count, done = 0;
            using (var gate = new SemaphoreSlim(4))
            {
                var tasks = fileInfos.Select(async f =>
                {
                    await gate.WaitAsync();
                    try
                    {
                        modInfos.TryGetValue(f.ModId, out var info);
                        var rel = CurseForgeService.RelativePathFor(f, info?.ClassId ?? CurseForgeService.ClassMods);
                        var dest = SafeCombine(gameRoot, rel);
                        if (dest == null) { failures.Add(rel); return; }

                        var sha1 = CurseForgeService.Sha1Of(f);
                        if (File.Exists(dest))
                        {
                            bool same = !string.IsNullOrEmpty(sha1)
                                ? string.Equals(await FileDownloader.Sha1Async(dest), sha1, StringComparison.OrdinalIgnoreCase)
                                : new FileInfo(dest).Length == f.FileLength;
                            if (same) return;   // ya está igual
                        }

                        try
                        {
                            var url = await CurseForgeService.ResolveUrlAsync(api, f, info?.Links?.WebsiteUrl);
                            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                            await FileDownloader.DownloadAsync(url, dest, sha1, f.FileLength);
                        }
                        catch (Exception ex) { failures.Add($"{info?.Name ?? f.DisplayName}: {ex.Message}"); }
                    }
                    finally
                    {
                        gate.Release();
                        var d = Interlocked.Increment(ref done);
                        progress?.Report($"Actualizando archivos del modpack ({d}/{total})...");
                        percent?.Report(0.1 + 0.8 * (total == 0 ? 1.0 : (double)d / total));
                    }
                }).ToList();
                await Task.WhenAll(tasks);
            }

            // 3) Overrides.
            progress?.Report("Aplicando archivos incluidos en el modpack...");
            ApplyOverrides(zip, new[] { manifest.Overrides.TrimEnd('/') + "/" }, gameRoot);

            // 4) Versión de Minecraft / loader y datos del pack.
            InstanceManager.ChangeVersion(instance, manifest.Mc, manifest.Loader, manifest.LoaderVersion);
            instance.ModpackVersionId = version.Id;
            instance.ModpackVersionName = version.Name;
            instance.Save();
            ModpackImporter.WriteFileList(instance, newFiles);
            percent?.Report(1.0);

            if (!failures.IsEmpty)
            {
                var list = failures.Distinct().OrderBy(x => x).ToList();
                onWarning?.Invoke("No se pudieron descargar " + list.Count + " archivo(s) del modpack (hay que bajarlos a mano):\n" +
                                  string.Join("\n", list.Take(8)) + (list.Count > 8 ? $"\n...y {list.Count - 8} más." : ""));
            }
        }

        // Combina una ruta relativa con la carpeta del juego sin permitir salirse de ella.
        public static string? SafeCombine(string gameRootWithSeparator, string relativePath)
        {
            var full = Path.GetFullPath(Path.Combine(gameRootWithSeparator, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            return full.StartsWith(gameRootWithSeparator, StringComparison.OrdinalIgnoreCase) ? full : null;
        }
    }
}
