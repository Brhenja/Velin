using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Explorador de modpacks de Modrinth o CurseForge (misma interfaz): se busca, se ELIGE LA VERSIÓN del modpack
    // y se instala como una instancia nueva.
    public class ModpackBrowserForm : VelinFormBase
    {
        private readonly ComboBox _source = new();
        private Label _hint = null!;
        private readonly TextBox _search = new();
        private readonly Button _btnSearch = new();
        private readonly ListBox _results = new() { ItemHeight = 34 };
        private readonly Label _title = new();
        private readonly Label _desc = new();
        private readonly ComboBox _versions = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Button _install = new();
        private readonly VelinProgressBar _progress = new() { Visible = false };
        private readonly Label _status = new();
        private int _versionsToken;

        public string ImportedName { get; private set; } = "";

        public ModpackBrowserForm() : base(Localization.T("mp_title"))
        {
            LockSize(800, 545);
            StartPosition = FormStartPosition.CenterParent;

            _hint = ContentUi.AddSearchBar(Content, _source, _search, _btnSearch, lockToModrinth: false);
            _btnSearch.Click += async (s, e) => await SearchAsync();
            AcceptButton = _btnSearch;
            _source.SelectedIndexChanged += async (s, e) =>
            {
                _hint.Text = ContentUi.HintText(ContentUi.SourceOf(_source));
                await SearchAsync();
            };

            _results.SetBounds(12, 72, 410, 360);
            _results.SelectedIndexChanged += async (s, e) => await OnSelectedAsync();
            Content.Controls.Add(_results);

            _title.AutoSize = false;
            _title.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _title.SetBounds(438, 50, 340, 28);
            Content.Controls.Add(_title);

            _desc.AutoSize = false;
            _desc.SetBounds(438, 82, 340, 130);
            Content.Controls.Add(_desc);

            Content.Controls.Add(new Label { Text = Localization.T("mp_version"), AutoSize = true, Location = new Point(438, 222) });
            _versions.SetBounds(438, 244, 340, 24);
            _versions.Enabled = false;
            Content.Controls.Add(_versions);

            var note = new Label { Text = Localization.T("mp_note"), AutoSize = false };
            note.Font = new Font("Segoe UI", 7.5f, FontStyle.Italic);
            note.SetBounds(438, 278, 340, 70);
            Content.Controls.Add(note);

            _install.Text = Localization.T("mp_install");
            _install.SetBounds(12, 442, 410, 32);
            _install.Enabled = false;
            _install.Click += async (s, e) => await InstallAsync();
            Content.Controls.Add(_install);

            _progress.SetBounds(438, 448, 340, 18);
            Content.Controls.Add(_progress);
            _status.SetBounds(12, 482, 766, 20);
            Content.Controls.Add(_status);

            Load += async (s, e) => await SearchAsync();
            Theme.Apply(this);
        }

        private class VersionItem
        {
            public readonly ContentVersion Version;
            public VersionItem(ContentVersion v) { Version = v; }
            public override string ToString() => Version.ToString();
        }

        private async System.Threading.Tasks.Task SearchAsync()
        {
            _status.Text = Localization.T("btn_search") + "...";
            _results.Items.Clear();
            ClearDetails();
            var source = ContentUi.SourceOf(_source);
            try
            {
                var hits = await ContentSources.SearchAsync(source, ContentType.ModPack, _search.Text, null);
                if (source != ContentUi.SourceOf(_source)) return; // cambió de fuente mientras buscaba
                foreach (var h in hits) _results.Items.Add(h);
                _status.Text = Localization.T("results_count", hits.Count);
            }
            catch (Exception ex) { _status.Text = "Error: " + ex.Message; }
        }

        private void ClearDetails()
        {
            _versionsToken++;
            _title.Text = ""; _desc.Text = "";
            _versions.Items.Clear(); _versions.Enabled = false; _install.Enabled = false;
        }

        private async System.Threading.Tasks.Task OnSelectedAsync()
        {
            if (_results.SelectedItem is not ContentProject item) return;
            var token = ++_versionsToken;
            _title.Text = item.Title;
            _desc.Text = item.Description;
            _versions.Items.Clear();
            _versions.Enabled = false;
            _install.Enabled = false;
            try
            {
                var versions = await ContentSources.GetVersionsAsync(item.Source, ContentType.ModPack, item.Id, null);
                if (token != _versionsToken) return; // el usuario ya eligió otro modpack
                foreach (var v in versions.Take(40)) _versions.Items.Add(new VersionItem(v));
                if (_versions.Items.Count > 0)
                {
                    _versions.SelectedIndex = 0;   // la más nueva queda elegida; se puede cambiar
                    _versions.Enabled = true;
                    _install.Enabled = true;
                }
                else _status.Text = Localization.T("mp_no_file");
            }
            catch (Exception ex) { _status.Text = "Error: " + ex.Message; }
        }

        private async System.Threading.Tasks.Task InstallAsync()
        {
            if (_versions.SelectedItem is not VersionItem vi || _results.SelectedItem is not ContentProject item) return;

            _install.Enabled = false; _versions.Enabled = false; _results.Enabled = false; _btnSearch.Enabled = false; _source.Enabled = false;
            _progress.Visible = true; _progress.Value = 0;
            var warnings = new List<string>();
            try
            {
                _status.Text = Localization.T("installing", item.Title);
                var progress = new Progress<string>(m => _status.Text = m);
                var percent = new Progress<double>(p => _progress.Value = Math.Max(0, Math.Min(100, (int)(p * 100))));
                var instance = await ContentSources.InstallModpackAsync(vi.Version, progress, percent, w => warnings.Add(w));
                ImportedName = instance.Name;

                MessageBox.Show(this, Localization.T("import_done", instance.Name), "Velin");
                foreach (var w in warnings)
                    MessageBox.Show(this, w, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localization.T("error_operation") + "\n\n" + ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _status.Text = "";
            }
            finally
            {
                if (!IsDisposed)
                {
                    _progress.Visible = false; _progress.Value = 0;
                    _install.Enabled = true; _versions.Enabled = true; _results.Enabled = true; _btnSearch.Enabled = true; _source.Enabled = true;
                }
            }
        }
    }
}
