using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Velin.Services
{
    public static class JavaManager
    {
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

        public static string JavaExeFor(int majorVersion)
        {
            var dir = Path.Combine(PathService.JavaDir, majorVersion.ToString());
            if (Directory.Exists(dir))
            {
                var found = Directory.GetFiles(dir, "javaw.exe", SearchOption.AllDirectories);
                if (found.Length > 0) return found[0];
            }
            return "";
        }

        public static async Task<string> EnsureJavaAsync(int majorVersion, IProgress<string>? progress = null, IProgress<double>? percent = null, CancellationToken ct = default)
        {
            var existing = JavaExeFor(majorVersion);
            if (!string.IsNullOrEmpty(existing)) { percent?.Report(1.0); return existing; }

            var dir = Path.Combine(PathService.JavaDir, majorVersion.ToString());
            Directory.CreateDirectory(dir);

            progress?.Report($"Descargando Java {majorVersion} (Eclipse Temurin)...");
            // API de Adoptium: siempre devuelve el último build estable de ese major para Windows x64.
            var url = $"https://api.adoptium.net/v3/binary/latest/{majorVersion}/ga/windows/x64/jre/hotspot/normal/eclipse";
            var zipPath = Path.Combine(dir, "java.zip");

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
            progress?.Report("Extrayendo Java...");
            ZipFile.ExtractToDirectory(zipPath, dir, true);
            File.Delete(zipPath);
            percent?.Report(1.0);

            var exe = JavaExeFor(majorVersion);
            if (string.IsNullOrEmpty(exe))
                throw new Exception($"No se encontró javaw.exe tras extraer Java {majorVersion}.");
            return exe;
        }
    }
}
