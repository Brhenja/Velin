using System.IO;
using System.Text.Json;
using Velin.Models;

namespace Velin.Services
{
    public static class SettingsManager
    {
        private static string FilePath => Path.Combine(PathService.DataDir, "settings.json");

        public static AppSettings Load()
        {
            PathService.EnsureAll();
            if (!File.Exists(FilePath)) return new AppSettings();
            try
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
                if (!loaded.MusicOnDefaultApplied)
                {
                    // Una sola vez: la música vuelve a estar activada por defecto (una versión de desarrollo anterior la había apagado).
                    // Desde ahí, lo que elijas con el interruptor de Configuración se respeta.
                    loaded.MusicEnabled = true;
                    loaded.MusicOnDefaultApplied = true;
                    try { Save(loaded); } catch { }
                }
                return loaded;
            }
            catch
            {
                return new AppSettings();
            }
        }

        public static void Save(AppSettings settings)
        {
            PathService.EnsureAll();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
