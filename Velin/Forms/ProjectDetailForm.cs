using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Detalles de un mod / modpack / resource pack / shader: ícono, descripción, galería y enlaces oficiales.
    // La descripción se dibuja con un RichTextBox nativo (sin WebView ni navegador embebido).
    public class ProjectDetailForm : VelinFormBase
    {
        private readonly ContentSource _source;
        private readonly string _projectId;
        private readonly string _author;
        private readonly CancellationTokenSource _cts = new();

        private readonly PictureBox _icon = new() { SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
        private readonly Label _title = new() { AutoSize = false };
        private readonly Label _meta = new() { AutoSize = false };
        private readonly FlowLayoutPanel _links = new() { WrapContents = false, AutoScroll = false, FlowDirection = FlowDirection.LeftToRight };
        private readonly PictureBox _gallery = new() { SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand };
        private readonly Button _prev = new() { Text = "◀" };
        private readonly Button _next = new() { Text = "▶" };
        private readonly Label _counter = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };
        private readonly RichTextBox _body = new() { ReadOnly = true, ScrollBars = RichTextBoxScrollBars.Vertical, DetectUrls = true, WordWrap = true };

        private ProjectDetails? _details;
        private int _galleryIndex;
        private readonly Dictionary<int, Image?> _galleryImages = new();

        public ProjectDetailForm(ContentSource source, string projectId, string title, string author = "") : base(title)
        {
            _source = source;
            _projectId = projectId;
            _author = author;
            LockSize(720, 600);
            StartPosition = FormStartPosition.CenterParent;

            _icon.SetBounds(12, 12, 64, 64);
            Content.Controls.Add(_icon);

            _title.Text = title;
            _title.Font = Theme.UiFont(14f, FontStyle.Bold);
            _title.SetBounds(86, 10, 620, 28);
            Content.Controls.Add(_title);

            _meta.Text = Localization.T("detail_loading");
            _meta.SetBounds(86, 42, 620, 36);
            Content.Controls.Add(_meta);

            _links.SetBounds(12, 84, 696, 30);
            Content.Controls.Add(_links);

            _gallery.SetBounds(12, 122, 696, 190);
            _gallery.Click += async (s, e) => await OpenViewerAsync();
            _gallery.Visible = false;
            Content.Controls.Add(_gallery);

            _prev.SetBounds(12, 316, 60, 26);
            _prev.Click += async (s, e) => await ShowGalleryAsync(_galleryIndex - 1);
            _next.SetBounds(648, 316, 60, 26);
            _next.Click += async (s, e) => await ShowGalleryAsync(_galleryIndex + 1);
            _counter.SetBounds(78, 316, 564, 26);
            foreach (Control c in new Control[] { _prev, _next, _counter }) { c.Visible = false; Content.Controls.Add(c); }

            _body.Font = Theme.UiFont(9.5f);
            _body.SetBounds(12, 122, 696, 426);
            _body.LinkClicked += (s, e) => OpenUrl(e.LinkText);
            Content.Controls.Add(_body);

            var close = new Button { Text = Localization.T("btn_close") };
            close.SetBounds(588, 556, 120, 32);
            close.Click += (s, e) => Close();
            Content.Controls.Add(close);

            Theme.Apply(this);
            _title.Font = Theme.UiFont(14f, FontStyle.Bold);

            Shown += async (s, e) => await LoadAsync();
            FormClosed += (s, e) =>
            {
                _cts.Cancel();
                _cts.Dispose();
                _icon.Image?.Dispose();
                _gallery.Image = null;
                foreach (var img in _galleryImages.Values) img?.Dispose();
                _galleryImages.Clear();
            };
        }

        private async Task LoadAsync()
        {
            var ct = _cts.Token;
            try
            {
                var details = await ProjectDetailsService.GetAsync(_source, _projectId, _author);
                if (IsDisposed || ct.IsCancellationRequested) return;
                _details = details;

                if (details.Title.Length > 0) { _title.Text = details.Title; Text = details.Title; }
                var parts = new List<string>();
                if (details.Author.Length > 0) parts.Add(Localization.T("detail_by", details.Author));
                parts.Add(Localization.T("detail_downloads", details.Downloads.ToString("N0", CultureInfo.CurrentCulture)));
                parts.Add(ContentUi.SourceName(details.Source));
                _meta.Text = string.Join("  ·  ", parts) + (details.Summary.Length > 0 ? "\n" + details.Summary : "");

                BuildLinks(details);

                var text = details.Body.Length > 0 ? details.Body : details.Summary;
                ContentTextRenderer.Render(_body, text.Length > 0 ? text : Localization.T("detail_no_description"), details.BodyIsHtml);

                if (details.Gallery.Count > 0)
                {
                    _gallery.Visible = true;
                    foreach (Control c in new Control[] { _prev, _next, _counter }) c.Visible = true;
                    _body.SetBounds(12, 348, 696, 200);
                    await ShowGalleryAsync(0);
                }

                // Ícono: se prueba la URL principal y, si Windows no la puede mostrar (WebP/SVG), el original.
                // Si no hay forma, se dibuja una letra en vez de dejar un cuadro negro.
                Image? icon = null;
                if (details.IconUrl.Length > 0)
                    icon = await ImageCache.LoadAsync(details.IconUrl, details.FallbackIconUrl, ct);
                if (IsDisposed) { icon?.Dispose(); return; }
                _icon.Image = icon ?? MakePlaceholder(details.Title.Length > 0 ? details.Title : _title.Text);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                _meta.Text = Localization.T("detail_error");
                _body.Clear();
                _body.Text = ex.Message;
            }
        }

        private void BuildLinks(ProjectDetails d)
        {
            _links.Controls.Clear();
            foreach (var link in d.Links)
            {
                if (_links.Controls.Count >= 5) break;
                // El ancho se ajusta al texto ("Reportar problemas" se cortaba con un ancho fijo).
                var width = Math.Max(90, TextRenderer.MeasureText(link.Label, Theme.UiFont(9f)).Width + 28);
                var b = new Button { Text = link.Label, Width = width, Height = 26, Margin = new Padding(0, 0, 6, 0), Tag = link.Url };
                b.Click += (s, e) => OpenUrl((string)((Button)s!).Tag!);
                _links.Controls.Add(b);
            }
            Theme.Apply(_links);
        }

        private async Task ShowGalleryAsync(int index)
        {
            if (_details == null || _details.Gallery.Count == 0) return;
            var count = _details.Gallery.Count;
            _galleryIndex = ((index % count) + count) % count;
            var item = _details.Gallery[_galleryIndex];
            var caption = item.Title.Length > 0 ? $"{_galleryIndex + 1} / {count}  —  {item.Title}" : $"{_galleryIndex + 1} / {count}";
            _counter.Text = caption + "  ·  " + Localization.T("detail_click_enlarge");

            if (!_galleryImages.TryGetValue(_galleryIndex, out var img))
            {
                _gallery.Image = null;
                var wanted = _galleryIndex;
                try { img = await ImageCache.LoadAsync(item.Url, item.FallbackUrl, _cts.Token); }
                catch (OperationCanceledException) { return; }
                if (IsDisposed) { img?.Dispose(); return; }
                _galleryImages[wanted] = img;
                if (wanted != _galleryIndex) return; // mientras cargaba, se pasó a otra imagen
            }
            _gallery.Image = img;
        }

        // Clic en la galería: visor con la imagen completa (sin recortes) y navegación.
        private async Task OpenViewerAsync()
        {
            if (_details == null || _details.Gallery.Count == 0) return;
            using var viewer = new ImageViewerForm(_details.Gallery, _galleryIndex);
            viewer.ShowDialog(this);
            if (!IsDisposed) await ShowGalleryAsync(viewer.CurrentIndex);
        }

        // Cuadro con la inicial del proyecto (cuando el ícono no se pudo cargar).
        private static Bitmap MakePlaceholder(string title)
        {
            var bmp = new Bitmap(64, 64);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Theme.ControlBg);
            var letter = (title.Trim().Length > 0 ? title.Trim().Substring(0, 1) : "?").ToUpperInvariant();
            using var font = Theme.UiFont(26f, FontStyle.Bold);
            TextRenderer.DrawText(g, letter, font, new Rectangle(0, 0, 64, 64), Theme.ControlText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return bmp;
        }

        private static void OpenUrl(string url)
        {
            if (!ProjectDetailsService.IsSafeUrl(url)) return;
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); } catch { }
        }
    }
}
