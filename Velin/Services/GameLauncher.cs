using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Velin.Models;

namespace Velin.Services
{
    public static class GameLauncher
    {
        // Envuelve el progreso "global" (0.0-1.0) para que cada etapa reporte dentro
        // de su propia porción del total, en vez de que cada una vuelva a empezar en 0%.
        private class StageProgress : IProgress<double>
        {
            private readonly IProgress<double>? _overall;
            private readonly double _start, _span;
            public StageProgress(IProgress<double>? overall, double start, double span) { _overall = overall; _start = start; _span = span; }
            public void Report(double value) => _overall?.Report(_start + Math.Clamp(value, 0, 1) * _span);
        }

        public static async Task<System.Diagnostics.Process> LaunchAsync(Instance instance, IProgress<string>? progress = null, IProgress<double>? percent = null, Action<string>? onWarning = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            instance.EnsureFolders();
            PathService.EnsureAll();

            // Advertencias del arranque (descargas que fallaron, etc.): se muestran Y quedan en el log.
            var startupWarnings = new List<string>();
            void Warn(string message) { startupWarnings.Add(message); onWarning?.Invoke(message); }

            // Java propio de la instancia (opcional): se valida antes de descargar nada.
            var customJava = (instance.JavaPath ?? "").Trim();
            if (customJava.Length > 0 && !File.Exists(customJava))
                throw new Exception($"El Java configurado para esta instancia no existe:\n{customJava}");

            // Cuenta: la de la instancia, si no la predeterminada, si no offline con el nombre de la instancia.
            // Se resuelve ANTES de descargar nada, así un error de login se ve enseguida.
            var appSettings = SettingsManager.Load();
            var account = AccountManager.Resolve(instance, appSettings);
            progress?.Report(account != null && account.IsMicrosoft ? "Verificando la sesión de Microsoft..." : "Preparando la cuenta...");
            var session = await AccountManager.GetSessionAsync(account, instance, MicrosoftAuth.ResolveClientId(appSettings.MicrosoftClientId), ct);

            ResolvedProfile profile;
            string? extraLibDir = null;

            switch (instance.Loader)
            {
                case ModLoaderType.Fabric:
                    progress?.Report("Resolviendo perfil de Fabric...");
                    profile = await MinecraftDownloader.ResolveFabricLikeAsync(instance.McVersion, instance.LoaderVersion!, quilt: false);
                    break;
                case ModLoaderType.Quilt:
                    progress?.Report("Resolviendo perfil de Quilt...");
                    profile = await MinecraftDownloader.ResolveFabricLikeAsync(instance.McVersion, instance.LoaderVersion!, quilt: true);
                    break;
                case ModLoaderType.Forge:
                    extraLibDir = Path.Combine(instance.InstanceDir, "libraries");
                    profile = await MinecraftDownloader.ResolveForgeLikeAsync(instance.McVersion, instance.LoaderVersion!, instance.InstanceDir, neoforge: false, progress, ct);
                    break;
                case ModLoaderType.NeoForge:
                    extraLibDir = Path.Combine(instance.InstanceDir, "libraries");
                    profile = await MinecraftDownloader.ResolveForgeLikeAsync(instance.McVersion, instance.LoaderVersion!, instance.InstanceDir, neoforge: true, progress, ct);
                    break;
                default:
                    progress?.Report("Resolviendo versión vanilla...");
                    profile = await MinecraftDownloader.ResolveVanillaAsync(instance.McVersion);
                    break;
            }

            // Reparto del progreso real entre etapas: Java 0-15%, cliente+librerías 15-40%,
            // assets 40-100% (suelen ser, por lejos, lo que más tarda la primera vez).
            string javaExe;
            if (customJava.Length > 0)
            {
                javaExe = customJava; // Java elegido a mano para esta instancia: no se baja nada
            }
            else
            {
                progress?.Report($"Preparando Java {profile.JavaMajor}...");
                javaExe = await JavaManager.EnsureJavaAsync(profile.JavaMajor, progress, new StageProgress(percent, 0.0, 0.15), ct);
            }

            await MinecraftDownloader.DownloadClientAndLibrariesAsync(profile, instance.VersionsDir, instance.McVersion, instance.NativesDir, extraLibDir, progress, new StageProgress(percent, 0.15, 0.25), Warn, ct,
                customJarPath: instance.CustomJar.Length > 0 ? Path.Combine(instance.InstanceDir, instance.CustomJar) : null);
            await MinecraftDownloader.DownloadAssetsAsync(profile, progress, new StageProgress(percent, 0.40, 0.60), Warn, ct);
            percent?.Report(1.0);

            var classpath = BuildClasspath(profile, instance, extraLibDir);
            var mainClass = profile.MainClass;

            var jvmArgs = JavaArgsBuilder.BuildAikarFlags(instance.RamMB);

            var gameArgs = BuildGameArgs(profile, instance, session);
            AddLaunchOptionArgs(gameArgs, instance, Warn);

            // Proxy (si está configurado en Velin): también lo usa el propio juego (skins, servidores, etc.).
            if (!string.IsNullOrWhiteSpace(appSettings.ProxyHost) && appSettings.ProxyPort is > 0 and < 65536)
            {
                var proxyHost = appSettings.ProxyHost.Trim();
                foreach (var scheme in new[] { "http", "https" })
                {
                    jvmArgs.Add($"-D{scheme}.proxyHost={proxyHost}");
                    jvmArgs.Add($"-D{scheme}.proxyPort={appSettings.ProxyPort}");
                }
            }

            var profileJvm = BuildProfileJvmArgs(profile, instance, classpath, extraLibDir);
            if (profileJvm.Count > 0)
            {
                // Perfiles modernos (1.13+, NeoForge/Forge): el JSON define -cp, --add-opens, -p, etc.
                jvmArgs.AddRange(profileJvm);
                if (!jvmArgs.Any(a => a.StartsWith("-Djava.library.path=")))
                    jvmArgs.Add($"-Djava.library.path={instance.NativesDir}");
                if (!jvmArgs.Contains("-cp") && !jvmArgs.Contains("-classpath"))
                { jvmArgs.Add("-cp"); jvmArgs.Add(classpath); }
            }
            else
            {
                jvmArgs.Add($"-Djava.library.path={instance.NativesDir}");
                jvmArgs.Add("-cp");
                jvmArgs.Add(classpath);
            }

            ct.ThrowIfCancellationRequested();
            progress?.Report("Iniciando Minecraft...");

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = javaExe,
                WorkingDirectory = instance.GameDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var a in jvmArgs) psi.ArgumentList.Add(a);
            psi.ArgumentList.Add(mainClass);
            foreach (var a in gameArgs) psi.ArgumentList.Add(a);

            // Siempre se guarda un log del lanzamiento: el comando completo (con el accessToken oculto)
            // y toda la salida del proceso. Si Minecraft se cierra con un error, de ahí sale el diagnóstico.
            var log = new LaunchLog(LogPathFor(instance));
            log.Line($"[Velin] {DateTime.Now:yyyy-MM-dd HH:mm:ss} — Lanzamiento de \"{instance.Name}\" (Minecraft {instance.McVersion}, {instance.Loader})");
            foreach (var w in startupWarnings) log.Line("[Velin] ADVERTENCIA: " + w.Replace("\n", " "));
            log.Line("[Velin] Comando:");
            log.Line(LaunchLog.FormatCommand(javaExe, psi.ArgumentList));
            log.Line("[Velin] --- Salida del proceso ---");

            System.Diagnostics.Process? process;
            try { process = System.Diagnostics.Process.Start(psi); }
            catch { log.Dispose(); throw; }
            if (process == null) { log.Dispose(); throw new Exception("No se pudo iniciar el proceso de Java."); }

            process.OutputDataReceived += (s, e) => { if (e.Data != null) log.Line(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) log.Line(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _ = Task.Run(() =>
            {
                try
                {
                    process.WaitForExit(); // espera también a que se termine de leer la salida
                    log.Line($"[Velin] --- El proceso terminó (código {process.ExitCode}) ---");
                }
                catch { }
                finally { log.Dispose(); }
            });

            return process;
        }

        public static string LogPathFor(Instance instance) =>
            System.IO.Path.Combine(instance.InstanceDir, "velin-launch.log");

        // Opciones por instancia (todas opcionales; vacío = como siempre): ventana, pantalla completa,
        // entrar directo a un servidor o a un mundo, y argumentos extra del juego.
        private static void AddLaunchOptionArgs(List<string> gameArgs, Instance instance, Action<string> warn)
        {
            if (instance.Fullscreen)
            {
                gameArgs.Add("--fullscreen");
            }
            else if (instance.WindowWidth > 0 && instance.WindowHeight > 0)
            {
                gameArgs.Add("--width"); gameArgs.Add(instance.WindowWidth.ToString());
                gameArgs.Add("--height"); gameArgs.Add(instance.WindowHeight.ToString());
            }

            bool quickPlay = SupportsQuickPlay(instance.McVersion);
            var server = (instance.JoinServer ?? "").Trim();
            var world = (instance.JoinWorld ?? "").Trim();

            if (server.Length > 0)
            {
                if (quickPlay)
                {
                    gameArgs.Add("--quickPlayMultiplayer"); gameArgs.Add(server);
                }
                else
                {
                    // Versiones anteriores a 1.20: --server <host> --port <puerto>
                    var host = server;
                    var port = "25565";
                    int colon = server.LastIndexOf(':');
                    if (colon > 0 && colon < server.Length - 1 && server.IndexOf(':') == colon)
                    {
                        host = server.Substring(0, colon);
                        port = server.Substring(colon + 1);
                    }
                    gameArgs.Add("--server"); gameArgs.Add(host);
                    gameArgs.Add("--port"); gameArgs.Add(port);
                }
            }

            if (world.Length > 0)
            {
                if (!quickPlay)
                    warn("Entrar directo a un mundo sólo funciona en Minecraft 1.20 o más nuevo; se ignora esa opción.");
                else if (server.Length > 0)
                    warn("Se configuró un servidor y un mundo a la vez; Minecraft sólo admite uno, se usa el servidor.");
                else
                {
                    gameArgs.Add("--quickPlaySingleplayer"); gameArgs.Add(world);
                }
            }

            gameArgs.AddRange(ArgsParser.Split(instance.ExtraGameArgs));
        }

        // "Quick Play" (entrar directo a un mundo/servidor) existe desde Minecraft 1.20.
        private static bool SupportsQuickPlay(string mcVersion)
        {
            var m = Regex.Match(mcVersion, @"^1\.(\d+)");
            if (m.Success) return int.Parse(m.Groups[1].Value) >= 20;

            m = Regex.Match(mcVersion, @"^(\d{2})w\d+");           // snapshots: 23w14a
            if (m.Success) return int.Parse(m.Groups[1].Value) >= 23;

            m = Regex.Match(mcVersion, @"^(\d+)\.\d+");            // esquema nuevo de versiones (ej: 26.1)
            if (m.Success) return int.Parse(m.Groups[1].Value) >= 2;

            return false;                                           // betas/alphas y desconocidas
        }

        private static string BuildClasspath(ResolvedProfile profile, Instance instance, string? extraLibDir)
        {
            var paths = new List<string>();
            var seen = new HashSet<string>();

            foreach (var lib in profile.Libraries)
            {
                if (!MinecraftDownloader.RuleAllowsWindows(lib.Rules)) continue;
                if (lib.Natives != null) continue; // los nativos no van en el classpath, van como .dll sueltas

                var relPath = lib.Downloads?.Artifact?.Path;
                if (string.IsNullOrEmpty(relPath) && !string.IsNullOrEmpty(lib.Name))
                    relPath = MinecraftDownloader.MavenNameToPath(lib.Name);
                if (string.IsNullOrEmpty(relPath)) continue;

                relPath = relPath.Replace('/', Path.DirectorySeparatorChar);

                string? full = null;
                if (extraLibDir != null)
                {
                    var candidate = Path.Combine(extraLibDir, relPath);
                    if (File.Exists(candidate)) full = candidate;
                }
                if (full == null)
                {
                    var candidate = Path.Combine(PathService.LibrariesDir, relPath);
                    if (File.Exists(candidate)) full = candidate;
                }

                if (full != null && seen.Add(full))
                    paths.Add(full);
            }

            var clientJar = Path.Combine(instance.VersionsDir, $"{instance.McVersion}.jar");
            if (File.Exists(clientJar)) paths.Add(clientJar);

            return string.Join(";", paths);
        }

        internal static string OfflineUuid(string username)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + username));
            hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
            hash[8] = (byte)((hash[8] & 0x3f) | 0x80);
            return new Guid(hash).ToString("N");
        }

        private static List<string> BuildGameArgs(ResolvedProfile profile, Instance instance, LaunchSession session)
        {
            var map = new Dictionary<string, string>
            {
                ["${auth_player_name}"] = session.Username,
                ["${version_name}"] = instance.McVersion,
                ["${game_directory}"] = instance.GameDir,
                ["${assets_root}"] = PathService.AssetsDir,
                ["${assets_index_name}"] = profile.Assets,
                ["${auth_uuid}"] = session.Uuid,
                ["${auth_access_token}"] = session.AccessToken,
                ["${auth_session}"] = session.AccessToken,
                ["${user_type}"] = session.UserType,
                ["${version_type}"] = "Velin",
                ["${clientid}"] = "0",
                ["${auth_xuid}"] = "0",
                ["${user_properties}"] = "{}"
            };

            var result = new List<string>();

            if (profile.Arguments?.Game != null && profile.Arguments.Game.Count > 0)
            {
                foreach (var el in profile.Arguments.Game)
                {
                    if (el.ValueKind == JsonValueKind.String)
                        result.Add(Substitute(el.GetString()!, map));
                    // los elementos con "rules" (features como is_demo_user) se ignoran a propósito
                }
            }
            else if (!string.IsNullOrEmpty(profile.MinecraftArguments))
            {
                foreach (var token in profile.MinecraftArguments.Split(' '))
                    result.Add(Substitute(token, map));
            }

            return result;
        }

        // Argumentos de JVM definidos por el JSON de la versión (vainilla moderna + loader), ya con
        // las reglas evaluadas para Windows x64 y las variables ${...} reemplazadas.
        private static List<string> BuildProfileJvmArgs(ResolvedProfile profile, Instance instance, string classpath, string? extraLibDir)
        {
            var result = new List<string>();
            var jvm = profile.Arguments?.Jvm;
            if (jvm == null || jvm.Count == 0) return result;

            var map = new Dictionary<string, string>
            {
                ["${natives_directory}"] = instance.NativesDir,
                ["${launcher_name}"] = "Velin",
                ["${launcher_version}"] = AppInfo.Version,
                ["${classpath}"] = classpath,
                ["${classpath_separator}"] = ";",
                ["${library_directory}"] = extraLibDir ?? PathService.LibrariesDir,
                ["${version_name}"] = instance.McVersion
            };

            foreach (var el in jvm)
            {
                if (el.ValueKind == JsonValueKind.String)
                {
                    result.Add(Substitute(el.GetString()!, map));
                }
                else if (el.ValueKind == JsonValueKind.Object)
                {
                    List<MojangRule>? rules = null;
                    if (el.TryGetProperty("rules", out var r))
                        rules = JsonSerializer.Deserialize<List<MojangRule>>(r.GetRawText());
                    if (!MinecraftDownloader.RuleAllowsWindows(rules)) continue;
                    if (!el.TryGetProperty("value", out var v)) continue;
                    if (v.ValueKind == JsonValueKind.String) result.Add(Substitute(v.GetString()!, map));
                    else if (v.ValueKind == JsonValueKind.Array)
                        foreach (var x in v.EnumerateArray())
                            if (x.ValueKind == JsonValueKind.String) result.Add(Substitute(x.GetString()!, map));
                }
            }
            return result;
        }

        private static string Substitute(string input, Dictionary<string, string> map)
        {
            foreach (var kv in map)
                input = input.Replace(kv.Key, kv.Value);
            return input;
        }
    }
}
