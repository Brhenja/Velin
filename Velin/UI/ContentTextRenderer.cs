using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Velin.UI
{
    // Muestra descripciones de mods (Markdown de Modrinth o HTML de CurseForge) en un RichTextBox NATIVO.
    // Alternativa liviana a WebView2/Chromium: cero dependencias y ~0 MB extra. Soporta títulos, negritas, cursivas,
    // código, listas, citas, tablas simples y enlaces (los links quedan como texto clickeable). Las imágenes de la
    // descripción no se incrustan: se ven en la galería de la ventana.
    public static class ContentTextRenderer
    {
        private const int MaxChars = 60000;
        private const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private readonly record struct Style(bool Bold, bool Italic, bool Mono);

        public static void Render(RichTextBox box, string text, bool isHtml)
        {
            box.Clear();
            box.DetectUrls = true;
            if (string.IsNullOrWhiteSpace(text)) return;
            if (text.Length > MaxChars) text = text.Substring(0, MaxChars) + "\n\n…";

            text = HtmlToMarkdown(text.Replace("\r\n", "\n"));

            var baseFont = box.Font;
            using var bold = new Font(baseFont, FontStyle.Bold);
            using var italic = new Font(baseFont, FontStyle.Italic);
            using var boldItalic = new Font(baseFont, FontStyle.Bold | FontStyle.Italic);
            using var mono = new Font("Consolas", baseFont.Size - 0.5f);
            var fonts = new Fonts(baseFont, bold, italic, boldItalic, mono);

            var dim = Blend(box.ForeColor, box.BackColor, 0.35);

            SendMessage(box.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                bool inCode = false, lastBlank = true;
                foreach (var raw in text.Split('\n'))
                {
                    var line = raw.TrimEnd();

                    if (line.TrimStart().StartsWith("```"))
                    {
                        inCode = !inCode;
                        continue;
                    }
                    if (inCode)
                    {
                        Append(box, line + "\n", fonts.Mono, box.ForeColor, 12);
                        lastBlank = false;
                        continue;
                    }

                    if (line.Trim().Length == 0)
                    {
                        if (!lastBlank) Append(box, "\n", fonts.Base, box.ForeColor, 0);
                        lastBlank = true;
                        continue;
                    }
                    lastBlank = false;

                    var heading = Regex.Match(line, @"^\s{0,3}(#{1,6})\s+(.*?)\s*#*\s*$");
                    if (heading.Success)
                    {
                        float size = heading.Groups[1].Length switch { 1 => 1.5f, 2 => 1.3f, 3 => 1.15f, _ => 1.05f };
                        using var hf = new Font(baseFont.FontFamily, baseFont.Size * size, FontStyle.Bold);
                        Append(box, StripInline(heading.Groups[2].Value) + "\n", hf, box.ForeColor, 0);
                        continue;
                    }

                    if (Regex.IsMatch(line, @"^\s{0,3}([-*_])(\s*\1){2,}\s*$"))
                    {
                        Append(box, new string('─', 40) + "\n", fonts.Base, dim, 0);
                        continue;
                    }

                    if (line.TrimStart().StartsWith("|"))
                    {
                        if (Regex.IsMatch(line, @"^\s*\|?[\s:\-|]+\|?\s*$")) continue; // línea separadora de tabla
                        var cells = line.Trim().Trim('|').Split('|');
                        AppendInline(box, string.Join("   ·   ", Array.ConvertAll(cells, c => c.Trim())) + "\n", fonts, default, box.ForeColor, 0);
                        continue;
                    }

                    var quote = Regex.Match(line, @"^\s{0,3}>\s?(.*)$");
                    if (quote.Success)
                    {
                        AppendInline(box, "▎ " + quote.Groups[1].Value + "\n", fonts, new Style(false, true, false), dim, 8);
                        continue;
                    }

                    var list = Regex.Match(line, @"^(\s*)([-*+]|\d+[.)])\s+(.*)$");
                    if (list.Success)
                    {
                        int indent = 10 + Math.Min(list.Groups[1].Length / 2, 4) * 14;
                        var marker = char.IsDigit(list.Groups[2].Value[0]) ? list.Groups[2].Value : "•";
                        AppendInline(box, marker + " " + list.Groups[3].Value + "\n", fonts, default, box.ForeColor, indent);
                        continue;
                    }

                    AppendInline(box, line.Trim() + "\n", fonts, default, box.ForeColor, 0);
                }
            }
            finally
            {
                SendMessage(box.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                box.SelectionStart = 0;
                box.SelectionLength = 0;
                box.ScrollToCaret();
                box.Invalidate();
            }
        }

        private sealed record Fonts(Font Base, Font Bold, Font Italic, Font BoldItalic, Font Mono);

        private static Font Pick(Fonts f, Style s) =>
            s.Mono ? f.Mono : s.Bold && s.Italic ? f.BoldItalic : s.Bold ? f.Bold : s.Italic ? f.Italic : f.Base;

        private static void Append(RichTextBox box, string text, Font font, Color color, int indent)
        {
            if (text.Length == 0) return;
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;
            box.SelectionIndent = indent;
            box.SelectionFont = font;
            box.SelectionColor = color;
            box.SelectedText = text;
        }

        // Orden de búsqueda: imagen (se omite), enlace, negrita, cursiva, código en línea.
        private static readonly Regex InlinePattern = new(
            @"!\[[^\]]*\]\([^)]*\)" +
            @"|\[(?<ltext>[^\]]+)\]\((?<lurl>[^)\s]+)(?:\s+""[^""]*"")?\)" +
            @"|(?<bm>\*\*|__)(?<btext>.+?)\k<bm>" +
            @"|(?<![\w*])\*(?<itext>[^*\s][^*]*?)\*(?!\w)" +
            @"|(?<!\w)_(?<utext>[^_\s][^_]*?)_(?!\w)" +
            @"|`(?<ctext>[^`]+)`",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static void AppendInline(RichTextBox box, string text, Fonts fonts, Style style, Color color, int indent)
        {
            int pos = 0;
            foreach (Match m in InlinePattern.Matches(text))
            {
                if (m.Index > pos) Append(box, text.Substring(pos, m.Index - pos), Pick(fonts, style), color, indent);
                pos = m.Index + m.Length;

                if (m.Groups["ltext"].Success)
                {
                    var label = StripInline(m.Groups["ltext"].Value);
                    var url = m.Groups["lurl"].Value;
                    // "texto (url)": el RichTextBox detecta la URL y la hace clickeable.
                    var shown = label.Length == 0 || label == url ? url : $"{label} ({url})";
                    Append(box, shown, Pick(fonts, style), color, indent);
                }
                else if (m.Groups["btext"].Success)
                    AppendInline(box, m.Groups["btext"].Value, fonts, style with { Bold = true }, color, indent);
                else if (m.Groups["itext"].Success)
                    AppendInline(box, m.Groups["itext"].Value, fonts, style with { Italic = true }, color, indent);
                else if (m.Groups["utext"].Success)
                    AppendInline(box, m.Groups["utext"].Value, fonts, style with { Italic = true }, color, indent);
                else if (m.Groups["ctext"].Success)
                    Append(box, m.Groups["ctext"].Value, fonts.Mono, color, indent);
                // imágenes: se omiten
            }
            if (pos < text.Length) Append(box, text.Substring(pos), Pick(fonts, style), color, indent);
        }

        // Texto plano de un fragmento con marcas (para títulos y textos de enlaces).
        private static string StripInline(string s) =>
            Regex.Replace(s, @"(\*\*|__|`)|(?<![\w*])\*(?=\S)|(?<=\S)\*(?!\w)", "").Trim();

        // CurseForge entrega HTML y Modrinth, Markdown con algo de HTML adentro: todo se lleva a Markdown simple.
        // Solo se tocan etiquetas HTML reales (<p>, <a href=...>); un autolink como <https://...> se respeta.
        public static string HtmlToMarkdown(string s)
        {
            if (s.IndexOf('<') < 0 && s.IndexOf('&') < 0) return s;

            s = Regex.Replace(s, @"<(script|style)\b[^>]*>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<!--.*?-->", "", RegexOptions.Singleline);

            // Enlaces
            s = Regex.Replace(s, @"<a\b[^>]*?href\s*=\s*[""']([^""']*)[""'][^>]*>(.*?)</a>", m =>
            {
                var inner = Regex.Replace(m.Groups[2].Value, @"</?[a-zA-Z][^>]*>", "").Trim();
                var url = m.Groups[1].Value.Trim();
                if (url.Length == 0 || url.StartsWith("#", StringComparison.Ordinal) || !Services.ProjectDetailsService.IsSafeUrl(url)) return inner;
                return inner.Length == 0 ? url : $"[{inner}]({url})";
            }, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            s = Regex.Replace(s, @"<img\b[^>]*>", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<h([1-6])\b[^>]*>", m => "\n\n" + new string('#', int.Parse(m.Groups[1].Value)) + " ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"</h[1-6]\s*>", "\n", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"</?(strong|b)\b[^>]*>", "**", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"</?(em|i)\b[^>]*>", "*", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"</?code\b[^>]*>", "`", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<pre\b[^>]*>", "\n```\n", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"</pre\s*>", "\n```\n", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<li\b[^>]*>", "\n- ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"</li\s*>", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<(br|hr)\b[^>]*>|</(p|div|tr|ul|ol|table|blockquote)\s*>|<(p|div|tr|ul|ol|table)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"</t[dh]\s*>", " | ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"</?[a-zA-Z][a-zA-Z0-9]*(\s[^>]*)?/?>", "");

            s = WebUtility.HtmlDecode(s);
            s = Regex.Replace(s, @"[ \t]+\n", "\n");
            s = Regex.Replace(s, @"\n{3,}", "\n\n");
            return s.Trim();
        }

        private static Color Blend(Color a, Color b, double t) =>
            Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }
}
