using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;

namespace Velin.UI
{
    public static class Theme
    {
        // Windows 10/11 le dibuja a ciertos controles (ListBox, ListView) un resplandor
        // celeste/azul al pasar el mouse o hacer foco, como parte del tema visual del
        // sistema — no tiene nada que ver con nuestros colores. Esto le dice a Windows
        // que no aplique ese tema a ese control puntual.
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

        // Registra las fuentes propias también en GDI (para el proceso, no se instalan en Windows).
        // Los controles de WinForms dibujan el texto con GDI (TextRenderer); sin esto, GDI no "ve" las
        // fuentes cargadas sólo en GDI+ y las reemplaza o las dibuja sin suavizado (se ve pixelado).
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern int AddFontResourceEx(string lpszFilename, uint fl, IntPtr pdv);
        private const uint FR_PRIVATE = 0x10;

        private static void DisableVisualStyleGlow(Control c)
        {
            try
            {
                if (!c.IsHandleCreated) c.CreateControl();
                if (c.IsHandleCreated) SetWindowTheme(c.Handle, "", "");
            }
            catch { /* si falla, se queda con el estilo nativo nomás */ }
        }

        public static ThemeDefinition Current = new ThemeDefinition();

        // Fuentes propias (MedievalSharp / Cinzel), cargadas una sola vez.
        private static readonly PrivateFontCollection _fonts = new();
        public static FontFamily TitleFamily { get; private set; } = SystemFonts.DefaultFont.FontFamily;
        public static FontFamily BodyFamily { get; private set; } = SystemFonts.DefaultFont.FontFamily;

        static Theme()
        {
            try
            {
                var dir = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets");
                var medieval = System.IO.Path.Combine(dir, "MedievalSharp.ttf");
                var cinzel = System.IO.Path.Combine(dir, "Cinzel.ttf");
                if (System.IO.File.Exists(medieval)) { try { AddFontResourceEx(medieval, FR_PRIVATE, IntPtr.Zero); } catch { } }
                if (System.IO.File.Exists(cinzel)) { try { AddFontResourceEx(cinzel, FR_PRIVATE, IntPtr.Zero); } catch { } }
                if (System.IO.File.Exists(medieval)) { _fonts.AddFontFile(medieval); TitleFamily = _fonts.Families[_fonts.Families.Length - 1]; }
                if (System.IO.File.Exists(cinzel)) { _fonts.AddFontFile(cinzel); BodyFamily = _fonts.Families[_fonts.Families.Length - 1]; }
            }
            catch { /* si faltan las fuentes, se usa la del sistema sin romper nada */ }
        }

        public static Color BgDark => C(Current.BgDark);
        public static Color BgLight => C(Current.BgLight);
        public static Color ControlBg => C(Current.ControlBg);
        public static Color ControlText => C(Current.ControlText);
        public static Color InputBg => C(Current.InputBg);
        public static Color InputText => C(Current.InputText);
        public static Color Accent => C(Current.Accent);
        public static Color ButtonTextOnAccent => C(Current.ButtonTextOnAccent);
        public static Color TitleBarBg => C(Current.TitleBarBg);
        public static Color TitleBarText => C(Current.TitleBarText);
        public static Color ProgressBarColor => C(Current.ProgressBarColor);
        public static Color ButtonGradientTop => C(Current.ButtonGradientTop);
        public static Color ButtonGradientBottom => C(Current.ButtonGradientBottom);

        private static Color C(string hex)
        {
            try { return ColorTranslator.FromHtml(hex); } catch { return Color.Gray; }
        }

        public static Font UiFont(float size, FontStyle style = FontStyle.Regular)
        {
            if (!Current.UseMedievalFont)
                return new Font(SystemFonts.DefaultFont.FontFamily, size, style);

            // Las fuentes medievales no tienen cursiva de verdad (Windows la "inventa" y se ve dentada) y
            // pierden nitidez cuando son muy chicas. Por eso: el texto chico o en cursiva (avisos, ayudas)
            // usa Segoe UI, y el resto usa la fuente propia con un tamaño mínimo legible.
            if (size < 8.2f || (style & FontStyle.Italic) != 0)
                return new Font("Segoe UI", size, style);
            return new Font(BodyFamily, Math.Max(size, 9f), style);
        }

        public static Font TitleFont(float size, FontStyle style = FontStyle.Regular)
            => new Font(Current.UseMedievalFont ? TitleFamily : SystemFonts.DefaultFont.FontFamily, size, style);

        public static void PaintBackground(Graphics g, Rectangle rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return;
            using var brush = new LinearGradientBrush(rect, BgDark, BgLight, 45f)
            {
                Blend = new Blend { Factors = new float[] { 0f, 0.5f, 1f }, Positions = new float[] { 0f, 0.5f, 1f } }
            };
            g.FillRectangle(brush, rect);
        }

        public static void Apply(Control root)
        {
            root.ForeColor = ControlText;
            ApplyRecursive(root);
        }

        private static void ApplyRecursive(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (!(c.Tag is string tag && tag == "titlebar"))
                {
                    switch (c)
                    {
                        case Button b:
                            StyleButton(b);
                            break;
                        case Label l:
                            l.BackColor = Color.Transparent;
                            l.ForeColor = ControlText;
                            l.Font = UiFont(l.Font.Size, l.Font.Style);
                            break;
                        case CheckBox chk:
                            chk.BackColor = Color.Transparent;
                            chk.ForeColor = ControlText;
                            chk.Font = UiFont(chk.Font.Size, chk.Font.Style);
                            break;
                        case ListBox lb:
                            lb.BackColor = ControlBg;
                            lb.ForeColor = ControlText;
                            lb.BorderStyle = BorderStyle.FixedSingle;
                            lb.DrawMode = DrawMode.OwnerDrawFixed;
                            if (lb.ItemHeight < 18) lb.ItemHeight = 18;
                            lb.DrawItem -= ListBoxDrawItem;
                            lb.DrawItem += ListBoxDrawItem;
                            DisableVisualStyleGlow(lb);
                            break;
                        case ListView lv:
                            lv.BackColor = ControlBg;
                            lv.ForeColor = ControlText;
                            DisableVisualStyleGlow(lv);
                            break;
                        case ComboBox cb:
                            cb.BackColor = InputBg;
                            cb.ForeColor = InputText;
                            cb.FlatStyle = FlatStyle.Flat;
                            cb.DrawMode = DrawMode.OwnerDrawFixed;
                            cb.DrawItem -= ComboBoxDrawItem;
                            cb.DrawItem += ComboBoxDrawItem;
                            break;
                        case TextBox tb:
                            tb.BackColor = InputBg;
                            tb.ForeColor = InputText;
                            tb.BorderStyle = BorderStyle.FixedSingle;
                            break;
                        case NumericUpDown nud:
                            nud.BackColor = InputBg;
                            nud.ForeColor = InputText;
                            break;
                    }
                }
                if (c.HasChildren) ApplyRecursive(c);
            }
        }

        public static void StyleButton(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.ForeColor = ControlText;
            b.Font = UiFont(9.5f, FontStyle.Bold);
            b.FlatAppearance.BorderColor = Accent;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.MouseOverBackColor = Lighten(ButtonGradientTop, 15);
            b.FlatAppearance.MouseDownBackColor = Darken(ButtonGradientBottom, 15);
            b.Cursor = Cursors.Hand;
            b.BackColor = ButtonGradientBottom;

            b.Paint -= ButtonPaintGradient;
            b.Paint += ButtonPaintGradient;
        }

        // Pinta cada botón con un degradé vertical (más notorio en el tema Flash,
        // sutil en Velin), en vez de un color plano.
        private static void ButtonPaintGradient(object? sender, PaintEventArgs e)
        {
            if (sender is not Button b) return;
            var rect = b.ClientRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            using var grad = new LinearGradientBrush(rect, ButtonGradientTop, ButtonGradientBottom, 90f);
            e.Graphics.FillRectangle(grad, rect);

            TextRenderer.DrawText(e.Graphics, b.Text, b.Font, rect, b.ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            using var pen = new Pen(Accent);
            e.Graphics.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);
        }

        private static Color Lighten(Color c, int amount) =>
            Color.FromArgb(c.A, Math.Min(255, c.R + amount), Math.Min(255, c.G + amount), Math.Min(255, c.B + amount));

        private static Color Darken(Color c, int amount) =>
            Color.FromArgb(c.A, Math.Max(0, c.R - amount), Math.Max(0, c.G - amount), Math.Max(0, c.B - amount));

        private static void ListBoxDrawItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not ListBox lb || e.Index < 0) { e.DrawBackground(); return; }
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using var bg = new SolidBrush(selected ? Accent : ControlBg);
            e.Graphics.FillRectangle(bg, e.Bounds);

            var fg = selected ? ButtonTextOnAccent : ControlText;
            var text = lb.GetItemText(lb.Items[e.Index]) ?? "";
            var lines = text.Split('\n');

            if (lines.Length >= 2)
            {
                // "Tarjeta" de dos líneas: título en negrita arriba, detalle más chico abajo.
                var pad = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 3, e.Bounds.Width - 10, e.Bounds.Height - 6);
                var topHalf = new Rectangle(pad.X, pad.Y, pad.Width, pad.Height / 2 + 2);
                var bottomHalf = new Rectangle(pad.X, pad.Y + pad.Height / 2, pad.Width, pad.Height / 2);

                using var boldFont = new Font(lb.Font, FontStyle.Bold);
                using var smallFont = new Font(lb.Font.FontFamily, Math.Max(7.5f, lb.Font.Size - 1.5f));

                TextRenderer.DrawText(e.Graphics, lines[0], boldFont, topHalf, fg,
                    TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(e.Graphics, lines[1], smallFont, bottomHalf, fg,
                    TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }
            else
            {
                TextRenderer.DrawText(e.Graphics, text, lb.Font, e.Bounds, fg,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }
        }

        private static void ComboBoxDrawItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not ComboBox cb || e.Index < 0) { e.DrawBackground(); return; }
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using var bg = new SolidBrush(selected ? Accent : InputBg);
            e.Graphics.FillRectangle(bg, e.Bounds);
            var text = cb.GetItemText(cb.Items[e.Index]);
            TextRenderer.DrawText(e.Graphics, text, cb.Font, e.Bounds, selected ? ButtonTextOnAccent : InputText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }
}
