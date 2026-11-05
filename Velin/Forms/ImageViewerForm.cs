using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Visor de las imágenes de la galería: se ve la imagen completa (ajustada a la ventana) y se navega con ◀ ▶ o las flechas.
    public class ImageViewerForm : VelinFormBase
    {
        private readonly IReadOnlyList<ProjectImage> _images;
        private readonly CancellationTokenSource _cts = new();
        private readonly PictureBox _picture = new() { SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
        private readonly Label _caption = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };
        private readonly Button _prev = new() { Text = "◀" };
        private readonly Button _next = new() { Text = "▶" };
        private int _loadToken;

        public int CurrentIndex { get; private set; }

        public ImageViewerForm(IReadOnlyList<ProjectImage> images, int startIndex) : base(Localization.T("viewer_title"))
        {
            _images = images;
            CurrentIndex = Math.Clamp(startIndex, 0, Math.Max(0, images.Count - 1));
            LockSize(900, 640);
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;

            _picture.SetBounds(12, 12, 876, 540);
            Content.Controls.Add(_picture);

            _prev.SetBounds(12, 562, 80, 30);
            _prev.Click += async (s, e) => await ShowAsync(CurrentIndex - 1);
            _next.SetBounds(808, 562, 80, 30);
            _next.Click += async (s, e) => await ShowAsync(CurrentIndex + 1);
            _caption.SetBounds(100, 562, 700, 30);
            Content.Controls.Add(_prev);
            Content.Controls.Add(_next);
            Content.Controls.Add(_caption);
            _prev.Enabled = _next.Enabled = images.Count > 1;

            Theme.Apply(this);

            KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) Close();
                else if (e.KeyCode == Keys.Left) await ShowAsync(CurrentIndex - 1);
                else if (e.KeyCode == Keys.Right) await ShowAsync(CurrentIndex + 1);
            };
            Shown += async (s, e) => await ShowAsync(CurrentIndex);
            FormClosed += (s, e) =>
            {
                _cts.Cancel();
                _cts.Dispose();
                var old = _picture.Image;
                _picture.Image = null;
                old?.Dispose();
            };
        }

        private async Task ShowAsync(int index)
        {
            if (_images.Count == 0) return;
            CurrentIndex = ((index % _images.Count) + _images.Count) % _images.Count;
            var item = _images[CurrentIndex];
            var token = ++_loadToken;
            _caption.Text = (item.Title.Length > 0 ? $"{CurrentIndex + 1} / {_images.Count}  —  {item.Title}" : $"{CurrentIndex + 1} / {_images.Count}");

            Image? img;
            try { img = await ImageCache.LoadAsync(item.Url, item.FallbackUrl, _cts.Token); }
            catch (OperationCanceledException) { return; }
            if (IsDisposed || token != _loadToken) { img?.Dispose(); return; } // mientras cargaba se pasó a otra imagen

            var old = _picture.Image;
            _picture.Image = img;
            old?.Dispose();
            if (img == null) _caption.Text += "  ·  " + Localization.T("viewer_unavailable");
        }
    }
}
