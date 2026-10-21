using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    public class NewInstanceForm : VelinFormBase
    {
        private readonly TextBox _name = new();
        private readonly ComboBox _mcVersion = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly CheckBox _showSnapshots = new() { Text = "" };
        private readonly CheckBox _showLegacy = new() { Text = "" };
        private readonly ComboBox _loader = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _loaderVersion = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly NumericUpDown _ram = new() { Minimum = 512, Maximum = 32768, Increment = 512 };
        private readonly TextBox _username = new();
        private readonly Button _ok = new() { DialogResult = DialogResult.OK };
        private readonly Button _cancel = new() { DialogResult = DialogResult.Cancel };
        private readonly Label _status = new();

        // Evita repetir llamadas de red si el usuario va y viene entre versiones/loaders.
        private readonly Dictionary<(string loader, string mc), List<string>> _versionCache = new();

        public NewInstanceForm() : base(Localization.T("new_instance_title"))
        {
            LockSize(440, 515);
            StartPosition = FormStartPosition.CenterParent;

            var settings = SettingsManager.Load();
            _username.Text = settings.DefaultUsername;
            _ram.Value = settings.DefaultRamMB;

            int y = 12;
            Content.Controls.Add(Lbl(Localization.T("lbl_name"), y)); y += 22;
            _name.SetBounds(12, y, 400, 24); Content.Controls.Add(_name); y += 32;

            Content.Controls.Add(Lbl(Localization.T("lbl_mc_version"), y)); y += 22;
            _mcVersion.SetBounds(12, y, 400, 24); Content.Controls.Add(_mcVersion);
            y += 30;

            _showSnapshots.Text = Localization.T("chk_snapshots");
            _showSnapshots.AutoSize = true;
            _showSnapshots.Location = new System.Drawing.Point(12, y);
            Content.Controls.Add(_showSnapshots);

            _showLegacy.Text = Localization.T("chk_legacy");
            _showLegacy.AutoSize = true;
            _showLegacy.Location = new System.Drawing.Point(_showSnapshots.PreferredSize.Width + 30, y);
            Content.Controls.Add(_showLegacy);

            _showSnapshots.CheckedChanged += async (s, e) => await LoadVersionsAsync();
            _showLegacy.CheckedChanged += async (s, e) => await LoadVersionsAsync();
            y += 30;

            Content.Controls.Add(Lbl(Localization.T("lbl_loader"), y)); y += 22;
            _loader.SetBounds(12, y, 400, 24); Content.Controls.Add(_loader);
            _loader.SelectedIndexChanged += async (s, e) => await LoadLoaderVersionsAsync();
            y += 32;

            Content.Controls.Add(Lbl(Localization.T("lbl_loader_version"), y)); y += 22;
            _loaderVersion.Enabled = false;
            _loaderVersion.SetBounds(12, y, 400, 24); Content.Controls.Add(_loaderVersion);
            y += 32;

            Content.Controls.Add(Lbl(Localization.T("lbl_ram"), y)); y += 22;
            _ram.SetBounds(12, y, 150, 24); Content.Controls.Add(_ram); y += 28;
            var ramHint = new Label { AutoSize = false, Font = new System.Drawing.Font("Segoe UI", 7.5f, System.Drawing.FontStyle.Italic), Text = RamHint.Build((int)_ram.Value) };
            ramHint.SetBounds(12, y, 400, 30); Content.Controls.Add(ramHint);
            _ram.ValueChanged += (s, e) => ramHint.Text = RamHint.Build((int)_ram.Value);
            y += 34;

            Content.Controls.Add(Lbl(Localization.T("lbl_username"), y)); y += 22;
            _username.SetBounds(12, y, 200, 24); Content.Controls.Add(_username); y += 32;

            _status.SetBounds(12, y, 400, 36); Content.Controls.Add(_status); y += 40;

            _ok.Text = Localization.T("btn_create");
            _ok.SetBounds(228, y, 90, 32); Content.Controls.Add(_ok);
            _cancel.Text = Localization.T("btn_cancel");
            _cancel.SetBounds(324, y, 90, 32); Content.Controls.Add(_cancel);

            AcceptButton = _ok;
            CancelButton = _cancel;
            _ok.Click += OnOk;

            _mcVersion.SelectedIndexChanged += async (s, e) => await RefreshCompatibleLoadersAsync();

            Load += async (s, e) => await LoadVersionsAsync();

            Theme.Apply(this);
        }

        private static Label Lbl(string text, int y)
        {
            var l = new Label { Text = text, AutoSize = true };
            l.SetBounds(12, y, 400, 18);
            return l;
        }

        private async Task LoadVersionsAsync()
        {
            _status.Text = Localization.T("status_loading_versions");
            try
            {
                var manifest = await MinecraftDownloader.GetManifestAsync();
                _mcVersion.Items.Clear();
                var filtered = manifest.Versions.Where(v =>
                    v.Type == "release"
                    || (_showSnapshots.Checked && v.Type == "snapshot")
                    || (_showLegacy.Checked && (v.Type == "old_beta" || v.Type == "old_alpha"))
                ).ToList();
                foreach (var v in filtered) _mcVersion.Items.Add(v.Id);
                if (_mcVersion.Items.Count > 0) _mcVersion.SelectedIndex = 0;
                else await RefreshCompatibleLoadersAsync();
                _status.Text = "";
            }
            catch (Exception ex)
            {
                _status.Text = "Error: " + ex.Message;
            }
        }

        private async Task<List<string>> GetVersionsCachedAsync(string loaderName, string mcVersion)
        {
            var key = (loaderName, mcVersion);
            if (_versionCache.TryGetValue(key, out var cached)) return cached;

            List<string> list;
            try
            {
                list = loaderName switch
                {
                    "Fabric" => await LoaderVersionsApi.GetFabricVersionsAsync(mcVersion),
                    "Quilt" => await LoaderVersionsApi.GetQuiltVersionsAsync(mcVersion),
                    "Forge" => await LoaderVersionsApi.GetForgeVersionsAsync(mcVersion),
                    "NeoForge" => await LoaderVersionsApi.GetNeoForgeVersionsAsync(mcVersion),
                    _ => new List<string>()
                };
            }
            catch
            {
                list = new List<string>();
            }
            _versionCache[key] = list;
            return list;
        }

        // Evita que dos llamadas a esta función pisándose entre sí (por ejemplo, el usuario
        // cambiando la versión dos veces rápido) terminen agregando loaders por duplicado.
        private int _loaderRefreshToken = 0;

        // Sólo deja elegir los mod loaders que realmente tienen versiones para la
        // versión de Minecraft seleccionada (ej: en 1.12.2 no van a aparecer Fabric/Quilt/NeoForge).
        private async Task RefreshCompatibleLoadersAsync()
        {
            if (_mcVersion.SelectedItem == null) return;
            var mcVersion = (string)_mcVersion.SelectedItem;
            var myToken = ++_loaderRefreshToken;

            _status.Text = Localization.T("status_checking_loaders");

            var names = new[] { "Fabric", "Forge", "Quilt", "NeoForge" };
            var tasks = names.Select(n => GetVersionsCachedAsync(n, mcVersion)).ToArray();
            var results = await Task.WhenAll(tasks);

            // Si mientras esperábamos la red el usuario ya cambió de versión otra vez,
            // esta respuesta quedó vieja: la descartamos para no pisar la lista actual.
            if (myToken != _loaderRefreshToken) return;

            _loader.Items.Clear();
            _loader.Items.Add("Vanilla");
            for (int i = 0; i < names.Length; i++)
                if (results[i].Count > 0 && !_loader.Items.Contains(names[i]))
                    _loader.Items.Add(names[i]);

            _loader.SelectedIndex = 0;
            _status.Text = "";
            await LoadLoaderVersionsAsync();
        }

        private async Task LoadLoaderVersionsAsync()
        {
            _loaderVersion.Items.Clear();
            if (_mcVersion.SelectedItem == null || _loader.SelectedItem == null) return;

            var mcVersion = (string)_mcVersion.SelectedItem;
            var loaderName = (string)_loader.SelectedItem;

            if (loaderName == "Vanilla")
            {
                _loaderVersion.Enabled = false;
                return;
            }

            _loaderVersion.Enabled = true;
            var versions = await GetVersionsCachedAsync(loaderName, mcVersion);
            foreach (var v in versions) _loaderVersion.Items.Add(v);
            if (_loaderVersion.Items.Count > 0) _loaderVersion.SelectedIndex = 0;
            _status.Text = versions.Count == 0 ? Localization.T("status_no_loader_versions") : "";
        }

        private void OnOk(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_name.Text))
            {
                MessageBox.Show(this, Localization.T("err_need_name"));
                DialogResult = DialogResult.None;
                return;
            }
            var cleanName = _name.Text.Trim();
            if (InstanceManager.Exists(cleanName))
            {
                MessageBox.Show(this, Localization.T("err_name_exists"));
                DialogResult = DialogResult.None;
                return;
            }
            if (_mcVersion.SelectedItem == null)
            {
                MessageBox.Show(this, Localization.T("err_need_mc_version"));
                DialogResult = DialogResult.None;
                return;
            }

            var loaderName = (string)_loader.SelectedItem!;
            var loaderType = loaderName switch
            {
                "Fabric" => ModLoaderType.Fabric,
                "Forge" => ModLoaderType.Forge,
                "Quilt" => ModLoaderType.Quilt,
                "NeoForge" => ModLoaderType.NeoForge,
                _ => ModLoaderType.Vanilla
            };

            if (loaderType != ModLoaderType.Vanilla && _loaderVersion.SelectedItem == null)
            {
                MessageBox.Show(this, Localization.T("err_need_loader_version"));
                DialogResult = DialogResult.None;
                return;
            }

            var instance = new Instance
            {
                Name = cleanName,
                McVersion = (string)_mcVersion.SelectedItem,
                Loader = loaderType,
                LoaderVersion = loaderType == ModLoaderType.Vanilla ? null : (string)_loaderVersion.SelectedItem!,
                RamMB = (int)_ram.Value,
                Username = string.IsNullOrWhiteSpace(_username.Text) ? "Player" : _username.Text.Trim()
            };
            instance.Save();
        }
    }
}
