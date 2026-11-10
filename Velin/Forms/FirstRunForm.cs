using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Asistente del primer inicio: idioma, tema, nombre de usuario, aviso de actualizaciones, pantalla de inicio, música y sonido de click.
    // Guarda settings.json al continuar.
    public class FirstRunForm : VelinFormBase
    {
        private readonly AppSettings _settings;
        private readonly Label _welcome = new() { AutoSize = false };
        private readonly Label _lblLang = new() { AutoSize = true };
        private readonly Label _lblTheme = new() { AutoSize = true };
        private readonly Label _lblUser = new() { AutoSize = true };
        private readonly Label _hint = new() { AutoSize = false };
        private readonly ComboBox _lang = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _theme = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly TextBox _user = new();
        private readonly CheckBox _updates = new() { AutoSize = false, Checked = true };
        private readonly CheckBox _splash = new() { AutoSize = false };
        private readonly CheckBox _music = new() { AutoSize = false };
        private readonly CheckBox _click = new() { AutoSize = false };
        private readonly Button _go = new();
        private bool _loading = true;

        public FirstRunForm(AppSettings settings) : base("Velin")
        {
            _settings = settings;
            LockSize(460, 440);
            StartPosition = FormStartPosition.CenterScreen;

            _welcome.SetBounds(12, 12, 436, 30);
            _lblLang.Location = new Point(12, 56);
            _lang.SetBounds(12, 78, 436, 24);
            _lblTheme.Location = new Point(12, 114);
            _theme.SetBounds(12, 136, 436, 24);
            _lblUser.Location = new Point(12, 172);
            _user.SetBounds(12, 194, 436, 24);
            _hint.SetBounds(12, 222, 436, 34);
            _updates.SetBounds(12, 264, 436, 24);
            _splash.SetBounds(12, 290, 436, 24);
            _music.SetBounds(12, 316, 436, 24);
            _click.SetBounds(12, 342, 436, 24);
            _go.SetBounds(298, 384, 150, 38);
            foreach (Control c in new Control[] { _welcome, _lblLang, _lang, _lblTheme, _theme, _lblUser, _user, _hint, _updates, _splash, _music, _click, _go }) Content.Controls.Add(c);

            var langs = LanguageCatalog.Available();
            foreach (var l in langs) _lang.Items.Add(l);
            var idx = langs.FindIndex(l => l.Code == Localization.Lang);
            _lang.SelectedIndex = idx >= 0 ? idx : 0;

            var themes = ThemeManager.LoadAll();
            foreach (var t in themes) _theme.Items.Add(t.Name);
            var ti = themes.FindIndex(t => t.Name == Theme.Current.Name);
            _theme.SelectedIndex = ti >= 0 ? ti : 0;

            _user.Text = string.IsNullOrWhiteSpace(settings.DefaultUsername) ? "Player" : settings.DefaultUsername;
            _splash.Checked = settings.ShowSplash;
            _music.Checked = settings.MusicEnabled;
            _click.Checked = settings.ClickSoundEnabled;

            _lang.SelectedIndexChanged += (s, e) =>
            {
                if (_loading) return;
                Localization.SetLanguage((_lang.SelectedItem as LanguageInfo)?.Code);
                ApplyTexts();
                RefreshTheme();   // la fuente puede cambiar según el idioma
            };
            _theme.SelectedIndexChanged += (s, e) =>
            {
                if (_loading) return;
                Theme.Current = ThemeManager.FindByName(_theme.SelectedItem?.ToString() ?? "");
                RefreshTheme();   // también repinta la barra de título (minimizar / cerrar)
                Invalidate(true);
            };
            _go.Click += (s, e) => Close();
            AcceptButton = _go;
            FormClosing += (s, e) => SaveChoices();   // también si se cierra con la X: se guardan las opciones elegidas

            ApplyTexts();
            Theme.Apply(this);
            _loading = false;
        }

        private void ApplyTexts()
        {
            _welcome.Text = Localization.T("first_welcome");
            _lblLang.Text = Localization.T("lbl_language");
            _lblTheme.Text = Localization.T("lbl_theme");
            _lblUser.Text = Localization.T("lbl_default_username");
            _hint.Text = Localization.T("first_hint");
            _updates.Text = Localization.T("chk_check_updates");
            _splash.Text = Localization.T("chk_splash");
            _music.Text = Localization.T("chk_music");
            _click.Text = Localization.T("chk_click");
            _go.Text = Localization.T("first_continue");
            _hint.Font = Theme.PlainFont(8f, FontStyle.Italic);
        }

        private void SaveChoices()
        {
            var s = SettingsManager.Load();
            s.Language = (_lang.SelectedItem as LanguageInfo)?.Code ?? "en";
            s.ThemeName = _theme.SelectedItem?.ToString() ?? s.ThemeName;
            s.DefaultUsername = string.IsNullOrWhiteSpace(_user.Text) ? "Player" : _user.Text.Trim();
            s.CheckUpdatesOnStart = _updates.Checked;
            s.ShowSplash = _splash.Checked;
            s.MusicEnabled = _music.Checked;
            s.ClickSoundEnabled = _click.Checked;
            SettingsManager.Save(s);
        }
    }
}
