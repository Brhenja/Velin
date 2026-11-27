using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace Velin.Services
{
    // Instancias con un .jar de cliente propio: se crea una carpeta en data\instances, se mete el .jar adentro y
    // Velin la reconoce sola como una version personalizada (por ejemplo, un Minecraft modificado).
    public static class CustomJar
    {
        // El primer .jar que haya directamente en la carpeta (no busca en subcarpetas).
        public static string? FindJarIn(string dir)
        {
            return Directory.GetFiles(dir, "*.jar").OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        }

        // Los clientes modernos (1.14+) traen "version.json" con el id de la version. Los viejos no: ahi devuelve null
        // y Velin le pregunta al usuario cual es la version base.
        public static string? TryReadVersion(string jarPath)
        {
            try
            {
                using var zip = ZipFile.OpenRead(jarPath);
                var entry = zip.GetEntry("version.json");
                if (entry == null) return null;
                using var s = entry.Open();
                using var doc = JsonDocument.Parse(s);
                if (doc.RootElement.TryGetProperty("id", out var id))
                {
                    var text = id.GetString();
                    return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
                }
            }
            catch { /* jar ilegible o sin version.json */ }
            return null;
        }

        // Copia el .jar propio como cliente de la instancia (versions\<version>.jar) solo si hace falta:
        // si falta o cambio (tamano o fecha). Devuelve true si copio.
        public static bool PrepareClientJar(string customJarPath, string clientJarPath)
        {
            if (!File.Exists(customJarPath))
                throw new FileNotFoundException("No se encontro el .jar propio de la instancia: " + customJarPath);

            var src = new FileInfo(customJarPath);
            var dst = new FileInfo(clientJarPath);
            if (dst.Exists && dst.Length == src.Length && dst.LastWriteTimeUtc == src.LastWriteTimeUtc) return false;

            Directory.CreateDirectory(Path.GetDirectoryName(clientJarPath)!);
            File.Copy(customJarPath, clientJarPath, true);
            File.SetLastWriteTimeUtc(clientJarPath, src.LastWriteTimeUtc);
            return true;
        }
    }
}
