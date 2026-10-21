using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Mundos de una instancia: backup de uno solo, duplicar, eliminar, exportar/importar .zip, datapacks.
    public class WorldsForm : VelinFormBase
    {
        private sealed class Row
        {
            public WorldInfo World { get; }
            public Row(WorldInfo w) { World = w; }
            public string Card =>
                World.Name + "\n" +
                (World.SizeBytes < 0 ? Localization.T("info_calculating") : FormatSize(World.SizeBytes)) +
                " · " + World.LastModified.ToString("yyyy-MM-dd HH:mm");
        }

        private readonly Instance _instance;
        private readonly ListBox _list = new();
        private readonly Button _backup = new();
        private readonly Button _duplicate = new();
        private readonly Button _delete = new();
        private readonly Button _datapacks = new();
        private readonly Button _export = new();
        private readonly Button _import = new();
        private readonly Label _status = new();

        public WorldsForm(Instance instance) : base(Localization.T("wl_title", instance.Name))
        {
            _instance = instance;
            LockSize(520, 420);
            StartPosition = FormStartPosition.CenterParent;

            _list.ItemHeight = 38;
            _list.DisplayMember = "Card";
            _list.SetBounds(12, 12, 496, 260);
            _list.SelectedIndexChanged += (s, e) => UpdateButtons();
            Content.Controls.Add(_list);

            Place(_backup, "wl_backup", 12, 282, 110, async () => await BackupAsync());
            Place(_duplicate, "wl_duplicate", 128, 282, 110, async () => await DuplicateAsync());
            Place(_delete, "wl_delete", 244, 282, 110, () => DeleteSelected());
            Place(_datapacks, "wl_datapacks", 360, 282, 148, () => OpenDatapacks());
            Place(_export, "wl_export", 12, 322, 150, async () => await ExportAsync());
            Place(_import, "wl_import", 168, 322, 150, async () => await ImportAsync());
            var open = new Button { Text = Localization.T("wl_open_folder") };
            open.SetBounds(324, 322, 184, 32);
            open.Click += (s, e) =>
            {
                try
                {
                    Directory.CreateDirectory(WorldService.SavesDir(_instance));
                    Process.Start(new ProcessStartInfo { FileName = WorldService.SavesDir(_instance), UseShellExecute = true });
                }
                catch { }
            };
            Content.Controls.Add(open);

            _status.AutoSize = false;
            _status.Font = new Font("Segoe UI", 8f, FontStyle.Italic);
            _status.SetBounds(12, 364, 496, 46);
            Content.Controls.Add(_status);

            Load += (s, e) => Reload();
            Theme.Apply(this);
        }

        private void Place(Button b, string key, int x, int y, int w, Action onClick)
        {
            b.Text = Localization.T(key);
            b.SetBounds(x, y, w, 32);
            b.Click += (s, e) => onClick();
            Content.Controls.Add(b);
        }

        private WorldInfo? Selected => (_list.SelectedItem as Row)?.World;

        private void Reload()
        {
            var selectedName = Selected?.Name;
            var worlds = WorldService.ListWorlds(_instance);
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var w in worlds)
            {
                var idx = _list.Items.Add(new Row(w));
                if (w.Name == selectedName) _list.SelectedIndex = idx;
            }
            _list.EndUpdate();
            _status.Text = worlds.Count == 0 ? Localization.T("wl_empty") : Localization.T("wl_warning");
            UpdateButtons();

            // El tamaño de cada mundo se calcula aparte, para no trabar la ventana.
            Task.Run(() =>
            {
                foreach (var w in worlds)
                {
                    w.SizeBytes = WorldService.DirectorySize(w.Path);
                    try { BeginInvoke(new Action(() => _list.Invalidate())); } catch { return; }
                }
            });
        }

        private void UpdateButtons()
        {
            var has = Selected != null;
            _backup.Enabled = has; _duplicate.Enabled = has; _delete.Enabled = has;
            _datapacks.Enabled = has; _export.Enabled = has;
        }

        private void SetBusy(bool busy)
        {
            foreach (var b in new[] { _backup, _duplicate, _delete, _datapacks, _export, _import }) b.Enabled = !busy;
            _list.Enabled = !busy;
            if (!busy) UpdateButtons();
        }

        private async Task RunAsync(string statusKey, Func<Task> work)
        {
            SetBusy(true);
            _status.Text = Localization.T(statusKey);
            try { await work(); }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localization.T("error_operation") + "\n\n" + ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetBusy(false);
                Reload();
            }
        }

        private async Task BackupAsync()
        {
            var w = Selected; if (w == null) return;
            await RunAsync("status_backup", async () =>
            {
                var file = await WorldService.BackupWorldAsync(_instance, w);
                MessageBox.Show(this, Localization.T("wl_backup_done", file), "Velin");
            });
        }

        private async Task DuplicateAsync()
        {
            var w = Selected; if (w == null) return;
            await RunAsync("status_duplicating", async () => { await WorldService.DuplicateAsync(_instance, w); });
        }

        private void DeleteSelected()
        {
            var w = Selected; if (w == null) return;
            if (MessageBox.Show(this, Localization.T("wl_confirm_delete", w.Name), "Velin", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { WorldService.Delete(w); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            Reload();
        }

        private void OpenDatapacks()
        {
            var w = Selected; if (w == null) return;
            using var form = new ContentBrowserForm(_instance, ContentKind.DataPack, w.Name);
            form.ShowDialog(this);
        }

        private async Task ExportAsync()
        {
            var w = Selected; if (w == null) return;
            using var sfd = new SaveFileDialog { Filter = Localization.T("export_filter"), FileName = w.Name + ".zip" };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;
            var path = sfd.FileName;
            await RunAsync("status_exporting", async () =>
            {
                await WorldService.ExportAsync(w, path);
                MessageBox.Show(this, Localization.T("export_done", path), "Velin");
            });
        }

        private async Task ImportAsync()
        {
            using var ofd = new OpenFileDialog { Filter = "ZIP|*.zip" };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;
            var path = ofd.FileName;
            await RunAsync("status_importing", async () =>
            {
                var name = await WorldService.ImportAsync(_instance, path);
                MessageBox.Show(this, Localization.T("wl_imported", name), "Velin");
            });
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024 * 1024)).ToString("0.0") + " GB";
            if (bytes >= 1024L * 1024) return (bytes / (1024.0 * 1024)).ToString("0.0") + " MB";
            return Math.Max(1, bytes / 1024) + " KB";
        }
    }
}
