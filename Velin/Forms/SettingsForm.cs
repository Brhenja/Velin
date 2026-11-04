using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Configuración dividida en secciones (lista a la izquierda, opciones a la derecha).
    public class SettingsForm : VelinFormBase
    {
        // General
        private readonly ComboBox _lang = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _theme = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _username = new();
        private readonly CheckBox _splash = new();
        private readonly CheckBox _confirmDelete = new();
        private readonly CheckBox _keepOpen = new();
        // Juego
        private readonly NumericUpDown _ram = new() { Minimum = 512, Maximum = 32768, Increment = 512 };
        private readonly CheckBox _optFlags = new();
        private readonly TextBox _jvmArgs = new();
        private readonly CheckBox _backup = new();
        // Java opcionales
        private readonly CheckBox _graalEnabled = new();
        private readonly Label _graalStatus = new();
        private readonly Button _graalBtn = new();
        private readonly CheckBox _semeruEnabled = new();
        private readonly ComboBox _semeruMajor = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Label _semeruStatus = new();
        private readonly Button _semeruBtn = new();
        private readonly VelinProgressBar _javaProgress = new() { Minimum = 0, Maximum = 100, Visible = false };
        private readonly Label _javaMessage = new();
        private readonly Button _javaCancel = new();
        private CancellationTokenSource? _javaCts;
        // Contenido
        private readonly ComboBox _defaultSource = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _cfKey = new() { UseSystemPasswordChar = true };
        private readonly LinkLabel _cfLink = new();
        // Sonido
        private readonly CheckBox _music = new();
        private readonly NumericUpDown _volume = new() { Minimum = 0, Maximum = 100, Increment = 5 };
        private readonly CheckBox _click = new();
        // Red y datos
        private readonly TextBox _proxy = new();
        private readonly Label _cacheLabel = new();
        // Acerca de
        private readonly LinkLabel _github = new();

        private readonly ListBox _nav = new() { ItemHeight = 30 };
        private readonly List<Panel> _panels = new();
        private readonly List<LinkLabel> _links = new();

        public SettingsForm() : base(Localization.T("settings_title"))
        {
            LockSize(660, 540);
            StartPosition = FormStartPosition.CenterParent;
            var s = SettingsManager.Load();

            _nav.SetBounds(12, 12, 150, 470);
            foreach (var key in new[] { "nav_general", "nav_game", "nav_java", "nav_content", "nav_sound", "nav_network", "nav_about" })
                _nav.Items.Add(Localization.T(key));
            _nav.SelectedIndexChanged += (a, b) => ShowPanel(_nav.SelectedIndex);
            Content.Controls.Add(_nav);

            BuildGeneral(s);
            BuildGame(s);
            BuildJava(s);
            BuildContent(s);
            BuildSound(s);
            BuildNetwork(s);
            BuildAbout();

            var save = new Button { Text = Localization.T("btn_save") };
            save.SetBounds(442, 492, 100, 32);
            save.Click += OnSave;
            Content.Controls.Add(save);
            var cancel = new Button { Text = Localization.T("btn_close") };
            cancel.SetBounds(548, 492, 100, 32);
            cancel.Click += (a, b) => Close();
            Content.Controls.Add(cancel);

            Theme.Apply(this);
            foreach (var link in _links)
            {
                // Los enlaces toman los colores del tema activo (después de Theme.Apply, que no los conoce).
                link.LinkColor = Theme.Accent;
                link.VisitedLinkColor = Theme.Accent;
                link.ActiveLinkColor = Theme.ControlText;
                link.LinkBehavior = LinkBehavior.HoverUnderline;
                link.BackColor = Color.Transparent;
            }

            FormClosing += (a, b) => { try { _javaCts?.Cancel(); } catch { } };
            _nav.SelectedIndex = 0;
        }

        // ---- Secciones -------------------------------------------------------------------------

        private Panel NewPanel()
        {
            var p = new Panel { BackColor = Color.Transparent, Visible = false };
            p.SetBounds(176, 12, 472, 470);
            Content.Controls.Add(p);
            _panels.Add(p);
            return p;
        }

        private void ShowPanel(int index)
        {
            for (int i = 0; i < _panels.Count; i++) _panels[i].Visible = i == index;
        }

        private static Label Lbl(Panel p, string text, int y, bool hint = false, int width = 0)
        {
            var l = new Label { Text = text, AutoSize = width == 0 };
            if (hint) l.Font = new Font("Segoe UI", 7.5f, FontStyle.Italic);
            if (width > 0) l.SetBounds(0, y, width, 0);
            l.Location = new Point(0, y);
            p.Controls.Add(l);
            return l;
        }

        private static CheckBox Chk(Panel p, CheckBox c, string text, bool value, int y, int height = 24)
        {
            c.Text = text;
            c.AutoSize = false;
            c.Checked = value;
            c.SetBounds(0, y, 460, height);
            p.Controls.Add(c);
            return c;
        }

        private void BuildGeneral(AppSettings s)
        {
            var p = NewPanel();
            int y = 0;
            Lbl(p, Localization.T("lbl_language"), y); y += 22;
            _lang.Items.AddRange(new object[] { "Español", "English" });
            _lang.SelectedIndex = s.Language == "en" ? 1 : 0;
            _lang.SetBounds(0, y, 200, 24); p.Controls.Add(_lang); y += 36;

            Lbl(p, Localization.T("lbl_theme"), y); y += 22;
            var themes = ThemeManager.LoadAll();
            foreach (var t in themes) _theme.Items.Add(t.Name);
            var idx = themes.FindIndex(t => t.Name == s.ThemeName);
            _theme.SelectedIndex = idx >= 0 ? idx : 0;
            _theme.SetBounds(0, y, 340, 24); p.Controls.Add(_theme); y += 28;
            Lbl(p, Localization.T("lbl_themes_hint"), y, hint: true); y += 32;

            Lbl(p, Localization.T("lbl_default_username"), y); y += 22;
            _username.Text = s.DefaultUsername;
            _username.SetBounds(0, y, 200, 24); p.Controls.Add(_username); y += 38;

            Chk(p, _splash, Localization.T("chk_splash"), s.ShowSplash, y); y += 30;
            Chk(p, _confirmDelete, Localization.T("chk_confirm_delete"), s.ConfirmDelete, y); y += 30;
            Chk(p, _keepOpen, Localization.T("chk_keep_open"), s.KeepLauncherOpen, y, 40);
        }

        private void BuildGame(AppSettings s)
        {
            var p = NewPanel();
            int y = 0;
            Lbl(p, Localization.T("lbl_default_ram"), y); y += 22;
            _ram.Value = Math.Clamp(s.DefaultRamMB, (int)_ram.Minimum, (int)_ram.Maximum);
            _ram.SetBounds(0, y, 150, 24); p.Controls.Add(_ram); y += 40;

            Chk(p, _optFlags, Localization.T("chk_optimized_flags"), s.UseOptimizedFlags, y, 40); y += 46;

            Lbl(p, Localization.T("lbl_global_jvm"), y); y += 22;
            _jvmArgs.Text = s.GlobalJvmArgs;
            _jvmArgs.SetBounds(0, y, 440, 24); p.Controls.Add(_jvmArgs); y += 28;
            Lbl(p, Localization.T("lbl_global_jvm_hint"), y, hint: true); y += 34;

            Chk(p, _backup, Localization.T("chk_backup"), s.BackupWorldsEnabled, y, 40);
        }

        private void BuildJava(AppSettings s)
        {
            var p = NewPanel();
            int y = 0;
            Lbl(p, Localization.T("lbl_java_optional"), y).Font = Theme.UiFont(10f, FontStyle.Bold); y += 28;
            Lbl(p, Localization.T("lbl_java_hint"), y, hint: true, width: 450).Height = 44; y += 52;

            // GraalVM
            Lbl(p, Localization.T("java_graal_title"), y); y += 22;
            _graalStatus.AutoSize = false; _graalStatus.SetBounds(0, y + 4, 190, 20); p.Controls.Add(_graalStatus);
            _graalBtn.SetBounds(196, y, 120, 28); _graalBtn.Click += async (a, b) => await ToggleGraalAsync(); p.Controls.Add(_graalBtn);
            Chk(p, _graalEnabled, Localization.T("chk_java_enabled"), s.GraalVmEnabled, y + 2); _graalEnabled.SetBounds(330, y + 2, 130, 24);
            y += 48;

            // Semeru
            Lbl(p, Localization.T("java_semeru_title"), y); y += 22;
            foreach (var m in OptionalJavaManager.SemeruMajors) _semeruMajor.Items.Add($"Java {m}");
            _semeruMajor.SelectedIndex = OptionalJavaManager.SemeruMajors.Length - 1;
            _semeruMajor.SetBounds(0, y, 90, 24); p.Controls.Add(_semeruMajor);
            _semeruMajor.SelectedIndexChanged += (a, b) => RefreshJavaState();
            _semeruStatus.AutoSize = false; _semeruStatus.SetBounds(98, y + 4, 92, 20); p.Controls.Add(_semeruStatus);
            _semeruBtn.SetBounds(196, y - 2, 120, 28); _semeruBtn.Click += async (a, b) => await ToggleSemeruAsync(); p.Controls.Add(_semeruBtn);
            Chk(p, _semeruEnabled, Localization.T("chk_java_enabled"), s.SemeruEnabled, y); _semeruEnabled.SetBounds(330, y, 130, 24);
            y += 52;

            _javaMessage.AutoSize = false; _javaMessage.SetBounds(0, y, 460, 36); p.Controls.Add(_javaMessage); y += 40;
            _javaProgress.SetBounds(0, y, 340, 18); p.Controls.Add(_javaProgress);
            _javaCancel.Text = Localization.T("btn_cancel"); _javaCancel.SetBounds(350, y - 5, 100, 28); _javaCancel.Visible = false;
            _javaCancel.Click += (a, b) => { try { _javaCts?.Cancel(); } catch { } };
            p.Controls.Add(_javaCancel);

            RefreshJavaState();
        }

        private void RefreshJavaState()
        {
            bool graal = OptionalJavaManager.IsInstalled(OptionalJavaKind.GraalVM, OptionalJavaManager.GraalMajor);
            _graalStatus.Text = Localization.T(graal ? "java_status_installed" : "java_status_missing");
            _graalBtn.Text = Localization.T(graal ? "btn_remove_java" : "btn_download");
            _graalEnabled.Enabled = graal;
            if (!graal) _graalEnabled.Checked = false;

            int major = SelectedSemeruMajor();
            bool sem = OptionalJavaManager.IsInstalled(OptionalJavaKind.Semeru, major);
            _semeruStatus.Text = Localization.T(sem ? "java_status_installed" : "java_status_missing");
            _semeruBtn.Text = Localization.T(sem ? "btn_remove_java" : "btn_download");
            _semeruEnabled.Enabled = OptionalJavaManager.AnySemeruInstalled();
            if (!_semeruEnabled.Enabled) _semeruEnabled.Checked = false;
        }

        private int SelectedSemeruMajor() =>
            OptionalJavaManager.SemeruMajors[Math.Max(0, _semeruMajor.SelectedIndex)];

        private void SetJavaBusy(bool busy)
        {
            _graalBtn.Enabled = !busy; _semeruBtn.Enabled = !busy; _semeruMajor.Enabled = !busy;
            _javaProgress.Visible = busy; _javaCancel.Visible = busy; _javaCancel.Enabled = true;
            if (busy) _javaProgress.Value = 0;
        }

        private async Task ToggleGraalAsync()
        {
            var kind = OptionalJavaKind.GraalVM; int major = OptionalJavaManager.GraalMajor;
            await InstallOrRemoveAsync(kind, major, "GraalVM 21", enable => { _graalEnabled.Checked = enable; PersistJavaEnabled(); });
        }

        private async Task ToggleSemeruAsync()
        {
            var kind = OptionalJavaKind.Semeru; int major = SelectedSemeruMajor();
            await InstallOrRemoveAsync(kind, major, $"IBM Semeru {major}", enable =>
            {
                if (enable) _semeruEnabled.Checked = true;
                else if (!OptionalJavaManager.AnySemeruInstalled()) _semeruEnabled.Checked = false;
                PersistJavaEnabled();
            });
        }

        private async Task InstallOrRemoveAsync(OptionalJavaKind kind, int major, string name, Action<bool> afterChange)
        {
            if (OptionalJavaManager.IsInstalled(kind, major))
            {
                if (MessageBox.Show(this, Localization.T("confirm_remove_java", name), "Velin", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                try { OptionalJavaManager.Remove(kind, major); _javaMessage.Text = Localization.T("java_removed", name); }
                catch (Exception ex) { _javaMessage.Text = Localization.T("java_error", ex.Message); }
                RefreshJavaState();
                afterChange(false);
                RefreshJavaState();
                return;
            }

            _javaCts?.Dispose();
            _javaCts = new CancellationTokenSource();
            var ct = _javaCts.Token;
            SetJavaBusy(true);
            var msg = new Progress<string>(m => { if (!IsDisposed) _javaMessage.Text = m; });
            var pct = new Progress<double>(v => { if (!IsDisposed) _javaProgress.Value = Math.Clamp((int)(v * 100), 0, 100); });
            try
            {
                await OptionalJavaManager.InstallAsync(kind, major, msg, pct, ct);
                if (IsDisposed) return;
                _javaMessage.Text = Localization.T("java_done", name);
                RefreshJavaState();
                afterChange(true);
                RefreshJavaState();
            }
            catch (OperationCanceledException) { if (!IsDisposed) _javaMessage.Text = Localization.T("status_cancelled"); }
            catch (Exception ex) { if (!IsDisposed) _javaMessage.Text = Localization.T("java_error", ex.Message); }
            finally { if (!IsDisposed) SetJavaBusy(false); }
        }

        // Instalar/quitar un Java guarda enseguida si está activado, sin esperar al botón Guardar.
        private void PersistJavaEnabled()
        {
            try
            {
                var cur = SettingsManager.Load();
                cur.GraalVmEnabled = _graalEnabled.Checked;
                cur.SemeruEnabled = _semeruEnabled.Checked;
                SettingsManager.Save(cur);
            }
            catch { }
        }

        private void BuildContent(AppSettings s)
        {
            var p = NewPanel();
            int y = 0;
            Lbl(p, Localization.T("lbl_default_source"), y); y += 22;
            _defaultSource.Items.AddRange(new object[] { "Modrinth", "CurseForge" });
            _defaultSource.SelectedIndex = string.Equals(s.DefaultContentSource, "curseforge", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            _defaultSource.SetBounds(0, y, 200, 24); p.Controls.Add(_defaultSource); y += 40;

            Lbl(p, Localization.T("lbl_cf_key"), y); y += 22;
            _cfKey.Text = s.CurseForgeApiKey;
            _cfKey.SetBounds(0, y, 340, 24); p.Controls.Add(_cfKey); y += 28;

            _cfLink.Text = Localization.T("lbl_cf_key_link");
            _cfLink.AutoSize = true;
            _cfLink.Font = new Font("Segoe UI", 7.5f, FontStyle.Italic);
            _cfLink.Location = new Point(0, y);
            _cfLink.LinkClicked += (a, b) => OpenUrl("https://console.curseforge.com/");
            p.Controls.Add(_cfLink);
            _links.Add(_cfLink);
        }

        private void BuildSound(AppSettings s)
        {
            var p = NewPanel();
            int y = 0;
            Chk(p, _music, Localization.T("chk_music"), s.MusicEnabled, y); y += 32;
            Lbl(p, Localization.T("lbl_music_volume"), y); y += 22;
            _volume.Value = Math.Clamp(s.MusicVolume, 0, 100);
            _volume.SetBounds(0, y, 100, 24); p.Controls.Add(_volume); y += 40;
            Chk(p, _click, Localization.T("chk_click"), s.ClickSoundEnabled, y);
        }

        private void BuildNetwork(AppSettings s)
        {
            var p = NewPanel();
            int y = 0;
            Lbl(p, Localization.T("lbl_proxy"), y); y += 22;
            _proxy.Text = s.ProxyHost.Length > 0 && s.ProxyPort > 0 ? s.ProxyHost + ":" + s.ProxyPort : "";
            _proxy.SetBounds(0, y, 340, 24); p.Controls.Add(_proxy); y += 48;

            var open = new Button { Text = Localization.T("btn_open_data") };
            open.SetBounds(0, y, 220, 30);
            open.Click += (a, b) => { try { Directory.CreateDirectory(PathService.DataDir); Process.Start(new ProcessStartInfo { FileName = PathService.DataDir, UseShellExecute = true }); } catch { } };
            p.Controls.Add(open); y += 48;

            _cacheLabel.AutoSize = true; _cacheLabel.Location = new Point(0, y); p.Controls.Add(_cacheLabel); y += 26;
            var clear = new Button { Text = Localization.T("btn_clear_cache") };
            clear.SetBounds(0, y, 220, 30);
            clear.Click += async (a, b) =>
            {
                clear.Enabled = false;
                await Task.Run(ImageCache.ClearCache);
                UpdateCacheLabel();
                clear.Enabled = true;
            };
            p.Controls.Add(clear);
            UpdateCacheLabel();
        }

        private void UpdateCacheLabel()
        {
            double mb = ImageCache.CacheSizeBytes() / 1048576.0;
            _cacheLabel.Text = Localization.T("lbl_cache_size", mb.ToString("0.0"));
        }

        private void BuildAbout()
        {
            var p = NewPanel();
            int y = 0;
            Lbl(p, "Velin " + AppInfo.Version, y).Font = Theme.UiFont(14f, FontStyle.Bold); y += 36;
            Lbl(p, Localization.T("lbl_credit"), y, hint: true); y += 26;

            _github.Text = "GitHub: " + AppInfo.RepoUrl;
            _github.AutoSize = true;
            _github.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            _github.Location = new Point(0, y);
            _github.LinkArea = new LinkArea(8, AppInfo.RepoUrl.Length);
            _github.LinkClicked += (a, b) => OpenUrl(AppInfo.RepoUrl);
            p.Controls.Add(_github);
            _links.Add(_github);
            y += 34;
            Lbl(p, Localization.T("lbl_about_disclaimer"), y, hint: true, width: 450).Height = 60;
        }

        // ---- Guardado --------------------------------------------------------------------------

        private void OnSave(object? sender, EventArgs e)
        {
            // Se parte de la configuración guardada, así los ajustes que no están en esta
            // ventana (como el orden de la lista de instancias) no se pierden al guardar.
            var ns = SettingsManager.Load();
            ns.Language = _lang.SelectedIndex == 1 ? "en" : "es";
            ns.ThemeName = _theme.SelectedItem?.ToString() ?? "Velin (medieval)";
            ns.DefaultUsername = string.IsNullOrWhiteSpace(_username.Text) ? "Player" : _username.Text.Trim();
            ns.DefaultRamMB = (int)_ram.Value;
            ns.MusicEnabled = _music.Checked;
            ns.MusicVolume = (int)_volume.Value;

            // Proxy: "host:puerto" (vacío = sin proxy).
            var proxyText = _proxy.Text.Trim();
            if (proxyText.Length == 0) { ns.ProxyHost = ""; ns.ProxyPort = 0; }
            else
            {
                var cut = proxyText.LastIndexOf(':');
                if (cut <= 0 || !int.TryParse(proxyText.Substring(cut + 1), out var proxyPort) || proxyPort is < 1 or > 65535)
                {
                    _nav.SelectedIndex = 5;
                    MessageBox.Show(this, Localization.T("proxy_invalid"), "Velin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                ns.ProxyHost = proxyText.Substring(0, cut).Trim();
                ns.ProxyPort = proxyPort;
            }
            ns.ClickSoundEnabled = _click.Checked;
            ns.ShowSplash = _splash.Checked;
            ns.ConfirmDelete = _confirmDelete.Checked;
            ns.BackupWorldsEnabled = _backup.Checked;
            ns.KeepLauncherOpen = _keepOpen.Checked;
            ns.UseOptimizedFlags = _optFlags.Checked;
            ns.GlobalJvmArgs = _jvmArgs.Text.Trim();
            ns.DefaultContentSource = _defaultSource.SelectedIndex == 1 ? "curseforge" : "modrinth";
            ns.CurseForgeApiKey = _cfKey.Text.Trim();
            ns.GraalVmEnabled = _graalEnabled.Checked && OptionalJavaManager.IsInstalled(OptionalJavaKind.GraalVM, OptionalJavaManager.GraalMajor);
            ns.SemeruEnabled = _semeruEnabled.Checked && OptionalJavaManager.AnySemeruInstalled();
            SettingsManager.Save(ns);

            // Música, volumen, sonido de click y tema se aplican al toque; sólo el idioma pide reiniciar
            // (porque los textos de las ventanas ya están armados con el idioma anterior).
            SoundManager.SetMusicVolume(ns.MusicVolume);
            SoundManager.SetMusicEnabled(ns.MusicEnabled);
            SoundManager.SetClickEnabled(ns.ClickSoundEnabled);
            Theme.Current = ThemeManager.FindByName(ns.ThemeName);

            MessageBox.Show(this, Localization.T("settings_saved"), Localization.T("settings_title"));
            Close();
        }

        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
            catch { /* sin navegador predeterminado: no pasa nada */ }
        }
    }
}
