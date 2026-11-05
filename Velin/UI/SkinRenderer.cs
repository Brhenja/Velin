using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace Velin.UI
{
    // Dibuja el personaje (frente / espalda) y la cara de una skin con System.Drawing, sin 3D ni dependencias.
    // Las skins viejas de 64x32 se convierten primero al formato de 64x64 (las piernas y los brazos izquierdos se copian espejados).
    public static class SkinRenderer
    {
        // Lee un PNG de skin y lo devuelve normalizado a 64x64. null si no se puede leer.
        public static Bitmap? Load(byte[] png)
        {
            try
            {
                using var ms = new MemoryStream(png);
                using var raw = new Bitmap(ms);
                if (raw.Width != 64 || (raw.Height != 64 && raw.Height != 32)) return null;

                var skin = new Bitmap(64, 64);
                using (var g = Graphics.FromImage(skin))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawImage(raw, new Rectangle(0, 0, 64, raw.Height), new Rectangle(0, 0, 64, raw.Height), GraphicsUnit.Pixel);
                }
                if (raw.Height == 32)
                {
                    // Formato antiguo: no hay capa izquierda; se espeja la derecha (sólo caras frontal y trasera, que son las que se dibujan).
                    Mirror(skin, new Rectangle(4, 20, 4, 12), 20, 52);   // pierna: frente
                    Mirror(skin, new Rectangle(12, 20, 4, 12), 28, 52);  // pierna: espalda
                    Mirror(skin, new Rectangle(44, 20, 4, 12), 36, 52);  // brazo: frente
                    Mirror(skin, new Rectangle(52, 20, 4, 12), 44, 52);  // brazo: espalda
                }
                return skin;
            }
            catch { return null; }
        }

        private static void Mirror(Bitmap bmp, Rectangle src, int dx, int dy)
        {
            for (int x = 0; x < src.Width; x++)
                for (int y = 0; y < src.Height; y++)
                    bmp.SetPixel(dx + (src.Width - 1 - x), dy + y, bmp.GetPixel(src.X + x, src.Y + y));
        }

        // Las skins "slim" dejan vacía (transparente) la columna de píxeles x=54..55 del brazo derecho.
        public static bool GuessSlim(Bitmap skin64)
        {
            for (int x = 54; x <= 55; x++)
                for (int y = 20; y < 32; y++)
                    if (skin64.GetPixel(x, y).A != 0) return false;
            return true;
        }

        // Personaje de frente: 16x32 "píxeles de skin", escalado por `scale`.
        public static Bitmap RenderFront(Bitmap skin, bool slim, int scale)
        {
            var bmp = NewCanvas(scale);
            using var g = Prepare(bmp);
            int aw = slim ? 3 : 4;                 // ancho del brazo
            int armOffset = slim ? 1 : 0;          // el brazo slim queda pegado al torso
            // cabeza
            Draw(g, skin, 8, 8, 8, 8, 4, 0, scale);  Draw(g, skin, 40, 8, 8, 8, 4, 0, scale);
            // torso
            Draw(g, skin, 20, 20, 8, 12, 4, 8, scale); Draw(g, skin, 20, 36, 8, 12, 4, 8, scale);
            // brazo derecho del personaje (a la izquierda de quien mira)
            Draw(g, skin, 44, 20, aw, 12, armOffset, 8, scale); Draw(g, skin, 44, 36, aw, 12, armOffset, 8, scale);
            // brazo izquierdo (a la derecha de quien mira)
            Draw(g, skin, 36, 52, aw, 12, 12, 8, scale); Draw(g, skin, 52, 52, aw, 12, 12, 8, scale);
            // piernas
            Draw(g, skin, 4, 20, 4, 12, 4, 20, scale); Draw(g, skin, 4, 36, 4, 12, 4, 20, scale);
            Draw(g, skin, 20, 52, 4, 12, 8, 20, scale); Draw(g, skin, 4, 52, 4, 12, 8, 20, scale);
            return bmp;
        }

        // Personaje de espalda (el brazo izquierdo queda a la izquierda de quien mira).
        public static Bitmap RenderBack(Bitmap skin, bool slim, int scale)
        {
            var bmp = NewCanvas(scale);
            using var g = Prepare(bmp);
            int aw = slim ? 3 : 4;
            int armOffset = slim ? 1 : 0;
            int leftBackX = slim ? 43 : 44;    // espalda del brazo izquierdo
            int leftSleeveX = slim ? 59 : 60;
            int rightBackX = slim ? 51 : 52;   // espalda del brazo derecho
            // cabeza
            Draw(g, skin, 24, 8, 8, 8, 4, 0, scale);  Draw(g, skin, 56, 8, 8, 8, 4, 0, scale);
            // torso
            Draw(g, skin, 32, 20, 8, 12, 4, 8, scale); Draw(g, skin, 32, 36, 8, 12, 4, 8, scale);
            // brazo izquierdo (a la izquierda de quien mira)
            Draw(g, skin, leftBackX, 52, aw, 12, armOffset, 8, scale); Draw(g, skin, leftSleeveX, 52, aw, 12, armOffset, 8, scale);
            // brazo derecho (a la derecha)
            Draw(g, skin, rightBackX, 20, aw, 12, 12, 8, scale); Draw(g, skin, rightBackX, 36, aw, 12, 12, 8, scale);
            // piernas: la izquierda queda a la izquierda de quien mira
            Draw(g, skin, 28, 52, 4, 12, 4, 20, scale); Draw(g, skin, 12, 52, 4, 12, 4, 20, scale);
            Draw(g, skin, 12, 20, 4, 12, 8, 20, scale); Draw(g, skin, 12, 36, 4, 12, 8, 20, scale);
            return bmp;
        }

        // Sólo la cara (con la capa del sombrero), cuadrada de `size` píxeles.
        public static Bitmap RenderFace(Bitmap skin, int size)
        {
            var bmp = new Bitmap(size, size);
            using var g = Prepare(bmp);
            var dest = new Rectangle(0, 0, size, size);
            g.DrawImage(skin, dest, new Rectangle(8, 8, 8, 8), GraphicsUnit.Pixel);
            g.DrawImage(skin, dest, new Rectangle(40, 8, 8, 8), GraphicsUnit.Pixel);
            return bmp;
        }

        private static Bitmap NewCanvas(int scale) => new Bitmap(16 * scale, 32 * scale);

        private static Graphics Prepare(Bitmap bmp)
        {
            var g = Graphics.FromImage(bmp);
            g.InterpolationMode = InterpolationMode.NearestNeighbor; // píxeles nítidos, sin suavizar
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;
            return g;
        }

        private static void Draw(Graphics g, Bitmap skin, int sx, int sy, int sw, int sh, int dx, int dy, int scale) =>
            g.DrawImage(skin, new Rectangle(dx * scale, dy * scale, sw * scale, sh * scale), new Rectangle(sx, sy, sw, sh), GraphicsUnit.Pixel);
    }
}
