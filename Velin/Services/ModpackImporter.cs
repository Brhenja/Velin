using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Velin.Models;

namespace Velin.Services
{
    // Importa modpacks de Modrinth (.mrpack): crea una instancia con la versión de Minecraft y el
    // mod loader que pide el pack, baja todos sus archivos (verificando el hash) y copia los "overrides".
    public static class ModpackImporter
    {
        // La especificación de .mrpack sólo permite descargar desde estos sitios.
        private static readonly string[] AllowedHosts =
            { "cdn.modrinth.com", "github.com", "raw.githubusercontent.com", "gitlab.com" };

        public class MrpackIndex
        {
            [JsonPropertyName("game")] public string? Game { get; set; }
            [JsonPropertyName("name")] public string? Name { get; set; }
            [JsonPropertyName("files")] public List<MrpackFile>? Files { get; set; }
            [JsonPropertyName("dependencies")] public Dictionary<string, string>? Dependencies { get; set; }
        }

        public class MrpackFile
        {
            [JsonPropertyName("path")] public string? Path { get; set; }
            [JsonPropertyName("hashes")] public MrpackHashes? Hashes { get; set; }
            [JsonPropertyName("env")] public MrpackEnv? Env { get; set; }
            [JsonPropertyName("downloads")] public List<string>? Downloads { get; set; }
            [JsonPropertyName("fileSize")] public long FileSize { get; set; }
        }

        public class MrpackHashes { [JsonPropertyName("sha1")] public string? Sha1 { get; set; } }
        public class MrpackEnv { [JsonPropertyName("client")] public string? Client { get; set; } }

        public static async Task<Instance> ImportMrpackAsync(string mrpackPath, IProgress<string>? progress = null,
            IProgress<double>? percent = null, Action<string>? onWarning = null)
        {
            using var zip = ZipFile.OpenRead(mrpackPath);
            var indexEntry = zip.GetEntry("modrinth.index.json")
                ?? throw new InvalidDataException("Este archivo no es un modpack de Modrinth (.mrpack): falta modrinth.index.json.");

            MrpackIndex index;
            using (var s = indexEntry.Open())
                index = await JsonSerializer.DeserializeAsync<MrpackIndex>(s)
                    ?? throw new InvalidDataException("El modrinth.index.json está vacío o dañado.");

            if (!string.Equals(index.Game, "minecraft", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Este modpack no es de Minecraft: Java Edition.");

            var deps = index.Dependencies ?? new Dictionary<string, string>();
            if (!deps.TryGetValue("minecraft", out var mc) || string.IsNullOrWhiteSpace(mc))
                throw new InvalidDataException("El modpack no indica la versión de Minecraft.");

            ModLoaderType loader = ModLoaderType.Vanilla;
            string? loaderVersion = null;
            if (deps.TryGetValue("fabric-loader", out var fabric)) { loader = ModLoaderType.Fabric; loaderVersion = fabric; }
            else if (deps.TryGetValue("quilt-loader", out var quilt)) { loader = ModLoaderType.Quilt; loaderVersion = quilt; }
            else if (deps.TryGetValue("neoforge", out var neo)) { loader = ModLoaderType.NeoForge; loaderVersion = neo; }
            else if (deps.TryGetValue("forge", out var forge))
            {
                loader = ModLoaderType.Forge;
                loaderVersion = forge.StartsWith(mc + "-", StringComparison.Ordinal) ? forge.Substring(mc.Length + 1) : forge;
            }

            PathService.EnsureAll();
            var settings = SettingsManager.Load();
            var baseName = InstanceManager.SanitizeName(!string.IsNullOrWhiteSpace(index.Name)
                ? index.Name!
                : System.IO.Path.GetFileNameWithoutExtension(mrpackPath));

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
                await DownloadFilesAsync(index, instance, progress, percent, onWarning);

                progress?.Report("Copiando configuración y archivos incluidos en el modpack...");
                ExtractOverrides(zip, "overrides/", instance.GameDir);
                ExtractOverrides(zip, "client-overrides/", instance.GameDir);
                percent?.Report(1.0);
            }
            catch
            {
                try { Directory.Delete(instance.InstanceDir, true); } catch { }
                throw;
            }

            return instance;
        }

        private static async Task DownloadFilesAsync(MrpackIndex index, Instance instance,
            IProgress<string>? progress, IProgress<double>? percent, Action<string>? onWarning)
        {
            // Los archivos que el pack marca como "no soportados en cliente" no se bajan.
            var files = (index.Files ?? new List<MrpackFile>())
                .Where(f => !string.Equals(f.Env?.Client, "unsupported", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var gameRoot = System.IO.Path.GetFullPath(instance.GameDir).TrimEnd(System.IO.Path.DirectorySeparatorChar)
                           + System.IO.Path.DirectorySeparatorChar;

            int total = files.Count, done = 0;
            var failures = new ConcurrentBag<string>();
            using var gate = new SemaphoreSlim(6);

            var tasks = files.Select(async f =>
            {
                await gate.WaitAsync();
                try
                {
                    if (string.IsNullOrWhiteSpace(f.Path)) { failures.Add("(archivo sin ruta)"); return; }

                    var dest = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                        instance.GameDir, f.Path.Replace('/', System.IO.Path.DirectorySeparatorChar)));

                    // Defensa: el pack no puede escribir fuera de la carpeta del juego.
                    if (!dest.StartsWith(gameRoot, StringComparison.OrdinalIgnoreCase)) { failures.Add(f.Path); return; }

                    var url = (f.Downloads ?? new List<string>()).FirstOrDefault(IsAllowedUrl);
                    if (url == null) { failures.Add(f.Path); return; }

                    try { await FileDownloader.DownloadAsync(url, dest, f.Hashes?.Sha1, f.FileSize); }
                    catch { failures.Add(f.Path); }
                }
                finally
                {
                    gate.Release();
                    var d = Interlocked.Increment(ref done);
                    progress?.Report($"Descargando archivos del modpack ({d}/{total})...");
                    percent?.Report(total == 0 ? 1.0 : (double)d / total);
                }
            }).ToList();

            await Task.WhenAll(tasks);

            if (!failures.IsEmpty)
            {
                var list = failures.Distinct().OrderBy(x => x).ToList();
                onWarning?.Invoke($"No se pudieron descargar {list.Count} archivo(s) del modpack: " +
                                  string.Join(", ", list.Take(5)) + (list.Count > 5 ? $" y {list.Count - 5} más" : "") +
                                  ".\nLa instancia se creó igual, pero puede que le falten mods.");
            }
        }

        private static bool IsAllowedUrl(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);

        internal static void ExtractOverrides(ZipArchive zip, string prefix, string gameDir)
        {
            var root = System.IO.Path.GetFullPath(gameDir).TrimEnd(System.IO.Path.DirectorySeparatorChar)
                       + System.IO.Path.DirectorySeparatorChar;

            foreach (var entry in zip.Entries)
            {
                var full = entry.FullName.Replace('\\', '/');
                if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

                var rel = full.Substring(prefix.Length);
                if (string.IsNullOrEmpty(rel) || rel.EndsWith('/')) continue; // carpetas

                var target = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                    gameDir, rel.Replace('/', System.IO.Path.DirectorySeparatorChar)));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;

                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, true);
            }
        }
    }
}
