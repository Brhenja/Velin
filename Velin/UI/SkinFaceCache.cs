using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Velin.Services;

namespace Velin.UI
{
    // Skin pública de una cuenta Microsoft guardada en data\cache\skins\<uuid>.png, para mostrar la cara en la ventana principal
    // sin pedirle nada a Mojang en cada arranque (el servidor de sesiones limita las consultas).
    public static class SkinFaceCache
    {
        private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);

        public static string Dir => Path.Combine(PathService.CacheDir, "skins");

        private static string FileFor(string uuid) => Path.Combine(Dir, MojangSkinService.NormalizeUuid(uuid) + ".png");

        // La skin guardada (sin red); null si todavía no hay.
        public static Bitmap? LoadFace(string uuid, int size)
        {
            try
            {
                if (MojangSkinService.NormalizeUuid(uuid).Length == 0) return null;
                var file = FileFor(uuid);
                if (!File.Exists(file)) return null;
                using var skin = SkinRenderer.Load(File.ReadAllBytes(file));
                return skin == null ? null : SkinRenderer.RenderFace(skin, size);
            }
            catch { return null; }
        }

        public static bool IsStale(string uuid)
        {
            try
            {
                var file = FileFor(uuid);
                return !File.Exists(file) || DateTime.UtcNow - File.GetLastWriteTimeUtc(file) > MaxAge;
            }
            catch { return true; }
        }

        // Baja la skin pública si hace falta (o si force) y devuelve la cara. Ante cualquier error devuelve null y se sigue usando lo guardado.
        public static async Task<Bitmap?> RefreshFaceAsync(string uuid, int size, bool force = false, CancellationToken ct = default)
        {
            try
            {
                if (MojangSkinService.NormalizeUuid(uuid).Length == 0) return null;
                if (force || IsStale(uuid))
                {
                    var info = await MojangSkinService.GetPublicSkinAsync(uuid, ct);
                    if (info == null) return LoadFace(uuid, size); // sin skin propia (usa Steve/Alex)
                    var png = await MojangSkinService.DownloadSkinAsync(info.Url, ct);
                    Store(uuid, png);
                }
                return LoadFace(uuid, size);
            }
            catch (OperationCanceledException) { throw; }
            catch { return LoadFace(uuid, size); }
        }

        // La ventana de skins guarda acá la skin que acaba de bajar/subir, así la cara de la ventana principal se actualiza al toque.
        public static void Store(string uuid, byte[] png)
        {
            try
            {
                if (SkinLibrary.ValidatePng(png) != null || MojangSkinService.NormalizeUuid(uuid).Length == 0) return;
                Directory.CreateDirectory(Dir);
                var file = FileFor(uuid);
                var tmp = file + "." + Guid.NewGuid().ToString("N").Substring(0, 6) + ".tmp";
                File.WriteAllBytes(tmp, png);
                File.Move(tmp, file, true);
            }
            catch { }
        }
    }
}
