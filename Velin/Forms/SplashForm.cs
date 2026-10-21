using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Media;
using System.Windows.Forms;
using Velin.UI;

namespace Velin.Forms
{
    // Splash de inicio: "Velin" grande + "By Brhenja" abajo, con el mismo fondo
    // minimalista del resto del launcher y una melodía corta de fondo.
    public class SplashForm : Form
    {
        private readonly PrivateFontCollection _fonts = new();
        private Font _titleFont = new Font("Segoe UI", 44f, FontStyle.Bold);
        private Font _subtitleFont = new Font("Segoe UI", 14f, FontStyle.Italic);
        private SoundPlayer? _player;

        // Se pone en true si el usuario hace click en el splash para saltearlo.
        public bool Skipped { get; private set; }

        public SplashForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Width = 520;
            Height = 320;
            DoubleBuffered = true;
            TopMost = true;
            ShowInTaskbar = false;

            Click += (s, e) => Skipped = true;

            LoadFonts();
            PlayMelody();
        }

        private void LoadFonts()
        {
            try
            {
                var titlePath = Path.Combine(AppContext.BaseDirectory, "Assets", "MedievalSharp.ttf");
                var subPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Cinzel.ttf");
                if (File.Exists(titlePath)) _fonts.AddFontFile(titlePath);
                if (File.Exists(subPath)) _fonts.AddFontFile(subPath);

                if (_fonts.Families.Length > 0) _titleFont = new Font(_fonts.Families[0], 54f, FontStyle.Regular);
                if (_fonts.Families.Length > 1) _subtitleFont = new Font(_fonts.Families[1], 16f, FontStyle.Regular);
            }
            catch { /* nos quedamos con las fuentes por defecto ya asignadas */ }
        }

        private void PlayMelody()
        {
            try
            {
                var wavPath = Path.Combine(AppContext.BaseDirectory, "Assets", "velin_theme.wav");
                if (File.Exists(wavPath))
                {
                    _player = new SoundPlayer(wavPath);
                    _player.Play();
                }
            }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var rect = ClientRectangle;
            Theme.PaintBackground(e.Graphics, rect);

            using var borderPen = new Pen(Theme.Accent, 3);
            e.Graphics.DrawRectangle(borderPen, 1, 1, rect.Width - 3, rect.Height - 3);

            e.Graphics.TextRenderingHint = TextRenderingHint.AntiAlias;

            const string title = "Velin";
            const string subtitle = "By Brhenja";

            using var titleBrush = new SolidBrush(Theme.TitleBarText);
            using var shadowBrush = new SolidBrush(Color.FromArgb(120, 0, 0, 0));

            var titleSize = e.Graphics.MeasureString(title, _titleFont);
            var titleX = (rect.Width - titleSize.Width) / 2f;
            var titleY = rect.Height * 0.32f - titleSize.Height / 2f;

            e.Graphics.DrawString(title, _titleFont, shadowBrush, titleX + 3, titleY + 3);
            e.Graphics.DrawString(title, _titleFont, titleBrush, titleX, titleY);

            var subSize = e.Graphics.MeasureString(subtitle, _subtitleFont);
            var subX = (rect.Width - subSize.Width) / 2f;
            var subY = titleY + titleSize.Height + 10;

            e.Graphics.DrawString(subtitle, _subtitleFont, shadowBrush, subX + 2, subY + 2);
            e.Graphics.DrawString(subtitle, _subtitleFont, titleBrush, subX, subY);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            try { _player?.Stop(); _player?.Dispose(); } catch { }
            try { _fonts.Dispose(); } catch { }
        }
    }
}
