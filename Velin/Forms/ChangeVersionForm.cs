using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Cambiar la versión de Minecraft y/o el mod loader de una instancia que ya existe.
    // Sólo elige los valores nuevos: quien la abre se encarga de aplicarlos (InstanceManager.ChangeVersion).
    public class ChangeVersionForm : VelinFormBase
    {
        private readonly Instance _instance;
        private readonly ComboBox _mcVersion = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly CheckBox _showSnapshots = new();
        private readonly CheckBox _showLegacy = new();
        private readonly ComboBox _loader = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _loaderVersion = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Label _status = new();
        private readonly Button _ok = new();
        private readonly Dictionary<(string loader, string mc), List<string>> _cache = new();

        // Se usan una sola vez, para dejar elegido lo que la instancia ya tiene.
        private string? _initialLoader;
        private string? _initialLoaderVersion;
        private int _refreshToken;

        public string NewMcVersion { get; private set; } = "";
        public ModLoaderType NewLoader { get; private set; } = ModLoaderType.Vanilla;
        public string? NewLoaderVersion { get; private set; }

        public ChangeVersionForm(Instance instance) : base(Localization.T("cv_title", instance.Name))
        {
            _instance = instance;
            _initialLoader = instance.Loader.ToString();
            _initialLoaderVersion = instance.LoaderVersion;
            LockSize(440, 470);
            StartPosition = FormStartPosition.CenterParent;

            int y = 12;
            var current = new Label { Text = Localization.T("cv_current", instance.McVersion, instance.Loader, instance.LoaderVersion ?? "-"), AutoSize = false };
            current.SetBounds(12, y, 410, 34);
            Content.Controls.Add(current);
            y += 40;

            Content.Controls.Add(Lbl(Localization.T("lbl_mc_version"), y)); y += 22;
            _mcVersion.SetBounds(12, y, 410, 24);
            Content.Controls.Add(_mcVersion);
            y += 30;

            _showSnapshots.Text = Localization.T("chk_snapshots");
            _showSnapshots.AutoSize = true;
            _showSnapshots.Location = new Point(12, y);
            Content.Controls.Add(_showSnapshots);
            _showLegacy.Text = Localization.T("chk_legacy");
            _showLegacy.AutoSize = true;
            _showLegacy.Location = new Point(_showSnapshots.PreferredSize.Width + 30, y);
            Content.Controls.Add(_showLegacy);
            _showSnapshots.CheckedChanged += async (s, e) => await LoadVersionsAsync();
            _showLegacy.CheckedChanged += async (s, e) => await LoadVersionsAsync();
            y += 30;

            Content.Controls.Add(Lbl(Localization.T("lbl_loader"), y)); y += 22;
            _loader.SetBounds(12, y, 410, 24);
            Content.Controls.Add(_loader);
            _loader.SelectedIndexChanged += async (s, e) => await LoadLoaderVersionsAsync();
            y += 32;

            Content.Controls.Add(Lbl(Localization.T("lbl_loader_version"), y)); y += 22;
            _loaderVersion.Enabled = false;
            _loaderVersion.SetBounds(12, y, 410, 24);
            Content.Controls.Add(_loaderVersion);
            y += 34;

            var warn = new Label { Text = Localization.T("cv_warning"), AutoSize = false };
            warn.Font = new Font("Segoe UI", 7.5f, FontStyle.Italic);
            warn.SetBounds(12, y, 410, 60);
            Content.Controls.Add(warn);
            y += 64;

            _status.AutoSize = false;
            _status.SetBounds(12, y, 410, 22);
            Content.Controls.Add(_status);
            y += 26;

            _ok.Text = Localization.T("btn_apply");
            _ok.SetBounds(228, y, 90, 32);
            _ok.Click += OnOk;
            Content.Controls.Add(_ok);
            var cancel = new Button { Text = Localization.T("btn_cancel"), DialogResult = DialogResult.Cancel };
            cancel.SetBounds(324, y, 98, 32);
            Content.Controls.Add(cancel);
            CancelButton = cancel;

            _mcVersion.SelectedIndexChanged += async (s, e) => await RefreshCompatibleLoadersAsync();
            Load += async (s, e) => await LoadVersionsAsync();

            Theme.Apply(this);
        }

        private static Label Lbl(string text, int y)
        {
            var l = new Label { Text = text, AutoSize = true };
            l.SetBounds(12, y, 410, 18);
            return l;
        }

        private async Task LoadVersionsAsync()
        {
            _status.Text = Localization.T("status_loading_versions");
            try
            {
                var wanted = (_mcVersion.SelectedItem as string) ?? _instance.McVersion;
                var manifest = await MinecraftDownloader.GetManifestAsync();
                var ids = manifest.Versions.Where(v =>
                    v.Type == "release"
                    || (_showSnapshots.Checked && v.Type == "snapshot")
                    || (_showLegacy.Checked && (v.Type == "old_beta" || v.Type == "old_alpha"))
                ).Select(v => v.Id).ToList();
                // La versión actual siempre tiene que estar en la lista, aunque sea un snapshot o una beta.
                if (!ids.Contains(_instance.McVersion)) ids.Insert(0, _instance.McVersion);

                _mcVersion.Items.Clear();
                foreach (var id in ids) _mcVersion.Items.Add(id);
                var idx = ids.IndexOf(wanted);
                _mcVersion.SelectedIndex = idx >= 0 ? idx : 0;
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
            if (_cache.TryGetValue(key, out var cached)) return cached;
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
            catch { list = new List<string>(); }
            _cache[key] = list;
            return list;
        }

        private async Task RefreshCompatibleLoadersAsync()
        {
            if (_mcVersion.SelectedItem == null) return;
            var mc = (string)_mcVersion.SelectedItem;
            var token = ++_refreshToken;
            _status.Text = Localization.T("status_checking_loaders");

            var names = new[] { "Fabric", "Forge", "Quilt", "NeoForge" };
            var results = await Task.WhenAll(names.Select(n => GetVersionsCachedAsync(n, mc)));
            if (token != _refreshToken) return;

            _loader.Items.Clear();
            _loader.Items.Add("Vanilla");
            for (int i = 0; i < names.Length; i++)
                if (results[i].Count > 0) _loader.Items.Add(names[i]);

            var wantedLoader = _initialLoader;
            _initialLoader = null;
            var li = wantedLoader != null ? _loader.Items.IndexOf(wantedLoader) : -1;
            _loader.SelectedIndex = li >= 0 ? li : 0;
            _status.Text = "";
        }

        private async Task LoadLoaderVersionsAsync()
        {
            _loaderVersion.Items.Clear();
            if (_mcVersion.SelectedItem == null || _loader.SelectedItem == null) return;
            var mc = (string)_mcVersion.SelectedItem;
            var loaderName = (string)_loader.SelectedItem;

            if (loaderName == "Vanilla") { _loaderVersion.Enabled = false; return; }

            _loaderVersion.Enabled = true;
            var versions = await GetVersionsCachedAsync(loaderName, mc);
            // Si mientras esperábamos el usuario cambió de loader o de versión, esta respuesta quedó vieja.
            if ((string?)_loader.SelectedItem != loaderName || (string?)_mcVersion.SelectedItem != mc) return;

            _loaderVersion.Items.Clear();
            foreach (var v in versions) _loaderVersion.Items.Add(v);

            // Por defecto queda elegida la MÁS NUEVA (la lista viene ordenada de más nueva a más vieja);
            // sólo la primera vez se respeta la versión que ya tenía la instancia.
            var wanted = _initialLoaderVersion;
            _initialLoaderVersion = null;
            var vi = wanted != null ? _loaderVersion.Items.IndexOf(wanted) : -1;
            if (_loaderVersion.Items.Count > 0) _loaderVersion.SelectedIndex = vi >= 0 ? vi : 0;
            _status.Text = versions.Count == 0 ? Localization.T("status_no_loader_versions") : "";
        }

        private void OnOk(object? sender, EventArgs e)
        {
            if (_mcVersion.SelectedItem == null || _loader.SelectedItem == null)
            {
                MessageBox.Show(this, Localization.T("err_need_mc_version"));
                return;
            }
            var loader = Enum.Parse<ModLoaderType>((string)_loader.SelectedItem);
            if (loader != ModLoaderType.Vanilla && _loaderVersion.SelectedItem == null)
            {
                MessageBox.Show(this, Localization.T("err_need_loader_version"));
                return;
            }

            NewMcVersion = (string)_mcVersion.SelectedItem;
            NewLoader = loader;
            NewLoaderVersion = loader == ModLoaderType.Vanilla ? null : (string)_loaderVersion.SelectedItem!;
            DialogResult = DialogResult.OK;
        }
    }
}
