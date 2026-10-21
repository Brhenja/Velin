using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Velin.Models;

namespace Velin.Services
{
    // Perfil ya "resuelto": la mezcla final de vanilla + loader (fabric/quilt/forge/neoforge)
    // lista para descargar y lanzar.
    public class ResolvedProfile
    {
        public string MainClass { get; set; } = "";
        public List<MojangLibrary> Libraries { get; set; } = new();
        public MojangArguments? Arguments { get; set; }
        public string? MinecraftArguments { get; set; }
        public MojangAssetIndexRef? AssetIndex { get; set; }
        public string Assets { get; set; } = "legacy";
        public MojangDownloadItem? ClientDownload { get; set; }
        public int JavaMajor { get; set; } = 8;
    }

    public static class MinecraftDownloader
    {
        private static readonly HttpClient _http = new HttpClient();
        private const string ManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";

        static MinecraftDownloader()
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.UserAgent);
            _http.Timeout = TimeSpan.FromMinutes(10);
        }

        public static async Task<VersionManifestRoot> GetManifestAsync()
        {
            var json = await _http.GetStringAsync(ManifestUrl);
            return JsonSerializer.Deserialize<VersionManifestRoot>(json)!;
        }

        public static async Task<MojangVersionJson> GetVersionJsonAsync(string mcVersion)
        {
            var cacheFile = Path.Combine(PathService.CacheDir, $"{mcVersion}.json");
            if (File.Exists(cacheFile))
                return JsonSerializer.Deserialize<MojangVersionJson>(await File.ReadAllTextAsync(cacheFile))!;

            var manifest = await GetManifestAsync();
            var entry = manifest.Versions.FirstOrDefault(v => v.Id == mcVersion)
                ?? throw new Exception($"Versión {mcVersion} no encontrada en el manifest de Mojang");
            var json = await _http.GetStringAsync(entry.Url);
            await File.WriteAllTextAsync(cacheFile, json);
            return JsonSerializer.Deserialize<MojangVersionJson>(json)!;
        }

        public static bool RuleAllowsWindows(List<MojangRule>? rules)
        {
            if (rules == null || rules.Count == 0) return true;
            bool allowed = false;
            foreach (var r in rules)
            {
                bool osMatches = r.Os == null || string.IsNullOrEmpty(r.Os.Name) || r.Os.Name == "windows";
                // Las versiones modernas traen nativos para x86 y ARM64 con una regla de arquitectura;
                // Velin corre en 64 bits, así que sólo cuentan las reglas sin arquitectura o de x64.
                bool archMatches = r.Os == null || string.IsNullOrEmpty(r.Os.Arch)
                    || r.Os.Arch == "x64" || r.Os.Arch == "amd64" || r.Os.Arch == "x86_64";
                bool matches = osMatches && archMatches;
                if (matches) allowed = r.Action == "allow";
            }
            return allowed;
        }

        public static string MavenNameToPath(string mavenName)
        {
            // group:artifact:version[:clasificador] -> group/con/slashes/artifact/version/artifact-version[-clasificador].jar
            var parts = mavenName.Split(':');
            var group = parts[0].Replace('.', '/');
            var artifact = parts[1];
            var version = parts[2];
            var classifier = parts.Length > 3 ? "-" + parts[3] : "";
            return $"{group}/{artifact}/{version}/{artifact}-{version}{classifier}.jar";
        }

        public static async Task<ResolvedProfile> ResolveVanillaAsync(string mcVersion)
        {
            var vjson = await GetVersionJsonAsync(mcVersion);
            return new ResolvedProfile
            {
                MainClass = vjson.MainClass,
                Libraries = vjson.Libraries,
                Arguments = vjson.Arguments,
                MinecraftArguments = vjson.MinecraftArguments,
                AssetIndex = vjson.AssetIndex,
                Assets = vjson.Assets ?? "legacy",
                ClientDownload = vjson.Downloads?.Client,
                JavaMajor = vjson.JavaVersion?.MajorVersion ?? 8
            };
        }

        public static async Task<ResolvedProfile> ResolveFabricLikeAsync(string mcVersion, string loaderVersion, bool quilt)
        {
            var metaBase = quilt ? "https://meta.quiltmc.org/v3" : "https://meta.fabricmc.net/v2";
            var url = $"{metaBase}/versions/loader/{mcVersion}/{loaderVersion}/profile/json";
            var json = await _http.GetStringAsync(url);
            var profileJson = JsonSerializer.Deserialize<MojangVersionJson>(json)!;

            var vanilla = await ResolveVanillaAsync(mcVersion);

            var libs = new List<MojangLibrary>(profileJson.Libraries);
            libs.AddRange(vanilla.Libraries);

            return new ResolvedProfile
            {
                MainClass = profileJson.MainClass,
                Libraries = libs,
                Arguments = vanilla.Arguments,
                MinecraftArguments = vanilla.MinecraftArguments,
                AssetIndex = vanilla.AssetIndex,
                Assets = vanilla.Assets,
                ClientDownload = vanilla.ClientDownload,
                JavaMajor = vanilla.JavaMajor
            };
        }

        public static async Task<string> InstallForgeLikeAsync(string mcVersion, string forgeVersion, string instanceDir, bool neoforge, IProgress<string>? progress = null, CancellationToken ct = default)
        {
            string installerUrl;
            string installerName;
            if (neoforge)
                forgeVersion = LoaderVersionsApi.NormalizeNeoForgeVersion(mcVersion, forgeVersion);
            if (neoforge && LoaderVersionsApi.IsLegacyNeoForge(mcVersion, forgeVersion))
            {
                // NeoForge 1.20.1: esquema viejo (artefacto "forge" dentro de net/neoforged).
                installerUrl = $"https://maven.neoforged.net/releases/net/neoforged/forge/{forgeVersion}/forge-{forgeVersion}-installer.jar";
                installerName = $"neoforge-forge-{forgeVersion}-installer.jar";
            }
            else if (neoforge)
            {
                installerUrl = $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{forgeVersion}/neoforge-{forgeVersion}-installer.jar";
                installerName = $"neoforge-{forgeVersion}-installer.jar";
            }
            else
            {
                installerUrl = $"https://maven.minecraftforge.net/net/minecraftforge/forge/{mcVersion}-{forgeVersion}/forge-{mcVersion}-{forgeVersion}-installer.jar";
                installerName = $"forge-{mcVersion}-{forgeVersion}-installer.jar";
            }

            // Si esta versión del loader ya se instaló bien en la instancia, no hace falta correr el
            // instalador de nuevo (tardaba varios minutos en cada lanzamiento en una PC justa).
            var marker = Path.Combine(instanceDir, ".velin-loader-" + (neoforge ? "neoforge-" : "forge-") + mcVersion + "-" + forgeVersion);
            if (File.Exists(marker))
            {
                var existing = FindInstalledProfile(instanceDir, mcVersion, forgeVersion);
                if (existing != null) return existing;
            }

            var installerPath = Path.Combine(PathService.CacheDir, installerName);
            if (!File.Exists(installerPath))
            {
                progress?.Report("Descargando instalador de " + (neoforge ? "NeoForge" : "Forge") + "...");
                var bytes = await _http.GetByteArrayAsync(installerUrl, ct);
                await File.WriteAllBytesAsync(installerPath, bytes);
            }

            var javaExe = await JavaManager.EnsureJavaAsync(21, progress, null, ct);
            javaExe = javaExe.Replace("javaw.exe", "java.exe");

            Directory.CreateDirectory(instanceDir);
            // Algunos instaladores de Forge sólo se comportan bien si el directorio destino
            // "parece" una carpeta de Minecraft válida; nos aseguramos de que exista este archivo.
            var profilesFile = Path.Combine(instanceDir, "launcher_profiles.json");
            if (!File.Exists(profilesFile))
                await File.WriteAllTextAsync(profilesFile, "{\"profiles\":{}}");

            progress?.Report("Ejecutando instalador (puede tardar varios minutos)...");
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = javaExe,
                WorkingDirectory = PathService.CacheDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-jar");
            psi.ArgumentList.Add(installerPath);
            psi.ArgumentList.Add("--installClient");
            psi.ArgumentList.Add(instanceDir);

            using var proc = System.Diagnostics.Process.Start(psi)!;
            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            try { await proc.WaitForExitAsync(ct); }
            catch (OperationCanceledException) { try { proc.Kill(true); } catch { } throw; }
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            var versionsFolder = Path.Combine(instanceDir, "versions");
            if (!Directory.Exists(versionsFolder))
                throw new Exception(
                    $"El instalador no generó la carpeta 'versions'. Código de salida: {proc.ExitCode}.\n" +
                    $"Salida del instalador: {Truncate(stdout)}\nErrores: {Truncate(stderr)}");

            var candidate = FindInstalledProfile(instanceDir, mcVersion, forgeVersion);

            if (candidate == null)
            {
                var found = string.Join(", ", Directory.GetDirectories(versionsFolder).Select(Path.GetFileName));
                throw new Exception(
                    $"No se encontró el perfil generado por el instalador de {(neoforge ? "NeoForge" : "Forge")}.\n" +
                    $"Carpetas encontradas en 'versions': [{found}]\n" +
                    $"Código de salida del instalador: {proc.ExitCode}\n" +
                    $"Salida: {Truncate(stdout)}\nErrores: {Truncate(stderr)}");
            }

            try { File.WriteAllText(marker, DateTime.UtcNow.ToString("o")); } catch { }
            return candidate;
        }

        // Busca el perfil (JSON) que dejó el instalador. Si hay varios (cambió la versión del loader),
        // gana el que lleva la versión pedida en el nombre de su carpeta.
        private static string? FindInstalledProfile(string instanceDir, string mcVersion, string forgeVersion)
        {
            var versionsFolder = Path.Combine(instanceDir, "versions");
            if (!Directory.Exists(versionsFolder)) return null;
            var vanillaJsonName = mcVersion + ".json";
            // "1.20.1-47.1.106" -> la carpeta que deja el instalador se llama "1.20.1-forge-47.1.106".
            var key = forgeVersion.StartsWith(mcVersion + "-") ? forgeVersion.Substring(mcVersion.Length + 1) : forgeVersion;
            return Directory.GetDirectories(versionsFolder)
                .Select(d => Path.Combine(d, Path.GetFileName(d) + ".json"))
                .Where(File.Exists)
                .Where(f => !Path.GetFileName(f).Equals(vanillaJsonName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => Path.GetFileName(Path.GetDirectoryName(f))!.Contains(key, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
        }

        private static string Truncate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "(vacío)";
            s = s.Trim();
            return s.Length > 600 ? s.Substring(0, 600) + "..." : s;
        }

        // Un JSON con "inheritsFrom" (Forge/NeoForge/Fabric) SUMA sus argumentos a los de vainilla:
        // primero van los del padre y después los del hijo. Antes el hijo pisaba al padre y se perdían
        // --username, --gameDir, etc. (y los argumentos de JVM nunca se usaban).
        private static MojangArguments? MergeArguments(MojangArguments? parent, MojangArguments? child)
        {
            if (child == null) return parent;
            if (parent == null) return child;
            var merged = new MojangArguments();
            merged.Jvm.AddRange(parent.Jvm); merged.Jvm.AddRange(child.Jvm);
            merged.Game.AddRange(parent.Game); merged.Game.AddRange(child.Game);
            return merged;
        }

        public static async Task<ResolvedProfile> ResolveForgeLikeAsync(string mcVersion, string forgeVersion, string instanceDir, bool neoforge, IProgress<string>? progress = null, CancellationToken ct = default)
        {
            var forgeJsonPath = await InstallForgeLikeAsync(mcVersion, forgeVersion, instanceDir, neoforge, progress, ct);
            var forgeJson = JsonSerializer.Deserialize<MojangVersionJson>(await File.ReadAllTextAsync(forgeJsonPath))!;

            ResolvedProfile baseProfile = !string.IsNullOrEmpty(forgeJson.InheritsFrom)
                ? await ResolveVanillaAsync(forgeJson.InheritsFrom)
                : await ResolveVanillaAsync(mcVersion);

            var libs = new List<MojangLibrary>(forgeJson.Libraries);
            libs.AddRange(baseProfile.Libraries);

            return new ResolvedProfile
            {
                MainClass = string.IsNullOrEmpty(forgeJson.MainClass) ? baseProfile.MainClass : forgeJson.MainClass,
                Libraries = libs,
                Arguments = MergeArguments(baseProfile.Arguments, forgeJson.Arguments),
                MinecraftArguments = forgeJson.MinecraftArguments ?? baseProfile.MinecraftArguments,
                AssetIndex = baseProfile.AssetIndex,
                Assets = baseProfile.Assets,
                ClientDownload = baseProfile.ClientDownload,
                JavaMajor = baseProfile.JavaMajor
            };
        }

        public static async Task DownloadClientAndLibrariesAsync(ResolvedProfile profile, string instanceVersionsDir, string mcVersion, string nativesDir, string? extraLibDir, IProgress<string>? progress = null, IProgress<double>? percent = null, Action<string>? onWarning = null, CancellationToken ct = default, string? customJarPath = null)
        {
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(instanceVersionsDir);
            Directory.CreateDirectory(nativesDir);

            var clientJarPath = Path.Combine(instanceVersionsDir, $"{mcVersion}.jar");

            if (customJarPath != null)
            {
                // Instancia con .jar propio: se usa ese cliente (no se baja el de Mojang). Las librerias y los assets
                // siguen siendo los de la version base.
                if (CustomJar.PrepareClientJar(customJarPath, clientJarPath))
                    progress?.Report("Usando tu .jar propio como cliente...");
            }
            else
            {
                // Si el jar ya existe pero quedo incompleto (tamano distinto al que informa Mojang) se baja de nuevo.
                if (File.Exists(clientJarPath)
                    && profile.ClientDownload is { Size: > 0 } existingInfo
                    && new FileInfo(clientJarPath).Length != existingInfo.Size)
                {
                    try { File.Delete(clientJarPath); } catch { }
                }

                if (!File.Exists(clientJarPath) && profile.ClientDownload != null)
                {
                    progress?.Report("Descargando cliente de Minecraft...");
                    await FileDownloader.DownloadAsync(profile.ClientDownload.Url, clientJarPath,
                        profile.ClientDownload.Sha1, profile.ClientDownload.Size, ct: ct);
                }
            }
            percent?.Report(0.1);

            // Descargas de librerías en paralelo. Cada una se verifica (tamaño/SHA1 cuando Mojang lo informa)
            // y se reintenta; lo que no se pueda bajar se avisa al final en vez de ignorarse en silencio.
            progress?.Report("Descargando librerías...");
            int libTotal = profile.Libraries.Count;
            int libDone = 0;
            var failures = new ConcurrentBag<string>();
            var claimed = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            using var libGate = new SemaphoreSlim(8);
            var libTasks = profile.Libraries.Select(async lib =>
            {
                if (!RuleAllowsWindows(lib.Rules)) { ReportLib(); return; }
                await libGate.WaitAsync(ct);
                try
                {
                    var relPath = lib.Downloads?.Artifact?.Path;
                    if (string.IsNullOrEmpty(relPath) && !string.IsNullOrEmpty(lib.Name))
                        relPath = MavenNameToPath(lib.Name);

                    // Si el instalador de Forge/NeoForge ya la dejó en su propia carpeta de libraries, no hace falta bajarla de nuevo.
                    bool alreadyInExtra = false;
                    if (extraLibDir != null && !string.IsNullOrEmpty(relPath))
                    {
                        var inExtra = Path.Combine(extraLibDir, relPath.Replace('/', Path.DirectorySeparatorChar));
                        alreadyInExtra = File.Exists(inExtra);
                    }

                    if (!alreadyInExtra)
                    {
                        string? url = null, dest = null, sha1 = null;
                        long size = 0;

                        if (lib.Downloads?.Artifact != null && !string.IsNullOrEmpty(lib.Downloads.Artifact.Path))
                        {
                            var art = lib.Downloads.Artifact;
                            dest = Path.Combine(PathService.LibrariesDir, art.Path.Replace('/', Path.DirectorySeparatorChar));
                            url = art.Url; sha1 = art.Sha1; size = art.Size;
                        }
                        else if (!string.IsNullOrEmpty(lib.Url) && !string.IsNullOrEmpty(relPath))
                        {
                            dest = Path.Combine(PathService.LibrariesDir, relPath.Replace('/', Path.DirectorySeparatorChar));
                            url = lib.Url.TrimEnd('/') + "/" + relPath;
                        }

                        // Sin URL (ej: librerías que genera el instalador de Forge) no hay nada que bajar.
                        if (dest != null && !string.IsNullOrEmpty(url)
                            && claimed.TryAdd(dest, 0)
                            && !FileDownloader.IsValidExisting(dest, size))
                        {
                            try { await FileDownloader.DownloadAsync(url, dest, sha1, size, ct: ct); }
                            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                            catch { failures.Add(lib.Name); }
                        }
                    }

                    if (lib.Natives != null && lib.Natives.TryGetValue("windows", out var classifierKey) && lib.Downloads?.Classifiers != null)
                    {
                        var key = classifierKey.Replace("${arch}", "64");
                        if (lib.Downloads.Classifiers.TryGetValue(key, out var nat) && !string.IsNullOrEmpty(nat.Url))
                        {
                            var tmp = Path.Combine(PathService.CacheDir, Path.GetFileName(nat.Path));
                            bool ready = true;
                            if (!FileDownloader.IsValidExisting(tmp, nat.Size))
                            {
                                try { await FileDownloader.DownloadAsync(nat.Url, tmp, nat.Sha1, nat.Size, ct: ct); }
                                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                                catch { failures.Add(lib.Name + " (nativos)"); ready = false; }
                            }
                            // Varios zips de nativos comparten archivos tipo META-INF: si chocan al extraer, no es grave.
                            if (ready) { try { System.IO.Compression.ZipFile.ExtractToDirectory(tmp, nativesDir, true); } catch { } }
                        }
                    }
                }
                finally { libGate.Release(); ReportLib(); }
            }).ToList();

            await Task.WhenAll(libTasks);
            percent?.Report(1.0);

            if (!failures.IsEmpty)
                onWarning?.Invoke(FormatFailures("librería(s)", failures));

            void ReportLib()
            {
                var d = Interlocked.Increment(ref libDone);
                if (libTotal > 0) percent?.Report(0.1 + 0.9 * d / libTotal);
            }
        }

        private static string FormatFailures(string what, IEnumerable<string> names)
        {
            var list = names.Distinct().OrderBy(n => n).ToList();
            var shown = string.Join(", ", list.Take(5));
            var more = list.Count > 5 ? $" y {list.Count - 5} más" : "";
            return $"No se pudieron descargar {list.Count} {what} tras varios intentos: {shown}{more}.\n" +
                   "El juego podría no iniciar o fallar durante la partida.";
        }

        public static async Task DownloadAssetsAsync(ResolvedProfile profile, IProgress<string>? progress = null, IProgress<double>? percent = null, Action<string>? onWarning = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (profile.AssetIndex == null) { percent?.Report(1.0); return; }

            var indexDir = Path.Combine(PathService.AssetsDir, "indexes");
            Directory.CreateDirectory(indexDir);
            var indexFile = Path.Combine(indexDir, $"{profile.AssetIndex.Id}.json");

            string json;
            if (File.Exists(indexFile))
                json = await File.ReadAllTextAsync(indexFile);
            else
            {
                json = await _http.GetStringAsync(profile.AssetIndex.Url, ct);
                await File.WriteAllTextAsync(indexFile, json);
            }

            var index = JsonSerializer.Deserialize<AssetIndexJson>(json)!;
            var objectsDir = Path.Combine(PathService.AssetsDir, "objects");

            // Faltan los que no existen o quedaron con un tamaño distinto al del índice (descarga cortada).
            // Se agrupa por hash porque varios nombres de archivo pueden apuntar al mismo contenido.
            var pending = index.Objects.Values
                .GroupBy(v => v.Hash)
                .Select(g => g.First())
                .Where(v => !FileDownloader.IsValidExisting(Path.Combine(objectsDir, v.Hash.Substring(0, 2), v.Hash), v.Size))
                .ToList();

            int total = pending.Count;
            if (total == 0) { percent?.Report(1.0); return; }

            int done = 0, failed = 0;
            using var gate = new SemaphoreSlim(10); // descargas de assets en paralelo
            var tasks = pending.Select(async obj =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    var sub = obj.Hash.Substring(0, 2);
                    var dest = Path.Combine(objectsDir, sub, obj.Hash);
                    var url = $"https://resources.download.minecraft.net/{sub}/{obj.Hash}";
                    try { await FileDownloader.DownloadAsync(url, dest, obj.Hash, obj.Size, ct: ct); }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch { Interlocked.Increment(ref failed); }

                    var d = Interlocked.Increment(ref done);
                    if (d % 20 == 0) progress?.Report($"Descargando assets ({d}/{total})...");
                    percent?.Report((double)d / total);
                }
                finally { gate.Release(); }
            });

            await Task.WhenAll(tasks);
            percent?.Report(1.0);

            if (failed > 0)
                onWarning?.Invoke($"No se pudieron descargar {failed} de {total} recursos del juego (sonidos/texturas) tras varios intentos.\n" +
                                  "Minecraft puede arrancar igual, pero con algunos recursos faltantes.");
        }
    }
}
