using System;

namespace Velin.Services
{
    // Cuentas del escalado por DPI (sin WinForms, para poder probarlas).
    public static class DpiMath
    {
        // Escala un valor en píxeles. Redondea hacia afuera en los .5 (37,5 -> 38) para que el resultado no dependa de nada más.
        public static int Scale(int value, float ratio) => (int)Math.Round(value * (double)ratio, MidpointRounding.AwayFromZero);

        public static int Scale(int value, int oldDpi, int newDpi) => oldDpi <= 0 || newDpi <= 0 ? value : Scale(value, newDpi / (float)oldDpi);

        // Cuando una ventana se agranda, la escala se limita para que siga entrando en la pantalla (área de trabajo), pero nunca
        // baja de 1. Cuando se achica (por ejemplo al pasarla a un monitor de menos DPI) se respeta tal cual.
        public static float FitRatio(float desired, int width, int height, int areaWidth, int areaHeight)
        {
            if (desired <= 1f || width <= 0 || height <= 0) return desired;
            var fit = Math.Min(areaWidth / (float)width, areaHeight / (float)height);
            return Math.Max(1f, Math.Min(desired, fit));
        }
    }
}
