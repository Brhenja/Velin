using System;
using System.Linq;

namespace Velin.Services
{
    // Fuentes de texto que se pueden elegir en Configuración. Lista corta y segura: las ventanas tienen tamaño fijo,
    // así que se evitan las fuentes muy anchas (Verdana, Georgia). Si una no está instalada, simplemente no se ofrece.
    public static class FontChoices
    {
        public static readonly string[] Safe =
        {
            "Segoe UI", "Calibri", "Arial", "Tahoma", "Trebuchet MS", "Cambria", "Consolas"
        };

        // Opción especial: fuente medieval propia de Velin (MedievalSharp / Cinzel), aunque el tema elegido no la use.
        public const string Medieval = "Medieval";

        // Devuelve el nombre canónico de la lista (o "Medieval"), o "" (fuente predeterminada de Velin) si no es una de las permitidas.
        public static string Normalize(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            var trimmed = name.Trim();
            if (string.Equals(trimmed, Medieval, StringComparison.OrdinalIgnoreCase)) return Medieval;
            var match = Safe.FirstOrDefault(f => string.Equals(f, trimmed, StringComparison.OrdinalIgnoreCase));
            return match ?? "";
        }
    }

    // Estilo de la ventana principal: "new" (botones con íconos, +/−/engranaje sobre la lista) o "legacy" (la disposición clásica).
    public static class UiStyles
    {
        public const string Legacy = "legacy";
        public const string New = "new";

        public static string Normalize(string? value) =>
            string.Equals(value?.Trim(), Legacy, StringComparison.OrdinalIgnoreCase) ? Legacy : New;

        public static bool IsNew(string? value) => Normalize(value) == New;
    }
}
