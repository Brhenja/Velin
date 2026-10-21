using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Resource packs, shaders y datapacks: buscar en Modrinth o CurseForge, instalar, ver lo instalado y quitarlo.
    // Misma interfaz que la de mods. Los datapacks van DENTRO de un mundo, así que ahí se elige el mundo arriba a la derecha.
    public class ContentBrowserForm : VelinFormBase
    {
        private readonly Instance _instance;
        private readonly ContentKind _kind;
        private readonly ContentType _type;

        private readonly ComboBox _source = new();
        private Label _hint = null!;
        private readonly TextBox _search = new();
        private readonly Button _btnSearch = new();
        private readonly ListBox _results = new() { ItemHeight = 34 };
        private readonly Button _btnInstall = new();
        private readonly ListBox _installed = new() { ItemHeight = 34 };
        private readonly Button _btnRemove = new();
        private readonly Button _btnOpenFolder = new();
        private readonly ComboBox _world = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Label _status = new();

        public ContentBrowserForm(Instance instance, ContentKind kind, string? initialWorld = null)
            : base(Localization.T("cb_title", KindName(kind), instance.Name, instance.McVersion))
        {
            _instance = instance;
            _kind = kind;
            _type = ContentSources.ToType(kind);
            LockSize(800, 545);
            StartPosition = FormStartPosition.CenterParent;

            _hint = ContentUi.AddSearchBar(Content, _source, _search, _btnSearch, lockToModrinth: kind == ContentKind.DataPack);
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

            if (kind == ContentKind.DataPack)
            {
                Content.Controls.Add(new Label { Text = Localization.T("cb_world"), AutoSize = true, Location = new Point(438, 52) });
                _world.SetBounds(500, 48, 278, 24);
                var worlds = WorldService.ListWorlds(instance);
                foreach (var w in worlds) _world.Items.Add(w.Name);
                if (_world.Items.Count > 0)
                {
                    var idx = initialWorld != null ? _world.Items.IndexOf(initialWorld) : -1;
                    _world.SelectedIndex = idx >= 0 ? idx : 0;
                }
                _world.SelectedIndexChanged += (s, e) => RefreshInstalled();
                Content.Controls.Add(_world);
            }
            else
            {
                Content.Controls.Add(new Label { Text = Localization.T("cb_installed"), AutoSize = true, Location = new Point(438, 50) });
            }

            _installed.SetBounds(438, 72, 340, 360);
            Content.Controls.Add(_installed);

            _btnRemove.Text = Localization.T("btn_remove_arrow");
            _btnRemove.SetBounds(438, 442, 110, 32);
            _btnRemove.Click += (s, e) => RemoveSelected();
            Content.Controls.Add(_btnRemove);

            _btnOpenFolder.Text = Localization.T("cb_open_folder");
            _btnOpenFolder.SetBounds(556, 442, 222, 32);
            _btnOpenFolder.Click += (s, e) => OpenFolder();
            Content.Controls.Add(_btnOpenFolder);

            _status.SetBounds(12, 482, 766, 20);
            Content.Controls.Add(_status);

            Load += async (s, e) =>
            {
                RefreshInstalled();
                if (_kind == ContentKind.DataPack && _world.Items.Count == 0)
                {
                    _status.Text = Localization.T("cb_no_worlds");
                    _btnInstall.Enabled = false;
                }
                await SearchAsync();
            };
            Theme.Apply(this);
        }

        private static string KindName(ContentKind kind) => kind switch
        {
            ContentKind.ResourcePack => Localization.T("kind_resourcepack"),
            ContentKind.Shader => Localization.T("kind_shader"),
            _ => Localization.T("kind_datapack")
        };

        private class FileItem
        {
            public readonly string Path;
            public FileItem(string path) { Path = path; }
            public override string ToString()
            {
                var name = System.IO.Path.GetFileName(Path);
                if (Directory.Exists(Path)) return name + "\n" + Localization.T("cb_folder");
                long size = 0; try { size = new FileInfo(Path).Length; } catch { }
                return name + "\n" + (size >= 1024 * 1024 ? (size / (1024.0 * 1024)).ToString("0.0") + " MB" : Math.Max(1, size / 1024) + " KB");
            }
        }

        // Carpeta actual (para datapacks depende del mundo elegido). Null si todavía no hay mundo.
        private string? CurrentFolder()
        {
            if (_kind == ContentKind.DataPack)
                return _world.SelectedItem is string w ? ContentService.FolderFor(_instance, _kind, w) : null;
            return ContentService.FolderFor(_instance, _kind);
        }

        private void RefreshInstalled()
        {
            _installed.Items.Clear();
            var folder = CurrentFolder();
            if (folder == null) return;
            foreach (var p in ContentService.ListInstalled(folder)) _installed.Items.Add(new FileItem(p));
        }

        private async System.Threading.Tasks.Task SearchAsync()
        {
            _status.Text = Localization.T("btn_search") + "...";
            _results.Items.Clear();
            var source = ContentUi.SourceOf(_source);
            try
            {
                var hits = await ContentSources.SearchAsync(source, _type, _search.Text, _instance);
                if (source != ContentUi.SourceOf(_source)) return; // cambió de fuente mientras buscaba
                foreach (var h in hits) _results.Items.Add(h);
                _status.Text = Localization.T("results_count", hits.Count);
            }
            catch (Exception ex) { _status.Text = "Error: " + ex.Message; }
        }

        private async System.Threading.Tasks.Task InstallAsync()
        {
            if (_results.SelectedItem is not ContentProject item) return;
            var folder = CurrentFolder();
            if (folder == null) { _status.Text = Localization.T("cb_no_worlds"); return; }

            _btnInstall.Enabled = false;
            try
            {
                await ContentService.InstallAsync(_instance, item.Source, _type, item.Id, item.Title, folder, msg => _status.Text = msg);
                RefreshInstalled();
                _status.Text = Localization.T("installed", item.Title);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localization.T("install_failed", ex.Message), "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _status.Text = "";
            }
            finally { _btnInstall.Enabled = true; }
        }

        private void RemoveSelected()
        {
            if (_installed.SelectedItem is not FileItem item) return;
            if (MessageBox.Show(this, Localization.T("cb_confirm_remove", Path.GetFileName(item.Path)), "Velin", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { ContentService.Remove(item.Path); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            RefreshInstalled();
        }

        private void OpenFolder()
        {
            var folder = CurrentFolder();
            if (folder == null) return;
            try
            {
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
            catch { }
        }
    }
}
