using System;
using System.IO;
using System.Threading.Tasks;

namespace Velin.Services
{
    // Escritura "todo o nada": se escribe a un archivo temporal y recién después reemplaza al definitivo.
    // Así un corte de luz o un cierre a la mitad no deja settings.json / instance.json truncados (y sin perder la configuración).
    public static class AtomicFile
    {
        public static void WriteAllText(string path, string content)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = path + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            try
            {
                File.WriteAllText(tmp, content);
                File.Move(tmp, path, true);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        public static async Task WriteAllTextAsync(string path, string content)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var tmp = path + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            try
            {
                await File.WriteAllTextAsync(tmp, content);
                File.Move(tmp, path, true);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
    }
}
