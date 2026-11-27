using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Velin.UI
{
    public enum IconKind { None, Play, Puzzle, Chest, Plus, Minus, Gear, Folder, Head }

    // Botón del tema que dibuja un ícono (a la izquierda del texto, o solo si no tiene texto). Lo pinta Theme.ButtonPaintGradient.
    public class IconButton : Button
    {
        private IconKind _icon;

        public IconKind Icon
        {
            get => _icon;
            set { _icon = value; Invalidate(); }
        }
    }

    // Íconos dibujados por código (vectoriales): no hay imágenes ni fuentes que cargar, y toman el color del texto del botón.
    public static class Icons
    {
        public static void Draw(Graphics g, IconKind kind, Rectangle bounds, Color color)
        {
            if (kind == IconKind.None || bounds.Width < 6 || bounds.Height < 6) return;

            var oldSmoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                float s = Math.Min(bounds.Width, bounds.Height);
                float x0 = bounds.X + (bounds.Width - s) / 2f;
                float y0 = bounds.Y + (bounds.Height - s) / 2f;
                float pw = Math.Max(1.6f, s / 11f);

                using var pen = new Pen(color, pw) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                using var brush = new SolidBrush(color);

                PointF P(float u, float v) => new PointF(x0 + u * s, y0 + v * s);
                RectangleF R(float u, float v, float w, float h) => new RectangleF(x0 + u * s, y0 + v * s, w * s, h * s);

                switch (kind)
                {
                    case IconKind.Plus:
                        g.DrawLine(pen, P(0.18f, 0.5f), P(0.82f, 0.5f));
                        g.DrawLine(pen, P(0.5f, 0.18f), P(0.5f, 0.82f));
                        break;

                    case IconKind.Minus:
                        g.DrawLine(pen, P(0.18f, 0.5f), P(0.82f, 0.5f));
                        break;

                    case IconKind.Play:
                        g.FillPolygon(brush, new[] { P(0.26f, 0.14f), P(0.88f, 0.5f), P(0.26f, 0.86f) });
                        break;

                    case IconKind.Gear:
                    {
                        // Aro con 8 dientes cortos y un hueco en el centro.
                        using var toothPen = new Pen(color, pw * 1.5f) { StartCap = LineCap.Flat, EndCap = LineCap.Flat };
                        for (int i = 0; i < 8; i++)
                        {
                            double a = i * Math.PI / 4;
                            var from = P(0.5f + (float)Math.Cos(a) * 0.33f, 0.5f + (float)Math.Sin(a) * 0.33f);
                            var to = P(0.5f + (float)Math.Cos(a) * 0.47f, 0.5f + (float)Math.Sin(a) * 0.47f);
                            g.DrawLine(toothPen, from, to);
                        }
                        var ring = R(0.2f, 0.2f, 0.6f, 0.6f);
                        g.DrawEllipse(pen, ring.X, ring.Y, ring.Width, ring.Height);
                        var hole = R(0.39f, 0.39f, 0.22f, 0.22f);
                        g.DrawEllipse(pen, hole.X, hole.Y, hole.Width, hole.Height);
                        break;
                    }

                    case IconKind.Puzzle:
                    {
                        // Pieza de rompecabezas: cuerpo cuadrado con una perilla arriba y otra a la derecha (Winding = se unen sin huecos).
                        using var path = new GraphicsPath(FillMode.Winding);
                        path.AddRectangle(R(0.14f, 0.30f, 0.60f, 0.60f));
                        path.AddEllipse(R(0.31f, 0.09f, 0.26f, 0.26f));
                        path.AddEllipse(R(0.67f, 0.47f, 0.26f, 0.26f));
                        g.FillPath(brush, path);
                        break;
                    }

                    case IconKind.Folder:
                    {
                        // Carpeta: solapa arriba a la izquierda y cuerpo.
                        using var path = new GraphicsPath();
                        path.AddLines(new[] { P(0.10f, 0.78f), P(0.10f, 0.24f), P(0.40f, 0.24f), P(0.50f, 0.36f), P(0.90f, 0.36f), P(0.90f, 0.78f) });
                        path.CloseFigure();
                        g.DrawPath(pen, path);
                        break;
                    }

                    case IconKind.Head:
                    {
                        // Cabeza de Minecraft simple: cara blanca con borde del color del texto, ojos y boca negros (píxeles nítidos).
                        g.SmoothingMode = SmoothingMode.None;
                        var face = R(0.12f, 0.12f, 0.76f, 0.76f);
                        g.FillRectangle(Brushes.White, face);
                        using var edge = new Pen(color, Math.Max(1f, s / 14f));
                        g.DrawRectangle(edge, face.X, face.Y, face.Width, face.Height);
                        g.FillRectangle(Brushes.Black, R(0.27f, 0.36f, 0.15f, 0.15f));
                        g.FillRectangle(Brushes.Black, R(0.58f, 0.36f, 0.15f, 0.15f));
                        g.FillRectangle(Brushes.Black, R(0.33f, 0.62f, 0.34f, 0.11f));
                        break;
                    }

                    case IconKind.Chest:
                    {
                        // Cofre: tapa, cuerpo y cerradura.
                        var lid = R(0.12f, 0.20f, 0.76f, 0.26f);
                        var body = R(0.12f, 0.46f, 0.76f, 0.38f);
                        g.DrawRectangle(pen, lid.X, lid.Y, lid.Width, lid.Height);
                        g.DrawRectangle(pen, body.X, body.Y, body.Width, body.Height);
                        var lockRect = R(0.43f, 0.38f, 0.14f, 0.20f);
                        g.FillRectangle(brush, lockRect);
                        break;
                    }
                }
            }
            finally { g.SmoothingMode = oldSmoothing; }
        }
    }
}
