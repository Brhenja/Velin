using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Velin.Services
{
    public enum OptionalJavaKind { GraalVM, Semeru }

    // Java opcionales: GraalVM (Oracle GraalVM para JDK 21) e IBM Semeru Runtime Open Edition (OpenJ9).
    // No vienen con Velin: se descargan solo si el usuario los pide en Configuración y viven en data\java\graalvm-21\
    // y data\java\semeru-<versión>\, separados del Temurin automático (no se pisan).
    public static class OptionalJavaManager
    {
        public const int GraalMajor = 21;
        public static readonly int[] SemeruMajors = { 8, 17, 21 };

        public const string GraalUrl = "https://download.oracle.com/graalvm/21/latest/graalvm-jdk-21_windows-x64_bin.zip";

        public static string DirFor(OptionalJavaKind kind, int major) =>
            Path.Combine(PathService.JavaDir, kind == OptionalJavaKind.GraalVM ? $"graalvm-{major}" : $"semeru-{major}");

        public static string ExeFor(OptionalJavaKind kind, int major) => JavaManager.FindJavaw(DirFor(kind, major));

        public static bool IsInstalled(OptionalJavaKind kind, int major) => ExeFor(kind, major).Length > 0;

        public static bool AnySemeruInstalled() => SemeruMajors.Any(m => IsInstalled(OptionalJavaKind.Semeru, m));

        public static void Remove(OptionalJavaKind kind, int major)
        {
            var dir = DirFor(kind, major);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        public static string RuntimeId(OptionalJavaKind kind) => kind == OptionalJavaKind.GraalVM ? "graalvm" : "semeru";

        // Elige el Java opcional que corresponde a una instancia, o "" si no hay ninguno utilizable
        // (no activado en Configuración, no instalado o demasiado viejo para esa versión de Minecraft).
        public static string ResolveExe(string runtimeId, int requiredMajor, AppSettingsView settings)
        {
            if (runtimeId == "graalvm")
            {
                if (!settings.GraalEnabled || requiredMajor > GraalMajor || requiredMajor < 17) return "";
                return ExeFor(OptionalJavaKind.GraalVM, GraalMajor);
            }
            if (runtimeId == "semeru")
            {
                if (!settings.SemeruEnabled) return "";
                // Para Java 8 solo sirve el 8 (los Minecraft viejos no andan bien en Java más nuevo); para el resto, el menor que alcance.
                var candidates = requiredMajor <= 8
                    ? new[] { 8 }
                    : SemeruMajors.Where(m => m >= requiredMajor && m >= 17).OrderBy(m => m).ToArray();
                foreach (var m in candidates)
                {
                    var exe = ExeFor(OptionalJavaKind.Semeru, m);
                    if (exe.Length > 0) return exe;
                }
            }
            return "";
        }

        public static async Task InstallAsync(OptionalJavaKind kind, int major, IProgress<string>? progress, IProgress<double>? percent, CancellationToken ct)
        {
            var dir = DirFor(kind, major);
            if (kind == OptionalJavaKind.GraalVM)
            {
                progress?.Report("Descargando GraalVM 21...");
                string? sha = null;
                try
                {
                    // Oracle publica el SHA-256 al lado del .zip; si no se puede leer, se sigue sin verificar.
                    var text = (await JavaManager._http.GetStringAsync(GraalUrl + ".sha256", ct)).Trim();
                    var m = Regex.Match(text, "^[0-9a-fA-F]{64}");
                    if (m.Success) sha = m.Value;
                }
                catch (OperationCanceledException) { throw; }
                catch { }
                await JavaManager.DownloadAndExtractAsync(GraalUrl, dir, sha, progress, percent, ct);
            }
            else
            {
                progress?.Report($"Buscando IBM Semeru {major}...");
                var (url, sha) = await FindSemeruAssetAsync(major, ct);
                progress?.Report($"Descargando IBM Semeru {major} (OpenJ9)...");
                await JavaManager.DownloadAndExtractAsync(url, dir, sha, progress, percent, ct);
            }

            if (ExeFor(kind, major).Length == 0)
            {
                try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
                throw new Exception("La descarga terminó pero no se encontró javaw.exe dentro.");
            }
        }

        // IBM publica los builds en GitHub (ibmruntimes/semeruN-binaries). Se toma el .zip de Windows x64 más nuevo (JRE; si no hay, JDK).
        private static async Task<(string url, string? sha256)> FindSemeruAssetAsync(int major, CancellationToken ct)
        {
            var api = $"https://api.github.com/repos/ibmruntimes/semeru{major}-binaries/releases?per_page=6";
            using var req = new HttpRequestMessage(HttpMethod.Get, api);
            req.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var resp = await JavaManager._http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                throw new Exception($"No se pudo consultar las versiones de Semeru (HTTP {(int)resp.StatusCode}). Si GitHub te limitó las consultas, probá en unos minutos.");

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            foreach (var prefix in new[] { "ibm-semeru-open-jre_x64_windows", "ibm-semeru-open-jdk_x64_windows" })
            {
                foreach (var release in doc.RootElement.EnumerateArray())
                {
                    if (release.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) continue;
                    if (release.TryGetProperty("draft", out var dr) && dr.GetBoolean()) continue;
                    if (!release.TryGetProperty("assets", out var assets)) continue;
                    foreach (var asset in assets.EnumerateArray())
                    {
                        var name = asset.GetProperty("name").GetString() ?? "";
                        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                        var url = asset.GetProperty("browser_download_url").GetString() ?? "";
                        if (url.Length == 0) continue;
                        string? sha = null;
                        if (asset.TryGetProperty("digest", out var dg) && dg.ValueKind == JsonValueKind.String)
                        {
                            var d = dg.GetString() ?? "";
                            if (d.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) sha = d.Substring(7);
                        }
                        return (url, sha);
                    }
                }
            }
            throw new Exception($"No encontré un .zip de IBM Semeru {major} para Windows x64.");
        }
    }

    // Vista mínima de los ajustes para decidir el Java (evita depender de la clase completa y facilita las pruebas).
    public readonly record struct AppSettingsView(bool GraalEnabled, bool SemeruEnabled);
}
