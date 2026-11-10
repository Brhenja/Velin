using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Importar instancias de Prism/MultiMC, CurseForge y del launcher oficial de Minecraft.
    public class ImportLaunchersForm : VelinFormBase
    {
        private readonly ListView _list = new() { View = View.Details, CheckBoxes = true, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable };
        private readonly Label _info = new() { AutoSize = false };
        private readonly CheckBox _worlds = new() { AutoSize = false, Checked = true };
        private readonly CheckBox _mods = new() { AutoSize = false, Checked = true };
        private readonly CheckBox _other = new() { AutoSize = false, Checked = true };
        private readonly Button _browse = new();
        private readonly Button _import = new();
        private readonly Button _close = new();
        private readonly Label _status = new() { AutoSize = false };
        private readonly VelinProgressBar _progress = new() { Minimum = 0, Maximum = 100, Visible = false };
        private readonly List<string> _extraFolders = new();
        private readonly CancellationTokenSource _cts = new();

        public string? LastImportedName { get; private set; }

        public ImportLaunchersForm() : base(Localization.T("imp_title"))
        {
            LockSize(660, 500);
            StartPosition = FormStartPosition.CenterParent;

            _info.Text = Localization.T("imp_info");
            _info.SetBounds(12, 10, 636, 38);

            _list.SetBounds(12, 52, 636, 230);
            _list.Columns.Add(Localization.T("imp_col_name"), 250);
            _list.Columns.Add(Localization.T("imp_col_launcher"), 130);
            _list.Columns.Add(Localization.T("imp_col_version"), 230);
            _list.ItemChecked += (s, e) => UpdateButtons();

            _worlds.Text = Localization.T("imp_worlds");
            _worlds.SetBounds(12, 290, 636, 22);
            _mods.Text = Localization.T("imp_mods");
            _mods.SetBounds(12, 314, 636, 22);
            _other.Text = Localization.T("imp_other");
            _other.SetBounds(12, 338, 636, 22);

            _browse.Text = Localization.T("imp_browse");
            _browse.SetBounds(12, 372, 250, 30);
            _browse.Click += (s, e) => BrowseFolder();

            _status.SetBounds(12, 410, 636, 22);
            _progress.SetBounds(12, 434, 636, 14);
            _import.Text = Localization.T("imp_import");
            _import.SetBounds(404, 454, 150, 34);
            _import.Click += async (s, e) => await ImportSelectedAsync();
            _close.Text = Localization.T("btn_close");
            _close.SetBounds(560, 454, 88, 34);
            _close.Click += (s, e) => Close();

            foreach (Control c in new Control[] { _info, _list, _worlds, _mods, _other, _browse, _status, _progress, _import, _close }) Content.Controls.Add(c);
            Theme.Apply(this);

            Shown += async (s, e) => await ScanAsync();
            FormClosing += (s, e) => { try { _cts.Cancel(); } catch { } };
            UpdateButtons();
        }

        private async Task ScanAsync()
        {
            _status.Text = Localization.T("imp_scanning");
            var found = await Task.Run(() => LauncherImporter.DetectAll(_extraFolders));
            if (IsDisposed) return;
            _list.Items.Clear();
            foreach (var c in found)
            {
                var version = c.McVersion + (c.Loader == ModLoaderType.Vanilla ? "" : " · " + c.Loader + (string.IsNullOrEmpty(c.LoaderVersion) ? "" : " " + c.LoaderVersion));
                var item = new ListViewItem(c.Name) { Tag = c };
                item.SubItems.Add(c.Launcher switch { LauncherKind.Prism => "Prism / MultiMC", LauncherKind.CurseForge => "CurseForge", _ => "Minecraft" });
                item.SubItems.Add(version);
                _list.Items.Add(item);
            }
            _status.Text = found.Count == 0 ? Localization.T("imp_none") : Localization.T("imp_found", found.Count);
            UpdateButtons();
        }

        private async void BrowseFolder()
        {
            using var dlg = new FolderBrowserDialog { Description = Localization.T("imp_browse_hint"), UseDescriptionForTitle = true };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _extraFolders.Add(dlg.SelectedPath);
            await ScanAsync();
        }

        private void UpdateButtons() => _import.Enabled = _list.CheckedItems.Count > 0;

        private async Task ImportSelectedAsync()
        {
            var selected = _list.CheckedItems.Cast<ListViewItem>().Select(i => (ImportCandidate)i.Tag!).ToList();
            if (selected.Count == 0) return;
            var options = new ImportOptions { Worlds = _worlds.Checked, Mods = _mods.Checked, Other = _other.Checked };

            _import.Enabled = false; _browse.Enabled = false; _list.Enabled = false; _close.Enabled = false;
            _progress.Visible = true;
            int ok = 0;
            var errors = new List<string>();
            try
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    var c = selected[i];
                    var progress = new Progress<string>(m => { if (!IsDisposed) _status.Text = $"({i + 1}/{selected.Count}) {m}"; });
                    var percent = new Progress<double>(p => { if (!IsDisposed) _progress.Value = Math.Clamp((int)(p * 100), 0, 100); });
                    try
                    {
                        var inst = await LauncherImporter.ImportAsync(c, options, progress, percent, _cts.Token);
                        LastImportedName = inst.Name;
                        ok++;
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex) { errors.Add(c.Name + ": " + ex.Message); }
                }
            }
            finally
            {
                if (!IsDisposed) { _progress.Visible = false; _progress.Value = 0; _list.Enabled = true; _browse.Enabled = true; _close.Enabled = true; UpdateButtons(); }
            }

            _status.Text = Localization.T("imp_done", ok);
            if (errors.Count > 0)
                MessageBox.Show(this, string.Join("\n", errors), "Velin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else if (ok > 0)
                MessageBox.Show(this, Localization.T("imp_done", ok), "Velin", MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (ok > 0) DialogResult = DialogResult.OK;
        }
    }
}
