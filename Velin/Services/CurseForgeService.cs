using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Velin.Models;

namespace Velin.Services
{
    // Instalar contenido de CurseForge y modpacks (.zip con manifest.json).
    public static class CurseForgeService
    {
        public const int ClassMods = 6, ClassModpacks = 4471, ClassResourcePacks = 12, ClassShaders = 6552;

        public static int LoaderId(ModLoaderType l) => l switch
        {
            ModLoaderType.Forge => 1,
            ModLoaderType.Fabric => 4,
            ModLoaderType.Quilt => 5,
            ModLoaderType.NeoForge => 6,
            _ => 0
        };

        public static string FolderFor(Instance i, int classId) => classId switch
        {
            ClassResourcePacks => Path.Combine(i.GameDir, "resourcepacks"),
            ClassShaders => Path.Combine(i.GameDir, "shaderpacks"),
            _ => i.ModsDir
        };

        // Prefiere versiones estables (release) sobre beta/alpha y, dentro de cada tipo, la más nueva.
        public static CfFile? PickBest(IEnumerable<CfFile> files) =>
            files.OrderBy(f => f.ReleaseType == 1 ? 0 : f.ReleaseType == 2 ? 1 : 2).ThenByDescending(f => f.FileDate).FirstOrDefault();

        private static string? Sha1Of(CfFile f) => f.Hashes.FirstOrDefault(h => h.Algo == 1)?.Value;

        private static async Task<string> ResolveUrlAsync(CurseForgeApi api, CfFile file, string? websiteUrl)
        {
            var url = file.DownloadUrl ?? await api.GetDownloadUrlAsync(file.ModId, file.Id);
            if (url == null)
                throw new CurseForgeException($"El autor de \"{file.DisplayName}\" no permite descargarlo desde otros programas."
                    + (string.IsNullOrEmpty(websiteUrl) ? "" : "\nBajalo a mano desde: " + websiteUrl));
            return url;
        }

        // Mods, resource packs y shaders: baja la versión compatible con la instancia.
        public static async Task<string> InstallAsync(Instance instance, CurseForgeApi api, CfMod mod, Action<string>? status = null)
        {
            int loader = mod.ClassId == ClassMods ? LoaderId(instance.Loader) : 0;
            status?.Invoke($"Buscando versiones de {mod.Name}...");
            var files = await api.GetFilesAsync(mod.Id, instance.McVersion, loader);
            var best = PickBest(files) ?? throw new CurseForgeException($"No hay versiones de {mod.Name} para Minecraft {instance.McVersion}"
                + (loader > 0 ? $" con {instance.Loader}." : "."));

            var url = await ResolveUrlAsync(api, best, mod.Links?.WebsiteUrl);
            var folder = FolderFor(instance, mod.ClassId);
            Directory.CreateDirectory(folder);
            var dest = Path.Combine(folder, ContentService.SafeFileName(best.FileName));
            status?.Invoke($"Descargando {mod.Name}...");
            await FileDownloader.DownloadAsync(url, dest, Sha1Of(best), best.FileLength);
            return dest;
        }

        // Baja el modpack (el .zip más nuevo) y lo instala como instancia nueva.
        public static async Task<Instance> InstallModpackAsync(CurseForgeApi api, CfMod pack, IProgress<string>? progress,
            IProgress<double>? percent, Action<string>? onWarning)
        {
            progress?.Report($"Buscando la última versión de {pack.Name}...");
            var files = await api.GetFilesAsync(pack.Id, null, 0);
            var best = PickBest(files) ?? throw new CurseForgeException("Este modpack no tiene archivos para descargar.");
            var url = await ResolveUrlAsync(api, best, pack.Links?.WebsiteUrl);

            var cache = Path.Combine(PathService.CacheDir, "modpacks");
            Directory.CreateDirectory(cache);
            var zipPath = Path.Combine(cache, ContentService.SafeFileName(best.FileName));
            try
            {
                progress?.Report($"Descargando {pack.Name}...");
                await FileDownloader.DownloadAsync(url, zipPath, Sha1Of(best), best.FileLength);
                return await ImportModpackAsync(zipPath, api, progress, percent, onWarning);
            }
            finally { try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { } }
        }

        // ¿Es un modpack de CurseForge? (.zip con manifest.json de tipo minecraftModpack)
        public static bool IsCurseForgeZip(string path)
        {
            try
            {
                using var zip = ZipFile.OpenRead(path);
                var entry = zip.GetEntry("manifest.json");
                if (entry == null) return false;
                using var s = entry.Open();
                using var doc = JsonDocument.Parse(s);
                return doc.RootElement.TryGetProperty("manifestType", out var t) && t.GetString() == "minecraftModpack";
            }
            catch { return false; }
        }

        // "forge-47.2.0" -> (Forge, "47.2.0"); "neoforge-21.1.99" -> (NeoForge, "21.1.99"); "fabric-0.15.7" -> (Fabric, "0.15.7")
        public static (ModLoaderType loader, string? version) ParseLoaderId(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return (ModLoaderType.Vanilla, null);
            int cut = id.IndexOf('-');
            if (cut <= 0) return (ModLoaderType.Vanilla, null);
            var version = id.Substring(cut + 1);
            return id.Substring(0, cut).ToLowerInvariant() switch
            {
                "forge" => (ModLoaderType.Forge, version),
                "neoforge" => (ModLoaderType.NeoForge, version),
                "fabric" => (ModLoaderType.Fabric, version),
                "quilt" => (ModLoaderType.Quilt, version),
                _ => (ModLoaderType.Vanilla, null)
            };
        }

        public static async Task<Instance> ImportModpackAsync(string zipPath, CurseForgeApi api, IProgress<string>? progress = null,
            IProgress<double>? percent = null, Action<string>? onWarning = null)
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var manifestEntry = zip.GetEntry("manifest.json")
                ?? throw new InvalidDataException("Este .zip no es un modpack de CurseForge: falta manifest.json.");

            string mc, name, overrides = "overrides";
            ModLoaderType loader = ModLoaderType.Vanilla; string? loaderVersion = null;
            var fileRefs = new List<(int project, int file)>();
            using (var s = manifestEntry.Open())
            using (var doc = await JsonDocument.ParseAsync(s))
            {
                var root = doc.RootElement;
                if (!root.TryGetProperty("minecraft", out var m) || !m.TryGetProperty("version", out var v) || string.IsNullOrWhiteSpace(v.GetString()))
                    throw new InvalidDataException("El modpack no indica la versión de Minecraft.");
                mc = v.GetString()!;
                name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                if (root.TryGetProperty("overrides", out var ov) && !string.IsNullOrWhiteSpace(ov.GetString())) overrides = ov.GetString()!;

                if (m.TryGetProperty("modLoaders", out var loaders) && loaders.ValueKind == JsonValueKind.Array && loaders.GetArrayLength() > 0)
                {
                    JsonElement chosen = loaders[0];
                    foreach (var l in loaders.EnumerateArray())
                        if (l.TryGetProperty("primary", out var p) && p.ValueKind == JsonValueKind.True) { chosen = l; break; }
                    (loader, loaderVersion) = ParseLoaderId(chosen.TryGetProperty("id", out var idEl) ? idEl.GetString() : null);
                }

                if (root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
                    foreach (var f in files.EnumerateArray())
                    {
                        // "required: false" son opcionales: se omiten.
                        if (f.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.False) continue;
                        if (f.TryGetProperty("projectID", out var pid) && f.TryGetProperty("fileID", out var fid))
                            fileRefs.Add((pid.GetInt32(), fid.GetInt32()));
                    }
            }

            PathService.EnsureAll();
            var settings = SettingsManager.Load();
            var baseName = InstanceManager.SanitizeName(name.Length > 0 ? name : Path.GetFileNameWithoutExtension(zipPath));
            var instance = new Instance
            {
                Name = InstanceManager.UniqueName(baseName),
                McVersion = mc,
                Loader = loader,
                LoaderVersion = loaderVersion,
                RamMB = settings.DefaultRamMB,
                Username = settings.DefaultUsername
            };
            instance.Save();

            try
            {
                progress?.Report("Consultando los archivos del modpack en CurseForge...");
                var fileInfos = new List<CfFile>();
                foreach (var chunk in fileRefs.Select(r => r.file).Distinct().Chunk(50))
                    fileInfos.AddRange(await api.GetFilesByIdsAsync(chunk.ToList()));

                var modInfos = new Dictionary<int, CfMod>();
                foreach (var chunk in fileInfos.Select(f => f.ModId).Distinct().Chunk(50))
                    foreach (var mod in await api.GetModsByIdsAsync(chunk.ToList())) modInfos[mod.Id] = mod;

                int total = fileInfos.Count, done = 0;
                var failures = new ConcurrentBag<string>();
                using var gate = new SemaphoreSlim(4);
                var tasks = fileInfos.Select(async f =>
                {
                    await gate.WaitAsync();
                    try
                    {
                        modInfos.TryGetValue(f.ModId, out var info);
                        try
                        {
                            var url = await ResolveUrlAsync(api, f, info?.Links?.WebsiteUrl);
                            var folder = FolderFor(instance, info?.ClassId ?? ClassMods);
                            Directory.CreateDirectory(folder);
                            await FileDownloader.DownloadAsync(url, Path.Combine(folder, ContentService.SafeFileName(f.FileName)),
                                Sha1Of(f), f.FileLength);
                        }
                        catch (Exception ex) { failures.Add($"{info?.Name ?? f.DisplayName}: {ex.Message}"); }
                    }
                    finally
                    {
                        gate.Release();
                        var d = Interlocked.Increment(ref done);
                        progress?.Report($"Descargando mods... {d}/{total}");
                        percent?.Report(total == 0 ? 1 : 0.9 * d / total);
                    }
                }).ToList();
                await Task.WhenAll(tasks);

                progress?.Report("Copiando configuración y archivos incluidos en el modpack...");
                ModpackImporter.ExtractOverrides(zip, overrides.TrimEnd('/') + "/", instance.GameDir);
                percent?.Report(1.0);

                if (!failures.IsEmpty)
                    onWarning?.Invoke("Algunos archivos no se pudieron bajar (hay que descargarlos a mano):\n\n" + string.Join("\n", failures.Take(15))
                        + (failures.Count > 15 ? $"\n...y {failures.Count - 15} más." : ""));
            }
            catch
            {
                try { Directory.Delete(instance.InstanceDir, true); } catch { }
                throw;
            }
            return instance;
        }
    }
}
