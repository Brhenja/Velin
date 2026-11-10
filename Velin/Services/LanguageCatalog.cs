using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Velin.Services
{
    public class LanguageInfo
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }

    // Idiomas de la interfaz: un archivo JSON por idioma ({"clave": "texto"}).
    //   Assets\Lang\<codigo>.json   -> los que trae Velin
    //   data\lang\<codigo>.json     -> los del usuario: agregan idiomas nuevos o pisan textos de uno existente
    // El archivo trae su nombre en la clave "lang.name" (por ejemplo "Português (Brasil)").
    public static class LanguageCatalog
    {
        public const string FallbackCode = "en";
        public const string NameKey = "lang.name";

        // Idiomas cuyo alfabeto cubren las fuentes medievales (MedievalSharp / Cinzel). El resto usa la fuente normal de Windows.
        private static readonly string[] LatinSafe = { "es", "en", "pt-BR", "de", "fr", "it" };

        public static string BuiltInDir => Path.Combine(PathService.BaseDir, "Assets", "Lang");
        public static string UserDir => Path.Combine(PathService.DataDir, "lang");

        public static bool NeedsSystemFont(string code) => !LatinSafe.Contains(code, StringComparer.OrdinalIgnoreCase);

        // Fuentes de Windows que sí tienen los caracteres de esos idiomas (la fuente por defecto no siempre los resuelve y salen cuadraditos).
        // Vacío para el resto: les alcanza con la fuente normal. Se usa la primera que esté instalada.
        public static string[] CjkFontNames(string code)
        {
            var two = code.Split('-')[0].ToLowerInvariant();
            return two switch
            {
                "ko" => new[] { "Malgun Gothic" },
                "ja" => new[] { "Yu Gothic UI", "Meiryo UI", "MS UI Gothic" },
                "zh" => new[] { "Microsoft YaHei UI", "Microsoft JhengHei UI", "SimSun" },
                _ => Array.Empty<string>()
            };
        }

        // Lista para el selector de idioma: español e inglés primero, después el resto por nombre.
        public static List<LanguageInfo> Available(string? builtInDir = null, string? userDir = null)
        {
            var found = new Dictionary<string, LanguageInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var dir in new[] { builtInDir ?? BuiltInDir, userDir ?? UserDir })
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
                {
                    var code = Path.GetFileNameWithoutExtension(file);
                    if (code.Length == 0 || code.Length > 16) continue;
                    var strings = ReadFile(file);
                    if (strings == null) continue;
                    var name = strings.TryGetValue(NameKey, out var n) && n.Length > 0 ? n : code;
                    found[code] = new LanguageInfo { Code = code, Name = name };
                }
            }
            int Rank(LanguageInfo l) => l.Code == "es" ? 0 : l.Code == "en" ? 1 : 2;
            return found.Values.OrderBy(Rank).ThenBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        // Textos de un idioma: los de Assets\Lang y, encima, los de data\lang (si los hay). Vacío si no existe.
        public static Dictionary<string, string> LoadStrings(string code, string? builtInDir = null, string? userDir = null)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var dir in new[] { builtInDir ?? BuiltInDir, userDir ?? UserDir })
            {
                var file = Path.Combine(dir, code + ".json");
                var strings = File.Exists(file) ? ReadFile(file) : null;
                if (strings == null) continue;
                foreach (var kv in strings) result[kv.Key] = kv.Value;
            }
            return result;
        }

        // Elige un idioma disponible a partir de la cultura de Windows ("pt-PT" -> pt-BR, "zh-Hans-CN" -> zh-CN, "es-AR" -> es...).
        public static string Detect(CultureInfo culture, IReadOnlyCollection<LanguageInfo> available)
        {
            string Pick(string code) => available.Any(a => a.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) ? code : "";
            var name = culture.Name;
            var two = culture.TwoLetterISOLanguageName;
            string chosen = two switch
            {
                "pt" => Pick("pt-BR"),
                "zh" => Pick("zh-CN"),
                _ => ""
            };
            if (chosen.Length == 0) chosen = Pick(name);
            if (chosen.Length == 0) chosen = Pick(two);
            return chosen.Length > 0 ? chosen : FallbackCode;
        }

        // Lee el JSON tolerando comas finales y comentarios; ignora valores que no sean texto.
        private static Dictionary<string, string>? ReadFile(string path)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var p in doc.RootElement.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.String) map[p.Name] = p.Value.GetString() ?? "";
                return map;
            }
            catch { return null; }
        }
    }
}
