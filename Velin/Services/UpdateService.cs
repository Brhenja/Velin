using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Velin.Services
{
    public class UpdateInfo
    {
        public string Tag { get; set; } = "";
        public Version Version { get; set; } = new Version(0, 0, 0);
        public string Notes { get; set; } = "";
        public string PageUrl { get; set; } = "";
        public string InstallerName { get; set; } = "";
        public string InstallerUrl { get; set; } = "";
        public long InstallerSize { get; set; }
        public string Sha256 { get; set; } = "";   // vacío si la release no trae hash
    }

    // Busca la última release de Velin en GitHub y, si el usuario quiere, descarga el instalador y lo verifica.
    public static class UpdateService
    {
        private static readonly HttpClient Http = Create();

        private static HttpClient Create()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.UserAgent);
            c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return c;
        }

        // "owner/repo" sacado de AppInfo.RepoUrl (https://github.com/Brhenja/Velin).
        public static string RepoPath => new Uri(AppInfo.RepoUrl).AbsolutePath.Trim('/');

        // Con el instalador queda unins000.exe junto a Velin.exe; la versión portable no lo tiene.
        public static bool IsInstalledCopy => File.Exists(Path.Combine(PathService.BaseDir, "unins000.exe"));

        // "v1.2.0" / "1.2" / "1.2.0-beta" -> Version. false si no se entiende.
        public static bool TryParseVersion(string? text, out Version version)
        {
            version = new Version(0, 0, 0);
            if (string.IsNullOrWhiteSpace(text)) return false;
            var m = Regex.Match(text.Trim(), @"^[vV]?(\d+)\.(\d+)(?:\.(\d+))?");
            if (!m.Success) return false;
            version = new Version(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value),
                m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 0);
            return true;
        }

        public static bool IsNewer(string latestTag, string currentVersion) =>
            TryParseVersion(latestTag, out var latest) && TryParseVersion(currentVersion, out var current) && latest > current;

        // Devuelve la info de la última release SOLO si es más nueva que la instalada; null si ya está al día.
        // Lanza excepción si no hay red o GitHub no responde (el que llama decide si avisar).
        public static async Task<UpdateInfo?> CheckAsync(string currentVersion, CancellationToken ct = default)
        {
            var json = await Http.GetStringAsync($"https://api.github.com/repos/{RepoPath}/releases/latest", ct);
            var info = ParseRelease(json);
            return info != null && IsNewer(info.Tag, currentVersion) ? info : null;
        }

        public static UpdateInfo? ParseRelease(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            var tag = Str(r, "tag_name");
            if (!TryParseVersion(tag, out var version)) return null;

            var info = new UpdateInfo { Tag = tag, Version = version, Notes = Str(r, "body"), PageUrl = Str(r, "html_url") };
            string sumsUrl = "";
            if (r.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var a in assets.EnumerateArray())
                {
                    var name = Str(a, "name");
                    var url = Str(a, "browser_download_url");
                    if (Regex.IsMatch(name, @"^VelinSetup-.*\.exe$", RegexOptions.IgnoreCase) && info.InstallerUrl.Length == 0)
                    {
                        info.InstallerName = name;
                        info.InstallerUrl = url;
                        info.InstallerSize = a.TryGetProperty("size", out var sz) && sz.ValueKind == JsonValueKind.Number ? sz.GetInt64() : 0;
                        var digest = Str(a, "digest");
                        if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) info.Sha256 = digest.Substring(7);
                    }
                    else if (name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase)) sumsUrl = url;
                }
            }
            info.PageUrl = IsHttps(info.PageUrl) ? info.PageUrl : AppInfo.RepoUrl + "/releases/latest";
            // El hash que se publica con la release (SHA256SUMS.txt) se resuelve al descargar.
            info.Sha256 = info.Sha256.Length > 0 ? info.Sha256 : (sumsUrl.Length > 0 ? "@" + sumsUrl : "");
            return info;
        }

        // Solo se descarga desde las releases de este repositorio en GitHub.
        public static bool IsTrustedDownloadUrl(string? url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps) return false;
            return u.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                && u.AbsolutePath.StartsWith("/" + RepoPath + "/releases/download/", StringComparison.OrdinalIgnoreCase);
        }

        // Descarga el instalador a %TEMP%\VelinUpdate, verifica el SHA-256 si la release lo publica y devuelve la ruta.
        public static async Task<string> DownloadInstallerAsync(UpdateInfo info, IProgress<double>? progress, CancellationToken ct = default)
        {
            if (!IsTrustedDownloadUrl(info.InstallerUrl))
                throw new InvalidOperationException("La release no trae un instalador descargable desde GitHub.");

            var expected = info.Sha256;
            if (expected.StartsWith("@"))
            {
                try { expected = FindHash(await Http.GetStringAsync(expected.Substring(1), ct), info.InstallerName); }
                catch (OperationCanceledException) { throw; }
                catch { expected = ""; }
            }

            var dir = Path.Combine(Path.GetTempPath(), "VelinUpdate");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, Path.GetFileName(info.InstallerName));
            var part = path + ".part";
            try
            {
                using (var resp = await Http.GetAsync(info.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    resp.EnsureSuccessStatusCode();
                    var total = resp.Content.Headers.ContentLength ?? info.InstallerSize;
                    using var src = await resp.Content.ReadAsStreamAsync(ct);
                    using var dst = File.Create(part);
                    var buffer = new byte[81920];
                    long done = 0; int read;
                    while ((read = await src.ReadAsync(buffer, ct)) > 0)
                    {
                        await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                        done += read;
                        if (total > 0) progress?.Report((double)done / total);
                    }
                }

                if (!string.IsNullOrEmpty(expected) && !expected.StartsWith("@"))
                {
                    var actual = await JavaManager.ComputeSha256Async(part, ct);
                    if (!actual.Equals(expected.Trim(), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("La descarga está dañada (el SHA-256 no coincide). Probá de nuevo.");
                }

                if (File.Exists(path)) File.Delete(path);
                File.Move(part, path);
                progress?.Report(1.0);
                return path;
            }
            finally
            {
                try { if (File.Exists(part)) File.Delete(part); } catch { }
            }
        }

        // Busca "<hash>  <archivo>" en el contenido de SHA256SUMS.txt.
        public static string FindHash(string sumsText, string fileName)
        {
            foreach (var line in sumsText.Split('\n'))
            {
                var m = Regex.Match(line.Trim(), @"^([0-9a-fA-F]{64})\s+\*?(.+)$");
                if (m.Success && m.Groups[2].Value.Trim().Equals(fileName, StringComparison.OrdinalIgnoreCase)) return m.Groups[1].Value;
            }
            return "";
        }

        private static bool IsHttps(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps;

        private static string Str(JsonElement e, string name) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
    }
}
