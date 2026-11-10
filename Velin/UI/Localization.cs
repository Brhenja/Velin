using System;
using System.Collections.Generic;
using Velin.Services;

namespace Velin.UI
{
    // Textos de la interfaz. Salen de los archivos JSON de Assets\Lang (y data\lang); si a un idioma le falta un texto se usa el
    // inglés, y si tampoco está se muestra la clave.
    public static class Localization
    {
        private static Dictionary<string, string> _current = new();
        private static Dictionary<string, string> _fallback = new();

        public static string Lang { get; private set; } = "es";

        // Alfabetos que las fuentes medievales no cubren (ruso, chino, japonés, coreano, turco, polaco...).
        public static bool UsesSystemFont => LanguageCatalog.NeedsSystemFont(Lang);

        public static void SetLanguage(string? code)
        {
            code = string.IsNullOrWhiteSpace(code) ? "es" : code.Trim();
            _fallback = LanguageCatalog.LoadStrings(LanguageCatalog.FallbackCode);
            var strings = code == LanguageCatalog.FallbackCode ? _fallback : LanguageCatalog.LoadStrings(code);
            if (strings.Count == 0 && code != LanguageCatalog.FallbackCode)
            {
                // Idioma que no existe (por ejemplo se borró su archivo): se vuelve a español, o a inglés si tampoco está.
                code = "es";
                strings = LanguageCatalog.LoadStrings(code);
                if (strings.Count == 0) { code = LanguageCatalog.FallbackCode; strings = _fallback; }
            }
            _current = strings;
            Lang = code;
        }

        public static string T(string key)
        {
            if (_current.TryGetValue(key, out var v)) return v;
            if (_fallback.TryGetValue(key, out v)) return v;
            return key;
        }

        public static string T(string key, params object[] args)
        {
            try { return string.Format(T(key), args); }
            catch (FormatException) { return T(key); }   // una traducción con llaves mal puestas no debe tirar la ventana
        }
    }
}
