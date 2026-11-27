using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Velin.Services
{
    // Idioma de la interfaz de Windows. Velin corre con InvariantGlobalization (ahorra memoria), y en ese modo
    // CultureInfo.CurrentUICulture siempre es la cultura invariante: por eso se le pregunta directo a Windows.
    public static class SystemLanguage
    {
        [DllImport("kernel32.dll")]
        private static extern ushort GetUserDefaultUILanguage();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int LCIDToLocaleName(uint locale, StringBuilder name, int cchName, uint flags);

        // Nombre de cultura del idioma de Windows (por ejemplo "es-AR"), o null si no se pudo leer.
        public static string? GetUiCultureName()
        {
            try
            {
                var sb = new StringBuilder(85);
                return LCIDToLocaleName(GetUserDefaultUILanguage(), sb, sb.Capacity, 0) > 0 ? sb.ToString() : null;
            }
            catch { return null; }
        }
    }
}
