using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velin.Models;
using Velin.UI;

namespace Velin.Forms
{
    // Capturas de pantalla de la instancia (carpeta "screenshots"): miniaturas, abrir, copiar, eliminar.
    // Sólo se cargan las 100 más recientes, para cuidar la memoria.
    public class ScreenshotsForm : VelinFormBase
    {
        private const int MaxShown = 100;

        private readonly Instance _instance;
        private readonly ListView _list = new() { View = View.LargeIcon, MultiSelect = false };
        private readonly ImageList _images = new() { ImageSize = new Size(160, 90), ColorDepth = ColorDepth.Depth32Bit };
        private readonly Label _status = new();
        private string Folder => Path.Combine(_instance.GameDir, "screenshots");

        public ScreenshotsForm(Instance instance) : base(Localization.T("ss_title", instance.Name))
        {
            _instance = instance;
            LockSize(760, 500);
            StartPosition = FormStartPosition.CenterParent;

            _list.LargeImageList = _images;
            _list.SetBounds(12, 12, 730, 390);
            _list.ItemActivate += (s, e) => OpenSelected();
            Content.Controls.Add(_list);

            int x = 12;
            x = AddButton("ss_open", x, 110, OpenSelected);
            x = AddButton("ss_copy", x, 150, CopySelected);
            x = AddButton("ss_delete", x, 110, DeleteSelected);
            x = AddButton("ss_open_folder", x, 160, () =>
            {
                try
                {
                    Directory.CreateDirectory(Folder);
                    Process.Start(new ProcessStartInfo { FileName = Folder, UseShellExecute = true });
                }
                catch { }
            });
            var close = new Button { Text = Localization.T("btn_close"), DialogResult = DialogResult.OK };
            close.SetBounds(632, 412, 110, 32);
            Content.Controls.Add(close);
            AcceptButton = close;

            _status.AutoSize = false;
            _status.SetBounds(12, 454, 730, 22);
            Content.Controls.Add(_status);

            Load += async (s, e) => await LoadAsync();
            FormClosed += (s, e) => { _list.Items.Clear(); _images.Dispose(); };
            Theme.Apply(this);
        }

        private int AddButton(string key, int x, int width, Action onClick)
        {
            var b = new Button { Text = Localization.T(key) };
            b.SetBounds(x, 412, width, 32);
            b.Click += (s, e) => onClick();
            Content.Controls.Add(b);
            return x + width + 6;
        }

        private async Task LoadAsync()
        {
            _status.Text = Localization.T("ss_loading");
            var files = new List<string>();
            try
            {
                if (Directory.Exists(Folder))
                    files = new DirectoryInfo(Folder).GetFiles("*.png")
                        .OrderByDescending(f => f.LastWriteTime).Take(MaxShown).Select(f => f.FullName).ToList();
            }
            catch { }

            var thumbs = await Task.Run(() => files.Select(f => (path: f, bmp: MakeThumb(f))).ToList());
            if (IsDisposed) { foreach (var t in thumbs) t.bmp?.Dispose(); return; }

            _list.BeginUpdate();
            foreach (var (path, bmp) in thumbs)
            {
                if (bmp == null) continue;
                _images.Images.Add(bmp);
                bmp.Dispose();
                var item = new ListViewItem(File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm"), _images.Images.Count - 1) { Tag = path };
                _list.Items.Add(item);
            }
            _list.EndUpdate();
            _status.Text = files.Count == 0 ? Localization.T("ss_empty") : Localization.T("ss_count", _list.Items.Count);
        }

        private static Bitmap? MakeThumb(string path)
        {
            try
            {
                using var img = Image.FromFile(path);
                var bmp = new Bitmap(160, 90);
                using var g = Graphics.FromImage(bmp);
                g.Clear(Color.Black);
                float scale = Math.Min(160f / img.Width, 90f / img.Height);
                int w = Math.Max(1, (int)(img.Width * scale)), h = Math.Max(1, (int)(img.Height * scale));
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.DrawImage(img, (160 - w) / 2, (90 - h) / 2, w, h);
                return bmp;
            }
            catch { return null; }
        }

        private string? SelectedPath => _list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as string : null;

        private void OpenSelected()
        {
            var p = SelectedPath; if (p == null) return;
            try { Process.Start(new ProcessStartInfo { FileName = p, UseShellExecute = true }); } catch { }
        }

        private void CopySelected()
        {
            var p = SelectedPath; if (p == null) return;
            try
            {
                using var img = Image.FromFile(p);
                Clipboard.SetImage(new Bitmap(img));
                _status.Text = Localization.T("ss_copied");
            }
            catch (Exception ex) { _status.Text = ex.Message; }
        }

        private void DeleteSelected()
        {
            var p = SelectedPath; if (p == null) return;
            if (MessageBox.Show(this, Localization.T("ss_confirm_delete", Path.GetFileName(p)), "Velin", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                File.Delete(p);
                _list.Items.Remove(_list.SelectedItems[0]);
                _status.Text = Localization.T("ss_count", _list.Items.Count);
            }
            catch (Exception ex) { _status.Text = ex.Message; }
        }
    }
}
