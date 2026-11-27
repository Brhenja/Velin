using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Velin.Services
{
    // Filtro y orden de la lista de mods instalados (sin pantallas, para poder probarlo).
    public static class ModListView
    {
        public const string Az = "az";
        public const string Recent = "recent";
        public const string Oldest = "oldest";
        public const string EnabledFirst = "enabled";
        public const string DisabledFirst = "disabled";

        // Mismo orden que el desplegable de la ventana de mods.
        public static readonly string[] Modes = { Az, Recent, Oldest, EnabledFirst, DisabledFirst };

        public static string Normalize(string? mode)
        {
            var trimmed = mode?.Trim();
            return Modes.FirstOrDefault(m => string.Equals(m, trimmed, StringComparison.OrdinalIgnoreCase)) ?? Az;
        }

        // Cada palabra de la búsqueda tiene que aparecer en alguno de los textos (sin importar mayúsculas ni tildes).
        public static bool Matches(string? query, params string[] texts)
        {
            var words = (query ?? "").Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return true;
            var compare = CultureInfo.InvariantCulture.CompareInfo;
            return words.All(w => texts.Any(t => compare.IndexOf(t ?? "", w, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0));
        }

        public static List<T> Sort<T>(IEnumerable<T> items, string? mode, Func<T, string> name, Func<T, DateTime> date, Func<T, bool> disabled)
        {
            var comparer = StringComparer.CurrentCultureIgnoreCase;
            IOrderedEnumerable<T> ordered = Normalize(mode) switch
            {
                Recent => items.OrderByDescending(date).ThenBy(name, comparer),
                Oldest => items.OrderBy(date).ThenBy(name, comparer),
                EnabledFirst => items.OrderBy(disabled).ThenBy(name, comparer),
                DisabledFirst => items.OrderByDescending(disabled).ThenBy(name, comparer),
                _ => items.OrderBy(name, comparer)
            };
            return ordered.ToList();
        }
    }
}
