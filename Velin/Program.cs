using System;
using System.Diagnostics;
using System.Net;
using System.Windows.Forms;
using Velin.Services;
using Velin.UI;

namespace Velin
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // DPI: se configura por código (la forma recomendada), no en el app.manifest.
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Más conexiones HTTP en paralelo = descargas de librerías/assets más rápidas.
            ServicePointManager.DefaultConnectionLimit = 20;

            var settings = SettingsManager.Load();

            // Proxy opcional (Configuración): lo usan todas las descargas de Velin.
            if (!string.IsNullOrWhiteSpace(settings.ProxyHost) && settings.ProxyPort is > 0 and < 65536)
            {
                try { System.Net.Http.HttpClient.DefaultProxy = new WebProxy(settings.ProxyHost.Trim(), settings.ProxyPort); }
                catch { /* un proxy mal escrito no debe impedir que abra el launcher */ }
            }
            PathService.EnsureAll();

            // Primer inicio (todavía no hay settings.json): asistente para elegir idioma, tema y usuario.
            if (!SettingsManager.Exists)
            {
                var langs = LanguageCatalog.Available();
                Localization.SetLanguage(LanguageCatalog.Detect(System.Globalization.CultureInfo.CurrentUICulture, langs));
                Theme.Current = ThemeManager.FindByName(settings.ThemeName);
                using (var wizard = new Velin.Forms.FirstRunForm(settings))
                    wizard.ShowDialog();
                settings = SettingsManager.Load();
            }
            Localization.SetLanguage(settings.Language);
            Theme.Current = ThemeManager.FindByName(settings.ThemeName);
            SoundManager.MusicEnabled = settings.MusicEnabled;
            SoundManager.ClickEnabled = settings.ClickSoundEnabled;
            SoundManager.MusicVolume = Math.Clamp(settings.MusicVolume, 0, 100);

            SoundManager.Init();
            SoundManager.InstallGlobalClickSound();

            // La pantalla de inicio se puede desactivar en Configuración, o saltear con un click.
            if (settings.ShowSplash)
            {
                using var splash = new Velin.Forms.SplashForm();
                splash.Show();
                splash.Refresh();

                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 5000 && !splash.Skipped)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(30);
                }

                splash.Close();
            }

            SoundManager.StartMusic();
            Application.Run(new Velin.Forms.MainForm(ShortcutService.ParsePlayArgument(args)));
            SoundManager.StopMusic();
        }
    }
}
