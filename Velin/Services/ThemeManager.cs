using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Velin.Models;

namespace Velin.Services
{
    // Los temas (incluidos los dos de fábrica, Velin y Flash) son simples archivos .json.
    // Los de fábrica viven en Assets/Themes (se instalan con el launcher); cualquiera puede
    // agregar los suyos poniendo un .json con el mismo formato en data\themes.
    public static class ThemeManager
    {
        public static List<ThemeDefinition> LoadAll()
        {
            var result = new List<ThemeDefinition>();
            LoadFrom(PathService.BuiltInThemesDir, result);
            LoadFrom(PathService.UserThemesDir, result);

            if (result.Count == 0) result.Add(new ThemeDefinition()); // red de seguridad
            return result;
        }

        private static void LoadFrom(string dir, List<ThemeDefinition> into)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (var file in Directory.GetFiles(dir, "*.json"))
                {
                    try
                    {
                        var def = JsonSerializer.Deserialize<ThemeDefinition>(File.ReadAllText(file));
                        if (def != null && !string.IsNullOrWhiteSpace(def.Name)
                            && !into.Any(t => t.Name == def.Name))
                            into.Add(def);
                    }
                    catch { /* archivo de tema inválido: se ignora sin romper nada */ }
                }
            }
            catch { }
        }

        public static ThemeDefinition FindByName(string name)
        {
            var all = LoadAll();
            return all.FirstOrDefault(t => t.Name == name)
                ?? all.FirstOrDefault(t => t.Name.Contains("Velin", StringComparison.OrdinalIgnoreCase))
                ?? all.First();
        }
    }
}
