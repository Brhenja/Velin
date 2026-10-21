using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Velin.Services
{
    // Descarga de archivos con verificación: baja a un archivo temporal ".part", comprueba tamaño
    // y SHA1 (si se conocen), reintenta si algo sale mal, y sólo entonces lo mueve a su lugar
    // definitivo. Así un corte de internet nunca deja un archivo a medias haciéndose pasar por bueno.
    public static class FileDownloader
    {
        public const int DefaultAttempts = 3;

        private static readonly HttpClient _http = CreateClient();

        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.UserAgent);
            return c;
        }

        public static async Task DownloadAsync(string url, string dest, string? expectedSha1 = null,
            long expectedSize = 0, int attempts = DefaultAttempts, CancellationToken ct = default)
        {
            var dir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            Exception? last = null;
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                // Nombre temporal único: si dos descargas apuntan al mismo destino, no se pisan.
                var tmp = dest + "." + Guid.NewGuid().ToString("N") + ".part";
                try
                {
                    using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                    resp.EnsureSuccessStatusCode();

                    using (var src = await resp.Content.ReadAsStreamAsync(ct))
                    using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                    {
                        await src.CopyToAsync(fs, 81920, ct);
                    }

                    var length = new FileInfo(tmp).Length;
                    if (expectedSize > 0 && length != expectedSize)
                        throw new InvalidDataException($"tamaño incorrecto (esperado {expectedSize}, recibido {length})");

                    if (!string.IsNullOrEmpty(expectedSha1))
                    {
                        var actual = await Sha1Async(tmp, ct);
                        if (!string.Equals(actual, expectedSha1, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException("el archivo descargado no coincide con su hash SHA1");
                    }

                    File.Move(tmp, dest, true);
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    last = ex;
                    try { File.Delete(tmp); } catch { }
                    if (attempt < attempts) await Task.Delay(500 * attempt, ct);
                }
            }

            throw new IOException($"No se pudo descargar {url}: {last?.Message}", last);
        }

        // true si el archivo existe y, cuando el tamaño esperado se conoce, mide lo que debería.
        public static bool IsValidExisting(string path, long expectedSize)
        {
            try
            {
                if (!File.Exists(path)) return false;
                return expectedSize <= 0 || new FileInfo(path).Length == expectedSize;
            }
            catch { return false; }
        }

        public static async Task<string> Sha1Async(string path, CancellationToken ct = default)
        {
            using var sha1 = SHA1.Create();
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            var hash = await sha1.ComputeHashAsync(fs, ct);
            return Convert.ToHexString(hash);
        }

    }
}
