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
        private readonly Button _btnDetails = new();
        private readonly Button _btnVersion = new();
        private readonly ResultsPager _pager = new(410);
        private ContentFilterBar _filters = null!;

        private readonly ListBox _installed = new() { ItemHeight = 34 };
        private readonly TextBox _filterInstalled = new();
        private readonly ComboBox _sortInstalled = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly System.Collections.Generic.List<ModEntry> _allInstalled = new();
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

            _filters = new ContentFilterBar(ContentType.Mod, _instance) { Location = new Point(12, 70) };
            _filters.Changed += async () => await SearchAsync();
            Content.Controls.Add(_filters);

            _results.SetBounds(12, 100, 410, 302);
            Content.Controls.Add(_results);

            _pager.Location = new Point(12, 406);
            _pager.PageRequested += async page => await SearchAsync(page);
            Content.Controls.Add(_pager);

            _btnInstall.Text = Localization.T("btn_install_arrow");
            _btnInstall.SetBounds(12, 442, 180, 32);
            _btnInstall.Click += async (s, e) => await InstallAsync();
            Content.Controls.Add(_btnInstall);

            // Detalles: descripción, imágenes y enlaces oficiales (también con doble clic en un resultado o en un mod instalado).
            _btnVersion.Text = Localization.T("btn_pick_version");
            _btnVersion.SetBounds(198, 442, 132, 32);
            _btnVersion.Click += async (s, e) => await ChooseVersionAsync();
            Content.Controls.Add(_btnVersion);

            _btnDetails.Text = Localization.T("btn_details");
            _btnDetails.SetBounds(336, 442, 86, 32);
            _btnDetails.Click += (s, e) => ShowDetails();
            Content.Controls.Add(_btnDetails);
            _results.DoubleClick += (s, e) => ShowDetails();
            _installed.DoubleClick += (s, e) => ShowInstalledDetails();

            Content.Controls.Add(new Label { Text = Localization.T("lbl_installed_mods"), AutoSize = true, Location = new Point(438, 50) });

            // Buscador y orden de los mods instalados (el orden elegido se recuerda).
            _filterInstalled.PlaceholderText = Localization.T("installed_filter_hint");
            _filterInstalled.SetBounds(438, 72, 176, 24);
            _filterInstalled.TextChanged += (s, e) => ApplyInstalledView();
            Content.Controls.Add(_filterInstalled);

            foreach (var key in new[] { "sort_mods_az", "sort_mods_recent", "sort_mods_oldest", "sort_mods_enabled", "sort_mods_disabled" })
                _sortInstalled.Items.Add(Localization.T(key));
            _sortInstalled.SelectedIndex = Math.Max(0, Array.IndexOf(ModListView.Modes, ModListView.Normalize(SettingsManager.Load().ModsSort)));
            _sortInstalled.SetBounds(620, 72, 158, 24);
            _sortInstalled.SelectedIndexChanged += (s, e) =>
            {
                var settings = SettingsManager.Load();
                settings.ModsSort = ModListView.Modes[Math.Max(0, _sortInstalled.SelectedIndex)];
                SettingsManager.Save(settings);
                ApplyInstalledView();
            };
            Content.Controls.Add(_sortInstalled);

            _installed.SetBounds(438, 102, 340, 330);
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

        private void ShowDetails()
        {
            if (_results.SelectedItem is ContentProject item) ContentUi.OpenDetails(this, item);
        }

        private void ShowInstalledDetails()
        {
            if (_installed.SelectedItem is not InstalledItem item || item.Mod.ProjectId.Length == 0) return;
            using var form = new ProjectDetailForm(ContentSources.ParseSource(item.Mod.Source), item.Mod.ProjectId, item.Mod.ProjectTitle);
            form.ShowDialog(this);
        }

        // Una fila de la lista de instalados, con lo que hace falta para filtrar y ordenar.
        private abstract class ModEntry
        {
            public string Name = "";      // lo que se busca y se ordena
            public string Key = "";       // archivo sin ".disabled": identifica la fila aunque cambie de lugar
            public DateTime Date;         // cuándo se instaló (fecha de creación del archivo)
            public bool Off;              // desactivado
        }

        private class InstalledItem : ModEntry
        {
            public readonly InstalledMod Mod;
            public bool Disabled => Off;
            public InstalledItem(InstalledMod m, bool disabled, DateTime date)
            {
                Mod = m;
                Off = disabled;
                Date = date;
                Key = m.Filename;
                Name = m.ProjectTitle.Length > 0 ? m.ProjectTitle : m.Filename;
            }
            public override string ToString() =>
                $"{(Disabled ? Localization.T("mod_off") + " " : "")}{Mod.ProjectTitle}\n{Mod.Filename} · {ContentUi.SourceName(ContentSources.ParseSource(Mod.Source))}{(Mod.Pinned ? " · " + Localization.T("mod_pinned") : "")}";
        }

        // Un .jar que está en la carpeta de mods pero que vos pusiste a mano (no vino
        // de Modrinth) — se puede ver y sacar desde acá igual, simplemente no tiene
        // chequeo de actualizaciones porque no sabemos de dónde salió.
        private class LocalModItem : ModEntry
        {
            public readonly string Path;
            public LocalModItem(string path, DateTime date)
            {
                Path = path;
                Off = path.EndsWith(ModManager.DisabledSuffix, StringComparison.OrdinalIgnoreCase);
                Date = date;
                var name = System.IO.Path.GetFileName(path);
                Key = Off ? name.Substring(0, name.Length - ModManager.DisabledSuffix.Length) : name;
                Name = Key;
            }
            public override string ToString() =>
                $"{(Off ? Localization.T("mod_off") + " " : "")}{System.IO.Path.GetFileName(Path)}\n({Localization.T("local_mod")})";
        }

        private DateTime FileDate(string filename)
        {
            try
            {
                var path = Path.Combine(_instance.ModsDir, filename);
                if (!File.Exists(path)) path += ModManager.DisabledSuffix;
                return File.Exists(path) ? File.GetCreationTimeUtc(path) : DateTime.MinValue;
            }
            catch { return DateTime.MinValue; }
        }

        // Lee los mods de la instancia y los muestra según el buscador y el orden. keepKey = fila a dejar seleccionada.
        private void RefreshInstalled(string? keepKey = null)
        {
            _allInstalled.Clear();

            var tracked = ModManager.LoadIndex(_instance).Mods;
            var trackedFilenames = new System.Collections.Generic.HashSet<string>(
                tracked.Select(m => m.Filename), StringComparer.OrdinalIgnoreCase);

            foreach (var m in tracked)
                _allInstalled.Add(new InstalledItem(m, ModManager.IsDisabled(_instance, m.Filename), FileDate(m.Filename)));

            try
            {
                Directory.CreateDirectory(_instance.ModsDir);
                var modFiles = Directory.GetFiles(_instance.ModsDir).Where(f =>
                    f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
                    || f.EndsWith(".jar" + ModManager.DisabledSuffix, StringComparison.OrdinalIgnoreCase));
                foreach (var path in modFiles)
                {
                    var name = Path.GetFileName(path);
                    if (name.EndsWith(ModManager.DisabledSuffix, StringComparison.OrdinalIgnoreCase))
                        name = name.Substring(0, name.Length - ModManager.DisabledSuffix.Length);
                    if (!trackedFilenames.Contains(name))
                        _allInstalled.Add(new LocalModItem(path, File.GetCreationTimeUtc(path)));
                }
            }
            catch { }

            ApplyInstalledView(keepKey);
        }

        private void ApplyInstalledView(string? keepKey = null)
        {
            keepKey ??= (_installed.SelectedItem as ModEntry)?.Key;

            var shown = _allInstalled.Where(e => ModListView.Matches(_filterInstalled.Text, e.Name, e.Key)).ToList();
            var sorted = ModListView.Sort(shown, ModListView.Modes[Math.Max(0, _sortInstalled.SelectedIndex)], e => e.Name, e => e.Date, e => e.Off);

            _installed.BeginUpdate();
            _installed.Items.Clear();
            foreach (var entry in sorted) _installed.Items.Add(entry);
            if (keepKey != null)
            {
                for (int i = 0; i < _installed.Items.Count; i++)
                    if (_installed.Items[i] is ModEntry e && string.Equals(e.Key, keepKey, StringComparison.OrdinalIgnoreCase)) { _installed.SelectedIndex = i; break; }
            }
            _installed.EndUpdate();
        }

        private void OpenModsVault()
        {
            VaultForm.Open(this, _instance.ModsDir, Localization.T("btn_mods_vault") + " — " + _instance.Name);
            RefreshInstalled();
        }

        private async System.Threading.Tasks.Task SearchAsync(int page = 0)
        {
            _status.Text = Localization.T("btn_search") + "...";
            _results.Items.Clear();
            _pager.SetBusy(true);
            try
            {
                var source = ContentUi.SourceOf(_source);
                await _filters.EnsureSourceAsync(source);
                var result = await ContentSources.SearchPageAsync(source, ContentType.Mod, _search.Text, _instance, page, _filters.Current);
                if (source != ContentUi.SourceOf(_source)) return; // cambió de fuente mientras buscaba
                foreach (var h in result.Items) _results.Items.Add(h);
                _pager.SetState(result);
                _status.Text = Localization.T("results_count", Math.Max(result.Total, result.Items.Count));
            }
            catch (Exception ex)
            {
                _pager.SetState(null);
                _status.Text = "Error: " + ex.Message;
            }
        }

        // "Elegir versión...": versiones del mod para la versión de Minecraft de la instancia (la última y las anteriores).
        // La que se elige queda fijada: "Revisar actualizaciones" no la cambia.
        private async System.Threading.Tasks.Task ChooseVersionAsync()
        {
            if (_results.SelectedItem is not ContentProject item) return;
            _status.Text = Localization.T("btn_search") + "...";
            _btnVersion.Enabled = false;
            System.Collections.Generic.List<ContentVersion> versions;
            try
            {
                versions = await ContentSources.GetVersionsAsync(item.Source, ContentType.Mod, item.Id, _instance);
            }
            catch (Exception ex)
            {
                _status.Text = "Error: " + ex.Message;
                return;
            }
            finally
            {
                _btnVersion.Enabled = true;
            }
            _status.Text = "";

            if (versions.Count == 0)
            {
                MessageBox.Show(this, Localization.T("pick_version_none", _instance.McVersion), "Velin", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var picker = new VersionPickerForm(item.Title, _instance.McVersion, versions);
            if (picker.ShowDialog(this) != DialogResult.OK || picker.Selected == null) return;
            await InstallAsync(picker.Selected);
        }

        private async System.Threading.Tasks.Task InstallAsync(ContentVersion? chosen = null)
        {
            if (_results.SelectedItem is not ContentProject item) return;
            _status.Text = Localization.T("installing", item.Title);
            _btnInstall.Enabled = false;
            _btnVersion.Enabled = false;
            try
            {
                // Instala el mod y también sus dependencias obligatorias (ej: Fabric API).
                var result = await ModManager.InstallModWithDependenciesAsync(_instance, item.Source,
                    item.Id, item.Title, msg => _status.Text = msg, chosen);
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
                _btnVersion.Enabled = true;
            }
        }

        // Activa o desactiva el mod elegido (renombra el .jar a .jar.disabled y viceversa).
        private void ToggleSelected()
        {
            var keepKey = (_installed.SelectedItem as ModEntry)?.Key;
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
            RefreshInstalled(keepKey);
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
