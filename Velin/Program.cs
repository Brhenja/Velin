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
        static void Main()
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
            Localization.Lang = settings.Language;
            Theme.Current = ThemeManager.FindByName(settings.ThemeName);
            SoundManager.MusicEnabled = settings.MusicEnabled;
            SoundManager.ClickEnabled = settings.ClickSoundEnabled;

            PathService.EnsureAll();
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
            Application.Run(new Velin.Forms.MainForm());
            SoundManager.StopMusic();
        }
    }
}
