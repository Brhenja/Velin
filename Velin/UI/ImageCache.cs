using System;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Velin.Services;

namespace Velin.UI
{
    // Descarga imágenes (íconos y capturas de los mods) una sola vez y las guarda en data\cache\img.
    // Sin dependencias extra: usa System.Drawing. Los .webp no los decodifica Windows (GDI+); en ese caso
    // se prueba con la URL alternativa y, si tampoco anda, simplemente no se muestra la imagen.
    public static class ImageCache
    {
        private const long MaxBytes = 8 * 1024 * 1024;
        private const int MaxSide = 1280;

        private static readonly HttpClient Http = Create();

        private static HttpClient Create()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.UserAgent);
            return c;
        }

        public static string Dir => Path.Combine(PathService.CacheDir, "img");

        public static async Task<Image?> LoadAsync(string url, string fallbackUrl = "", CancellationToken ct = default)
        {
            var img = await TryLoadAsync(url, ct);
            if (img == null && !string.IsNullOrEmpty(fallbackUrl) && fallbackUrl != url)
                img = await TryLoadAsync(fallbackUrl, ct);
            return img;
        }

        private static async Task<Image?> TryLoadAsync(string url, CancellationToken ct)
        {
            if (!ProjectDetailsService.IsSafeUrl(url)) return null;
            try
            {
                var file = Path.Combine(Dir, Hash(url) + ".img");
                byte[] bytes;
                if (File.Exists(file))
                {
                    bytes = await File.ReadAllBytesAsync(file, ct);
                }
                else
                {
                    using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                    if (!resp.IsSuccessStatusCode) return null;
                    if (resp.Content.Headers.ContentLength is > MaxBytes) return null;
                    bytes = await resp.Content.ReadAsByteArrayAsync(ct);
                    if (bytes.LongLength > MaxBytes) return null;
                }

                var image = Decode(bytes);
                if (image == null) return null;

                if (!File.Exists(file))
                {
                    try { Directory.CreateDirectory(Dir); await File.WriteAllBytesAsync(file, bytes, ct); } catch { }
                }
                return image;
            }
            catch (OperationCanceledException) { throw; }
            catch { return null; }
        }

        // Copia la imagen a un Bitmap propio (así no queda ligada al stream) y la achica si es enorme.
        private static Image? Decode(byte[] bytes)
        {
            try
            {
                using var ms = new MemoryStream(bytes);
                using var source = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: true);
                var scale = Math.Min(1.0, (double)MaxSide / Math.Max(source.Width, source.Height));
                var w = Math.Max(1, (int)(source.Width * scale));
                var h = Math.Max(1, (int)(source.Height * scale));
                var bmp = new Bitmap(w, h);
                using var g = Graphics.FromImage(bmp);
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(source, 0, 0, w, h);
                return bmp;
            }
            catch { return null; }
        }

        private static string Hash(string url)
        {
            var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(url));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        // Peso de la carpeta de caché (para mostrarlo en Configuración) y limpieza.
        public static long CacheSizeBytes()
        {
            long total = 0;
            try
            {
                if (Directory.Exists(PathService.CacheDir))
                    foreach (var f in Directory.EnumerateFiles(PathService.CacheDir, "*", SearchOption.AllDirectories))
                        try { total += new FileInfo(f).Length; } catch { }
            }
            catch { }
            return total;
        }

        public static void ClearCache()
        {
            try
            {
                if (!Directory.Exists(PathService.CacheDir)) return;
                foreach (var f in Directory.EnumerateFiles(PathService.CacheDir, "*", SearchOption.AllDirectories))
                    try { File.Delete(f); } catch { }
                foreach (var d in Directory.EnumerateDirectories(PathService.CacheDir))
                    try { Directory.Delete(d, true); } catch { }
            }
            catch { }
        }
    }
}
