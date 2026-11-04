using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Velin.Services
{
    // Java "de fábrica" de Velin: Eclipse Temurin (JRE), descargado solo según la versión de Minecraft.
    public static class JavaManager
    {
        internal static readonly HttpClient _http = CreateHttp();

        private static HttpClient CreateHttp()
        {
            var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Velin-Launcher (github.com/Brhenja/Velin)");
            return http;
        }

        public static string JavaExeFor(int majorVersion) => FindJavaw(Path.Combine(PathService.JavaDir, majorVersion.ToString()));

        internal static string FindJavaw(string dir)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    var found = Directory.GetFiles(dir, "javaw.exe", SearchOption.AllDirectories);
                    if (found.Length > 0) return found[0];
                }
            }
            catch { }
            return "";
        }

        public static async Task<string> EnsureJavaAsync(int majorVersion, IProgress<string>? progress = null, IProgress<double>? percent = null, CancellationToken ct = default)
        {
            var existing = JavaExeFor(majorVersion);
            if (!string.IsNullOrEmpty(existing)) { percent?.Report(1.0); return existing; }

            var dir = Path.Combine(PathService.JavaDir, majorVersion.ToString());
            progress?.Report($"Descargando Java {majorVersion} (Eclipse Temurin)...");
            // API de Adoptium: siempre devuelve el último build estable de ese major para Windows x64.
            var url = $"https://api.adoptium.net/v3/binary/latest/{majorVersion}/ga/windows/x64/jre/hotspot/normal/eclipse";
            await DownloadAndExtractAsync(url, dir, null, progress, percent, ct);

            var exe = JavaExeFor(majorVersion);
            if (string.IsNullOrEmpty(exe))
                throw new Exception($"No se encontró javaw.exe tras extraer Java {majorVersion}.");
            return exe;
        }

        // Baja un .zip (con progreso y cancelación), lo verifica si se conoce el SHA-256 y lo extrae en una carpeta
        // temporal que recién al final reemplaza a la definitiva: si se corta a la mitad no queda un Java roto.
        internal static async Task DownloadAndExtractAsync(string url, string targetDir, string? expectedSha256,
            IProgress<string>? progress, IProgress<double>? percent, CancellationToken ct)
        {
            Directory.CreateDirectory(PathService.JavaDir);
            var zipPath = targetDir + ".download.zip";
            var tmpDir = targetDir + ".extracting";
            try
            {
                using (var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    resp.EnsureSuccessStatusCode();
                    var totalBytes = resp.Content.Headers.ContentLength;
                    using var httpStream = await resp.Content.ReadAsStreamAsync(ct);
                    using var fs = File.Create(zipPath);

                    var buffer = new byte[81920];
                    long readSoFar = 0;
                    int read;
                    while ((read = await httpStream.ReadAsync(buffer, ct)) > 0)
                    {
                        await fs.WriteAsync(buffer.AsMemory(0, read), ct);
                        readSoFar += read;
                        if (totalBytes is > 0)
                            percent?.Report((double)readSoFar / totalBytes.Value * 0.9); // dejamos 10% para la extracción
                    }
                }

                ct.ThrowIfCancellationRequested();

                if (!string.IsNullOrWhiteSpace(expectedSha256))
                {
                    progress?.Report("Verificando descarga...");
                    var actual = await ComputeSha256Async(zipPath, ct);
                    if (!string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("La descarga de Java está dañada (el SHA-256 no coincide). Probá de nuevo.");
                }

                progress?.Report("Extrayendo Java...");
                if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true);
                await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, tmpDir, true), ct);
                ct.ThrowIfCancellationRequested();

                if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
                Directory.Move(tmpDir, targetDir);
                percent?.Report(1.0);
            }
            finally
            {
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
                try { if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true); } catch { }
            }
        }

        internal static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(path);
            var hash = await sha.ComputeHashAsync(fs, ct);
            return Convert.ToHexString(hash);
        }
    }
}
