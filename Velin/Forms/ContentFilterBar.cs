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
    // Fila de filtros del buscador: orden, categoría, versión de Minecraft y loader.
    // Por defecto la versión y el loader son los de la instancia, pero se pueden cambiar o poner en "cualquiera".
    public class ContentFilterBar : Panel
    {
        private readonly ContentType _type;
        private readonly ComboBox _sort = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _category = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _version = new() { DropDownStyle = ComboBoxStyle.DropDown };
        private readonly ComboBox _loader = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly List<ContentCategory> _categories = new();
        private ContentSource? _loadedSource;
        private bool _loading = true;
        private bool _versionsLoaded;
        private string _lastVersionText = "";

        public event Action? Changed;

        public ContentFilterBar(ContentType type, Instance? instance, int width = 410)
        {
            _type = type;
            BackColor = Color.Transparent;
            Size = new Size(width, 26);
            bool hasLoader = type == ContentType.Mod || type == ContentType.ModPack;

            _sort.Items.AddRange(new object[]
            {
                Localization.T("filter_sort_relevance"), Localization.T("filter_sort_downloads"),
                Localization.T("filter_sort_newest"), Localization.T("filter_sort_updated")
            });
            _sort.SelectedIndex = 0;

            _category.Items.Add(Localization.T("filter_all_categories"));
            _category.SelectedIndex = 0;

            var any = Localization.T("filter_any_version");
            _version.Items.Add(any);
            var initial = type == ContentType.ModPack || instance == null ? any : (string.IsNullOrEmpty(instance.McVersion) ? any : instance.McVersion);
            _version.Text = initial;
            _lastVersionText = initial;

            _loader.Items.AddRange(new object[] { Localization.T("filter_any_loader"), "Fabric", "Forge", "Quilt", "NeoForge" });
            _loader.SelectedIndex = type == ContentType.Mod && instance != null ? LoaderIndex(instance.Loader) : 0;

            // Anchos: orden 98, categoría 118, versión 80 y, si corresponde, loader con el resto.
            int x = 0;
            _sort.SetBounds(x, 1, 98, 24); x += 102;
            int categoryWidth = hasLoader ? 118 : width - x - 84;
            _category.SetBounds(x, 1, categoryWidth, 24); x += categoryWidth + 4;
            _version.SetBounds(x, 1, 80, 24); x += 84;
            if (hasLoader) { _loader.SetBounds(x, 1, width - x, 24); Controls.Add(_loader); }
            Controls.Add(_sort); Controls.Add(_category); Controls.Add(_version);

            _sort.SelectedIndexChanged += (s, e) => Raise();
            _category.SelectedIndexChanged += (s, e) => Raise();
            _loader.SelectedIndexChanged += (s, e) => Raise();
            _version.SelectionChangeCommitted += (s, e) => BeginInvoke(new Action(RaiseVersion));
            _version.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; RaiseVersion(); } };
            _version.Leave += (s, e) => RaiseVersion();
            _loading = false;
        }

        private static int LoaderIndex(ModLoaderType l) => l switch
        {
            ModLoaderType.Fabric => 1,
            ModLoaderType.Forge => 2,
            ModLoaderType.Quilt => 3,
            ModLoaderType.NeoForge => 4,
            _ => 0
        };

        public ContentFilter Current
        {
            get
            {
                var version = _version.Text.Trim();
                if (version.Length == 0 || version == Localization.T("filter_any_version")) version = "";
                return new ContentFilter
                {
                    Sort = (ContentSort)Math.Max(0, _sort.SelectedIndex),
                    CategoryId = _category.SelectedIndex > 0 && _category.SelectedIndex - 1 < _categories.Count ? _categories[_category.SelectedIndex - 1].Id : null,
                    McVersion = version.Length == 0 ? null : version,
                    Loader = _loader.Visible && _loader.SelectedIndex > 0
                        ? _loader.SelectedIndex switch { 1 => ModLoaderType.Fabric, 2 => ModLoaderType.Forge, 3 => ModLoaderType.Quilt, _ => ModLoaderType.NeoForge }
                        : null
                };
            }
        }

        private void Raise() { if (!_loading) Changed?.Invoke(); }

        private void RaiseVersion()
        {
            var text = _version.Text.Trim();
            if (text == _lastVersionText) return;   // sólo si cambió de verdad
            _lastVersionText = text;
            Raise();
        }

        // Carga las categorías de la fuente elegida (y, la primera vez, la lista de versiones de Minecraft). Si no hay red, los filtros siguen andando sin esas listas.
        public async Task EnsureSourceAsync(ContentSource source)
        {
            if (_loadedSource != source)
            {
                _loadedSource = source;
                List<ContentCategory> cats;
                try { cats = await ContentCatalog.GetCategoriesAsync(source, _type); }
                catch { cats = new List<ContentCategory>(); }
                if (IsDisposed) return;
                _loading = true;
                _categories.Clear();
                _categories.AddRange(cats);
                _category.Items.Clear();
                _category.Items.Add(Localization.T("filter_all_categories"));
                foreach (var c in cats) _category.Items.Add(c.Name);
                _category.SelectedIndex = 0;
                _loading = false;
            }

            if (!_versionsLoaded)
            {
                _versionsLoaded = true;
                try
                {
                    var versions = await ContentCatalog.GetMinecraftVersionsAsync();
                    if (IsDisposed) return;
                    var keep = _version.Text;
                    _loading = true;
                    foreach (var v in versions.Take(60)) _version.Items.Add(v);
                    _version.Text = keep;
                    _loading = false;
                }
                catch { _versionsLoaded = false; }
            }
        }
    }
}
