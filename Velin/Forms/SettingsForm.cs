using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    public class SettingsForm : VelinFormBase
    {
        private readonly ComboBox _lang = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _theme = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _username = new();
        private readonly NumericUpDown _ram = new() { Minimum = 512, Maximum = 32768, Increment = 512 };
        private readonly CheckBox _music = new();
        private readonly CheckBox _click = new();
        private readonly TextBox _proxy = new();
        private readonly CheckBox _splash = new();
        private readonly CheckBox _backup = new();
        private readonly CheckBox _keepOpen = new();
        private readonly Button _save = new();
        private readonly Label _credit = new();
        private readonly LinkLabel _github = new();
        private readonly TextBox _cfKey = new() { UseSystemPasswordChar = true };
        private readonly LinkLabel _cfLink = new();
        private readonly Label _themesHint = new();

        public SettingsForm() : base(Localization.T("settings_title"))
        {
            LockSize(400, 662);
            StartPosition = FormStartPosition.CenterParent;

            var s = SettingsManager.Load();

            int y = 12;
            Content.Controls.Add(Lbl(Localization.T("lbl_language"), y)); y += 22;
            _lang.Items.AddRange(new object[] { "Español", "English" });
            _lang.SelectedIndex = s.Language == "en" ? 1 : 0;
            _lang.SetBounds(16, y, 200, 24);
            Content.Controls.Add(_lang);
            y += 34;

            Content.Controls.Add(Lbl(Localization.T("lbl_theme"), y)); y += 22;
            var themes = ThemeManager.LoadAll();
            foreach (var t in themes) _theme.Items.Add(t.Name);
            var idx = themes.FindIndex(t => t.Name == s.ThemeName);
            _theme.SelectedIndex = idx >= 0 ? idx : 0;
            _theme.SetBounds(16, y, 340, 24);
            Content.Controls.Add(_theme);
            y += 26;

            _themesHint.Text = Localization.T("lbl_themes_hint");
            _themesHint.AutoSize = true;
            _themesHint.Font = new Font("Segoe UI", 7.5f, FontStyle.Italic);
            _themesHint.Location = new Point(16, y);
            Content.Controls.Add(_themesHint);
            y += 30;

            Content.Controls.Add(Lbl(Localization.T("lbl_default_username"), y)); y += 22;
            _username.Text = s.DefaultUsername;
            _username.SetBounds(16, y, 200, 24);
            Content.Controls.Add(_username);
            y += 34;

            Content.Controls.Add(Lbl(Localization.T("lbl_default_ram"), y)); y += 22;
            _ram.Value = s.DefaultRamMB;
            _ram.SetBounds(16, y, 150, 24);
            Content.Controls.Add(_ram);
            y += 36;

            _music.Text = Localization.T("chk_music");
            _music.AutoSize = true;
            _music.Checked = s.MusicEnabled;
            _music.Location = new Point(16, y);
            Content.Controls.Add(_music);
            y += 28;

            _click.Text = Localization.T("chk_click");
            _click.AutoSize = true;
            _click.Checked = s.ClickSoundEnabled;
            _click.Location = new Point(16, y);
            Content.Controls.Add(_click);
            y += 28;

            _splash.Text = Localization.T("chk_splash");
            _splash.AutoSize = true;
            _splash.Checked = s.ShowSplash;
            _splash.Location = new Point(16, y);
            Content.Controls.Add(_splash);
            y += 28;

            // Texto largo: en vez de AutoSize, un ancho fijo para que se parta en dos renglones.
            _backup.Text = Localization.T("chk_backup");
            _backup.AutoSize = false;
            _backup.SetBounds(16, y, 360, 38);
            _backup.Checked = s.BackupWorldsEnabled;
            Content.Controls.Add(_backup);
            y += 46;

            Content.Controls.Add(Lbl(Localization.T("lbl_proxy"), y)); y += 22;
            _proxy.Text = s.ProxyHost.Length > 0 && s.ProxyPort > 0 ? s.ProxyHost + ":" + s.ProxyPort : "";
            _proxy.SetBounds(16, y, 340, 24);
            Content.Controls.Add(_proxy);
            y += 34;

            Content.Controls.Add(Lbl(Localization.T("lbl_cf_key"), y)); y += 22;
            _cfKey.Text = s.CurseForgeApiKey;
            _cfKey.SetBounds(16, y, 340, 24);
            Content.Controls.Add(_cfKey);
            y += 28;

            _cfLink.Text = Localization.T("lbl_cf_key_link");
            _cfLink.AutoSize = true;
            _cfLink.Font = new Font("Segoe UI", 7.5f, FontStyle.Italic);
            _cfLink.Location = new Point(16, y);
            _cfLink.LinkClicked += (s2, e2) => OpenUrl("https://console.curseforge.com/");
            Content.Controls.Add(_cfLink);
            y += 28;

            _keepOpen.Text = Localization.T("chk_keep_open");
            _keepOpen.AutoSize = false;
            _keepOpen.SetBounds(16, y, 360, 38);
            _keepOpen.Checked = s.KeepLauncherOpen;
            Content.Controls.Add(_keepOpen);
            y += 46;

            _save.Text = Localization.T("btn_save");
            _save.SetBounds(16, y, 110, 32);
            _save.Click += OnSave;
            Content.Controls.Add(_save);
            y += 46;

            _credit.Text = Localization.T("lbl_credit");
            _credit.AutoSize = true;
            _credit.Font = new Font("Segoe UI", 8f, FontStyle.Italic);
            _credit.Location = new Point(16, y);
            Content.Controls.Add(_credit);

            // "GitHub: <enlace>" al lado del crédito; el enlace abre el repositorio en el navegador.
            _github.Text = "GitHub: " + AppInfo.RepoUrl;
            _github.AutoSize = true;
            _github.Font = new Font("Segoe UI", 8f, FontStyle.Italic);
            _github.Location = new Point(16 + _credit.PreferredSize.Width + 12, y);
            _github.LinkArea = new LinkArea(8, AppInfo.RepoUrl.Length);
            _github.LinkClicked += (s2, e2) => OpenUrl(AppInfo.RepoUrl);
            Content.Controls.Add(_github);

            Theme.Apply(this);

            // Los enlaces toman los colores del tema activo (después de Theme.Apply, que no los conoce).
            foreach (var link in new[] { _github, _cfLink })
            {
                link.LinkColor = Theme.Accent;
                link.VisitedLinkColor = Theme.Accent;
                link.ActiveLinkColor = Theme.ControlText;
                link.LinkBehavior = LinkBehavior.HoverUnderline;
                link.BackColor = Color.Transparent;
            }
        }

        private static Label Lbl(string text, int y)
        {
            var l = new Label { Text = text, AutoSize = true };
            l.Location = new Point(16, y);
            return l;
        }

        private void OnSave(object? sender, System.EventArgs e)
        {
            // Se parte de la configuración guardada, así los ajustes que no están en esta
            // ventana (como el orden de la lista de instancias) no se pierden al guardar.
            var ns = SettingsManager.Load();
            ns.Language = _lang.SelectedIndex == 1 ? "en" : "es";
            ns.ThemeName = _theme.SelectedItem?.ToString() ?? "Velin (medieval)";
            ns.DefaultUsername = string.IsNullOrWhiteSpace(_username.Text) ? "Player" : _username.Text.Trim();
            ns.DefaultRamMB = (int)_ram.Value;
            ns.MusicEnabled = _music.Checked;

            // Proxy: "host:puerto" (vacío = sin proxy).
            var proxyText = _proxy.Text.Trim();
            if (proxyText.Length == 0) { ns.ProxyHost = ""; ns.ProxyPort = 0; }
            else
            {
                var cut = proxyText.LastIndexOf(':');
                if (cut <= 0 || !int.TryParse(proxyText.Substring(cut + 1), out var proxyPort) || proxyPort is < 1 or > 65535)
                {
                    MessageBox.Show(this, Localization.T("proxy_invalid"), "Velin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                ns.ProxyHost = proxyText.Substring(0, cut).Trim();
                ns.ProxyPort = proxyPort;
            }
            ns.ClickSoundEnabled = _click.Checked;
            ns.ShowSplash = _splash.Checked;
            ns.BackupWorldsEnabled = _backup.Checked;
            ns.KeepLauncherOpen = _keepOpen.Checked;
            ns.CurseForgeApiKey = _cfKey.Text.Trim();
            SettingsManager.Save(ns);

            // Música, sonido de click y tema se aplican al toque; sólo el idioma pide reiniciar
            // (porque los textos de las ventanas ya están armados con el idioma anterior).
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
