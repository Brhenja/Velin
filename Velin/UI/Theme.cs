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
            if (!UseMedieval)
                return MakeFont(CjkFamily ?? SystemFonts.DefaultFont.FontFamily, size, style);

            // Las fuentes medievales no tienen cursiva de verdad (Windows la "inventa" y se ve dentada) y
            // pierden nitidez cuando son muy chicas. Por eso: el texto chico o en cursiva (avisos, ayudas)
            // usa Segoe UI, y el resto usa la fuente propia con un tamaño mínimo legible.
            if (size < 8.2f || (style & FontStyle.Italic) != 0)
                return new Font("Segoe UI", size, style);
            return new Font(BodyFamily, Math.Max(size, 9f), style);
        }

        public static Font TitleFont(float size, FontStyle style = FontStyle.Regular)
            => MakeFont(UseMedieval ? TitleFamily : (CjkFamily ?? SystemFonts.DefaultFont.FontFamily), size, style);

        // Fuente normal (Segoe UI) para textos chicos, ayudas y cursivas. En coreano, japonés y chino usa una fuente que tenga esos caracteres.
        public static Font PlainFont(float size, FontStyle style = FontStyle.Regular)
        {
            var cjk = CjkFamily;
            return cjk != null ? MakeFont(cjk, size, style) : new Font("Segoe UI", size, style);
        }

        // Familia para coreano / japonés / chino (null si el idioma actual no la necesita o no hay ninguna instalada). Se resuelve una vez por idioma.
        private static string? _cjkLang;
        private static FontFamily? _cjkFamily;
        private static FontFamily? CjkFamily
        {
            get
            {
                var lang = Localization.Lang;
                if (_cjkLang != lang)
                {
                    _cjkLang = lang;
                    _cjkFamily = null;
                    foreach (var name in LanguageCatalog.CjkFontNames(lang))
                    {
                        try { _cjkFamily = new FontFamily(name); break; }
                        catch (ArgumentException) { /* esa fuente no está instalada: se prueba la siguiente */ }
                    }
                }
                return _cjkFamily;
            }
        }

        // Algunas fuentes no traen cursiva/negrita: si el estilo no existe se usa el normal en vez de romper la ventana.
        private static Font MakeFont(FontFamily family, float size, FontStyle style)
        {
            try { return new Font(family, size, style); }
            catch (ArgumentException) { return new Font(family, size, FontStyle.Regular); }
        }

        // Las fuentes medievales sólo tienen alfabeto latino básico: en ruso, chino, japonés, coreano, turco, polaco... se usa la fuente normal.
        private static bool UseMedieval => Current.UseMedievalFont && !Localization.UsesSystemFont;

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
                        case RichTextBox rtb:
                            rtb.BackColor = InputBg;
                            rtb.ForeColor = InputText;
                            rtb.BorderStyle = BorderStyle.FixedSingle;
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

            var top = ButtonGradientTop;
            var bottom = ButtonGradientBottom;
            var text = b.ForeColor;
            var border = Accent;

            if (!b.Enabled)
            {
                // Deshabilitado: se ve apagado (antes se veía igual que uno activo y parecía "trabado").
                text = Mix(ControlText, bottom, 0.55);
                border = Mix(Accent, bottom, 0.6);
            }
            else if (b.ClientRectangle.Contains(b.PointToClient(Control.MousePosition)))
            {
                // Hover: se aclara en temas oscuros y se oscurece en temas claros.
                top = IsLight(top) ? Darken(top, 14) : Lighten(top, 16);
                bottom = IsLight(bottom) ? Darken(bottom, 14) : Lighten(bottom, 16);
            }

            using var grad = new LinearGradientBrush(rect, top, bottom, 90f);
            e.Graphics.FillRectangle(grad, rect);

            TextRenderer.DrawText(e.Graphics, b.Text, b.Font, rect, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            using var pen = new Pen(border);
            e.Graphics.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);
        }

        private static bool IsLight(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) > 150;

        private static Color Mix(Color a, Color b, double t) =>
            Color.FromArgb(255, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

        // Menús contextuales y desplegables con los colores del tema (antes salían siempre en gris claro de Windows).
        public static void StyleMenu(ToolStrip menu)
        {
            menu.Renderer = new ToolStripProfessionalRenderer(new ThemedColors());
            menu.BackColor = ControlBg;
            menu.ForeColor = ControlText;
            menu.Font = UiFont(9f);
            foreach (ToolStripItem item in menu.Items)
            {
                item.ForeColor = ControlText;
                item.BackColor = ControlBg;
            }
        }

        private sealed class ThemedColors : ProfessionalColorTable
        {
            public ThemedColors() { UseSystemColors = false; }
            public override Color ToolStripDropDownBackground => ControlBg;
            public override Color ImageMarginGradientBegin => ControlBg;
            public override Color ImageMarginGradientMiddle => ControlBg;
            public override Color ImageMarginGradientEnd => ControlBg;
            public override Color MenuBorder => Accent;
            public override Color MenuItemBorder => Accent;
            public override Color MenuItemSelected => Mix(ControlBg, ControlText, 0.2);
            public override Color MenuItemSelectedGradientBegin => Mix(ControlBg, ControlText, 0.2);
            public override Color MenuItemSelectedGradientEnd => Mix(ControlBg, ControlText, 0.2);
            public override Color MenuItemPressedGradientBegin => Mix(ControlBg, ControlText, 0.2);
            public override Color MenuItemPressedGradientEnd => Mix(ControlBg, ControlText, 0.2);
            public override Color SeparatorDark => Mix(ControlText, ControlBg, 0.7);
            public override Color SeparatorLight => ControlBg;
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
