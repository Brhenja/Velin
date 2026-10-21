using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    public class ModBrowserForm : VelinFormBase
    {
        private readonly Instance _instance;
        private readonly ComboBox _source = new();
        private Label _hint = null!;

        private readonly TextBox _search = new();
        private readonly Button _btnSearch = new();
        private readonly ListBox _results = new() { ItemHeight = 34 };
        private readonly Button _btnInstall = new();

        private readonly ListBox _installed = new() { ItemHeight = 34 };
        private readonly Button _btnRemove = new();
        private readonly Button _btnToggle = new();
        private readonly Button _btnVault = new();

        private readonly Label _status = new();

        public ModBrowserForm(Instance instance)
            : base(Localization.T("mods_title", instance.Name, instance.McVersion, instance.Loader.ToString()))
        {
            _instance = instance;
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
            Content.Controls.Add(_results);

            _btnInstall.Text = Localization.T("btn_install_arrow");
            _btnInstall.SetBounds(12, 442, 240, 32);
            _btnInstall.Click += async (s, e) => await InstallAsync();
            Content.Controls.Add(_btnInstall);

            Content.Controls.Add(new Label { Text = Localization.T("lbl_installed_mods"), AutoSize = true, Location = new Point(438, 50) });

            _installed.SetBounds(438, 72, 340, 360);
            Content.Controls.Add(_installed);

            _btnRemove.Text = Localization.T("btn_remove_arrow");
            _btnRemove.SetBounds(438, 442, 90, 32);
            _btnRemove.Click += (s, e) => RemoveSelected();
            Content.Controls.Add(_btnRemove);

            _btnToggle.Text = Localization.T("btn_toggle_mod");
            _btnToggle.SetBounds(534, 442, 126, 32);
            _btnToggle.Click += (s, e) => ToggleSelected();
            Content.Controls.Add(_btnToggle);

            _btnVault.Text = Localization.T("btn_mods_vault");
            _btnVault.SetBounds(666, 442, 112, 32);
            _btnVault.Click += (s, e) => OpenModsVault();
            Content.Controls.Add(_btnVault);

            _status.SetBounds(12, 482, 766, 20);
            Content.Controls.Add(_status);

            Load += (s, e) => RefreshInstalled();
            Theme.Apply(this);
        }

        private class InstalledItem
        {
            public readonly InstalledMod Mod;
            public readonly bool Disabled;
            public InstalledItem(InstalledMod m, bool disabled) { Mod = m; Disabled = disabled; }
            public override string ToString() =>
                $"{(Disabled ? Localization.T("mod_off") + " " : "")}{Mod.ProjectTitle}\n{Mod.Filename} · {ContentUi.SourceName(ContentSources.ParseSource(Mod.Source))}";
        }

        // Un .jar que está en la carpeta de mods pero que vos pusiste a mano (no vino
        // de Modrinth) — se puede ver y sacar desde acá igual, simplemente no tiene
        // chequeo de actualizaciones porque no sabemos de dónde salió.
        private class LocalModItem
        {
            public readonly string Path;
            public LocalModItem(string path) { Path = path; }
            public override string ToString() =>
                $"{(Path.EndsWith(ModManager.DisabledSuffix, StringComparison.OrdinalIgnoreCase) ? Localization.T("mod_off") + " " : "")}{System.IO.Path.GetFileName(Path)}\n({Localization.T("local_mod")})";
        }

        private void RefreshInstalled()
        {
            _installed.Items.Clear();

            var tracked = ModManager.LoadIndex(_instance).Mods;
            var trackedFilenames = new System.Collections.Generic.HashSet<string>(
                tracked.Select(m => m.Filename), StringComparer.OrdinalIgnoreCase);

            foreach (var m in tracked)
                _installed.Items.Add(new InstalledItem(m, ModManager.IsDisabled(_instance, m.Filename)));

            try
            {
                Directory.CreateDirectory(_instance.ModsDir);
                var modFiles = Directory.GetFiles(_instance.ModsDir).Where(f =>
                    f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
                    || f.EndsWith(".jar" + ModManager.DisabledSuffix, StringComparison.OrdinalIgnoreCase));
                foreach (var path in modFiles.OrderBy(Path.GetFileName))
                {
                    var name = Path.GetFileName(path);
                    if (name.EndsWith(ModManager.DisabledSuffix, StringComparison.OrdinalIgnoreCase))
                        name = name.Substring(0, name.Length - ModManager.DisabledSuffix.Length);
                    if (!trackedFilenames.Contains(name))
                        _installed.Items.Add(new LocalModItem(path));
                }
            }
            catch { }
        }

        private void OpenModsVault()
        {
            using var form = new VaultForm(_instance.ModsDir, Localization.T("btn_mods_vault") + " — " + _instance.Name);
            form.ShowDialog(this);
            RefreshInstalled();
        }

        private async System.Threading.Tasks.Task SearchAsync()
        {
            _status.Text = Localization.T("btn_search") + "...";
            _results.Items.Clear();
            try
            {
                var source = ContentUi.SourceOf(_source);
                var hits = await ContentSources.SearchAsync(source, ContentType.Mod, _search.Text, _instance);
                if (source != ContentUi.SourceOf(_source)) return; // cambió de fuente mientras buscaba
                foreach (var h in hits) _results.Items.Add(h);
                _status.Text = Localization.T("results_count", hits.Count);
            }
            catch (Exception ex)
            {
                _status.Text = "Error: " + ex.Message;
            }
        }

        private async System.Threading.Tasks.Task InstallAsync()
        {
            if (_results.SelectedItem is not ContentProject item) return;
            _status.Text = Localization.T("installing", item.Title);
            _btnInstall.Enabled = false;
            try
            {
                // Instala el mod y también sus dependencias obligatorias (ej: Fabric API).
                var result = await ModManager.InstallModWithDependenciesAsync(_instance, item.Source,
                    item.Id, item.Title, msg => _status.Text = msg);
                RefreshInstalled();

                int extra = Math.Max(0, result.Installed.Count - 1);
                _status.Text = extra > 0
                    ? Localization.T("deps_installed", item.Title, extra)
                    : Localization.T("installed", item.Title);

                if (result.Warnings.Count > 0)
                    MessageBox.Show(this, Localization.T("deps_warning_title") + "\n\n- " + string.Join("\n- ", result.Warnings),
                        "Velin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localization.T("install_failed", ex.Message), "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _status.Text = "";
            }
            finally
            {
                _btnInstall.Enabled = true;
            }
        }

        // Activa o desactiva el mod elegido (renombra el .jar a .jar.disabled y viceversa).
        private void ToggleSelected()
        {
            var index = _installed.SelectedIndex;
            try
            {
                if (_installed.SelectedItem is InstalledItem item)
                {
                    var path = Path.Combine(_instance.ModsDir, item.Mod.Filename);
                    if (File.Exists(path)) ModManager.ToggleModFile(path);
                    else if (File.Exists(path + ModManager.DisabledSuffix)) ModManager.ToggleModFile(path + ModManager.DisabledSuffix);
                }
                else if (_installed.SelectedItem is LocalModItem local)
                {
                    ModManager.ToggleModFile(local.Path);
                }
                else return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshInstalled();
            if (index >= 0 && index < _installed.Items.Count) _installed.SelectedIndex = index;
        }

        private void RemoveSelected()
        {
            if (_installed.SelectedItem is InstalledItem item)
            {
                ModManager.RemoveMod(_instance, item.Mod);
                RefreshInstalled();
            }
            else if (_installed.SelectedItem is LocalModItem local)
            {
                try { File.Delete(local.Path); } catch { }
                RefreshInstalled();
            }
        }
    }
}
