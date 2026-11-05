using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Velin.Services
{
    public class SkinEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public bool Slim { get; set; }
        public string FileName { get; set; } = "";   // dentro de data\skins\
        public DateTime AddedUtc { get; set; } = DateTime.UtcNow;
        public override string ToString() => Name + (Slim ? "  (slim)" : "");
    }

    // Biblioteca local de skins: data\skins\*.png + data\skins\skins.json. No depende de ninguna cuenta,
    // así sirve igual para cuentas premium y offline.
    public static class SkinLibrary
    {
        private const int MaxBytes = 100 * 1024;
        private static readonly object Lock = new();
        private static string IndexFile => Path.Combine(PathService.SkinsDir, "skins.json");

        public static List<SkinEntry> List()
        {
            lock (Lock) return Load().OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static SkinEntry Add(string name, byte[] png, bool slim)
        {
            var error = ValidatePng(png);
            if (error != null) throw new SkinException(error);
            lock (Lock)
            {
                Directory.CreateDirectory(PathService.SkinsDir);
                var entry = new SkinEntry { Name = CleanName(name), Slim = slim };
                entry.FileName = entry.Id + ".png";
                File.WriteAllBytes(Path.Combine(PathService.SkinsDir, entry.FileName), png);
                var all = Load();
                all.Add(entry);
                Save(all);
                return entry;
            }
        }

        public static void Remove(string id)
        {
            lock (Lock)
            {
                var all = Load();
                var entry = all.FirstOrDefault(e => e.Id == id);
                if (entry == null) return;
                try { File.Delete(FilePath(entry)); } catch { }
                all.Remove(entry);
                Save(all);
            }
        }

        public static void Update(string id, string? name = null, bool? slim = null)
        {
            lock (Lock)
            {
                var all = Load();
                var entry = all.FirstOrDefault(e => e.Id == id);
                if (entry == null) return;
                if (name != null) entry.Name = CleanName(name);
                if (slim != null) entry.Slim = slim.Value;
                Save(all);
            }
        }

        public static byte[]? ReadPng(SkinEntry entry)
        {
            try { return File.ReadAllBytes(FilePath(entry)); } catch { return null; }
        }

        public static string FilePath(SkinEntry entry) => Path.Combine(PathService.SkinsDir, Path.GetFileName(entry.FileName));

        // Una skin válida es un PNG de 64x64 (o 64x32, formato antiguo). Se lee sólo el encabezado, sin decodificar la imagen.
        // Devuelve null si está bien o el motivo si no.
        public static string? ValidatePng(byte[] data)
        {
            if (data.Length > MaxBytes) return "El archivo es demasiado grande para ser una skin (máximo 100 KB).";
            if (!TryReadPngSize(data, out var w, out var h)) return "El archivo no es un PNG válido.";
            if (!((w == 64 && h == 64) || (w == 64 && h == 32)))
                return $"Una skin debe medir 64x64 (o 64x32) píxeles; este archivo mide {w}x{h}.";
            return null;
        }

        public static bool TryReadPngSize(byte[] data, out int width, out int height)
        {
            width = height = 0;
            if (data.Length < 24) return false;
            byte[] sig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            for (int i = 0; i < 8; i++) if (data[i] != sig[i]) return false;
            if (data[12] != 'I' || data[13] != 'H' || data[14] != 'D' || data[15] != 'R') return false;
            width = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
            height = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
            return width > 0 && height > 0;
        }

        // Nombre de archivo de skin que usa CustomSkinLoader para cuentas offline: LocalSkin\skins\<usuario>.png
        public static string CustomSkinLoaderPath(string gameDir, string username)
        {
            var safe = string.Concat(username.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            return Path.Combine(gameDir, "CustomSkinLoader", "LocalSkin", "skins", safe + ".png");
        }

        private static string CleanName(string name)
        {
            var n = (name ?? "").Trim();
            return n.Length == 0 ? "Skin" : (n.Length > 40 ? n.Substring(0, 40) : n);
        }

        private static List<SkinEntry> Load()
        {
            try
            {
                if (!File.Exists(IndexFile)) return new List<SkinEntry>();
                var all = JsonSerializer.Deserialize<List<SkinEntry>>(File.ReadAllText(IndexFile)) ?? new List<SkinEntry>();
                return all.Where(e => File.Exists(FilePath(e))).ToList(); // ignora entradas cuyo archivo se borró a mano
            }
            catch { return new List<SkinEntry>(); }
        }

        private static void Save(List<SkinEntry> all) =>
            AtomicFile.WriteAllText(IndexFile, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }
}
