using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Copias de seguridad de los mundos de una instancia: crear una ahora, restaurar, eliminar.
    public class BackupsForm : VelinFormBase
    {
        private sealed class Row
        {
            public BackupService.BackupInfo Info { get; }
            public Row(BackupService.BackupInfo info) { Info = info; }
            public string Card =>
                Info.Created.ToString("yyyy-MM-dd HH:mm:ss") + "\n" +
                FormatSize(Info.SizeBytes) + (Info.Reason.Length > 0 ? " · " + ReasonText(Info.Reason) : "");
        }

        private readonly Instance _instance;
        private readonly ListBox _list = new();
        private readonly Button _create = new();
        private readonly Button _restore = new();
        private readonly Button _delete = new();
        private readonly Label _status = new();

        public BackupsForm(Instance instance) : base(Localization.T("bk_title", instance.Name))
        {
            _instance = instance;
            LockSize(460, 400);
            StartPosition = FormStartPosition.CenterParent;

            _list.ItemHeight = 38;
            _list.DisplayMember = "Card";
            _list.SetBounds(12, 12, 430, 230);
            _list.SelectedIndexChanged += (s, e) => UpdateButtons();
            Content.Controls.Add(_list);

            _create.Text = Localization.T("bk_create");
            _create.SetBounds(12, 252, 140, 32);
            _create.Click += async (s, e) => await CreateAsync();
            Content.Controls.Add(_create);

            _restore.Text = Localization.T("bk_restore");
            _restore.SetBounds(158, 252, 140, 32);
            _restore.Click += async (s, e) => await RestoreAsync();
            Content.Controls.Add(_restore);

            _delete.Text = Localization.T("bk_delete");
            _delete.SetBounds(304, 252, 138, 32);
            _delete.Click += (s, e) => DeleteSelected();
            Content.Controls.Add(_delete);

            var openFolder = new Button { Text = Localization.T("bk_open_folder") };
            openFolder.SetBounds(12, 292, 200, 32);
            openFolder.Click += (s, e) =>
            {
                try
                {
                    Directory.CreateDirectory(BackupService.BackupDirFor(_instance));
                    Process.Start(new ProcessStartInfo { FileName = BackupService.BackupDirFor(_instance), UseShellExecute = true });
                }
                catch { }
            };
            Content.Controls.Add(openFolder);

            var close = new Button { Text = Localization.T("btn_close"), DialogResult = DialogResult.OK };
            close.SetBounds(332, 292, 110, 32);
            Content.Controls.Add(close);
            AcceptButton = close;

            _status.AutoSize = false;
            _status.SetBounds(12, 332, 430, 56);
            _status.Font = new Font("Segoe UI", 8f, FontStyle.Italic);
            Content.Controls.Add(_status);

            Reload();
            Theme.Apply(this);
        }

        private BackupService.BackupInfo? Selected => (_list.SelectedItem as Row)?.Info;

        private void Reload()
        {
            var selectedPath = Selected?.Path;
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var b in BackupService.ListBackups(_instance))
            {
                var idx = _list.Items.Add(new Row(b));
                if (b.Path == selectedPath) _list.SelectedIndex = idx;
            }
            _list.EndUpdate();
            _status.Text = _list.Items.Count == 0 ? Localization.T("bk_empty", BackupService.KeepPerInstance) : Localization.T("bk_hint", BackupService.KeepPerInstance);
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            _restore.Enabled = Selected != null;
            _delete.Enabled = Selected != null;
        }

        private void SetBusy(bool busy)
        {
            _create.Enabled = !busy;
            _restore.Enabled = !busy && Selected != null;
            _delete.Enabled = !busy && Selected != null;
            _list.Enabled = !busy;
        }

        private async System.Threading.Tasks.Task CreateAsync()
        {
            SetBusy(true);
            _status.Text = Localization.T("status_backup");
            try
            {
                var file = await BackupService.BackupWorldsAsync(_instance, "manual");
                if (file == null)
                    MessageBox.Show(this, Localization.T("bk_no_worlds"), "Velin");
            }
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

        private async System.Threading.Tasks.Task RestoreAsync()
        {
            var b = Selected;
            if (b == null) return;
            if (MessageBox.Show(this, Localization.T("bk_confirm_restore", b.Created.ToString("yyyy-MM-dd HH:mm:ss")),
                    "Velin", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            SetBusy(true);
            _status.Text = Localization.T("bk_restoring");
            try
            {
                await BackupService.RestoreAsync(_instance, b.Path);
                MessageBox.Show(this, Localization.T("bk_restored"), "Velin");
            }
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

        private void DeleteSelected()
        {
            var b = Selected;
            if (b == null) return;
            if (MessageBox.Show(this, Localization.T("bk_confirm_delete"), "Velin", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { BackupService.Delete(b.Path); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            Reload();
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024 * 1024)).ToString("0.0") + " GB";
            if (bytes >= 1024L * 1024) return (bytes / (1024.0 * 1024)).ToString("0.0") + " MB";
            return Math.Max(1, bytes / 1024) + " KB";
        }

        private static string ReasonText(string reason) => reason.ToLowerInvariant() switch
        {
            "manual" => Localization.T("bk_reason_manual"),
            "mods" => Localization.T("bk_reason_mods"),
            "delete" => Localization.T("bk_reason_delete"),
            "version" => Localization.T("bk_reason_version"),
            "prerestore" => Localization.T("bk_reason_prerestore"),
            _ => reason
        };
    }
}
