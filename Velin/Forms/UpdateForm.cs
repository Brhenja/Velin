using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Aviso de versión nueva: notas de la release, enlace a GitHub y botón que descarga el instalador y lo ejecuta.
    public class UpdateForm : VelinFormBase
    {
        private readonly UpdateInfo _info;
        private readonly CancellationTokenSource _cts = new();
        private readonly RichTextBox _notes = new() { ReadOnly = true, ScrollBars = RichTextBoxScrollBars.Vertical, DetectUrls = true, WordWrap = true };
        private readonly VelinProgressBar _progress = new() { Minimum = 0, Maximum = 100, Visible = false };
        private readonly Label _status = new() { AutoSize = false };
        private readonly CheckBox _skip = new() { AutoSize = false };
        private readonly Button _download = new();
        private readonly Button _github = new();
        private readonly Button _later = new();
        private bool _busy;

        public UpdateForm(UpdateInfo info) : base(Localization.T("upd_title"))
        {
            _info = info;
            LockSize(580, 470);
            StartPosition = FormStartPosition.CenterParent;
            bool installed = UpdateService.IsInstalledCopy && info.InstallerUrl.Length > 0;

            var title = new Label { Text = Localization.T("upd_available", info.Version), AutoSize = false };
            title.SetBounds(12, 12, 556, 28);
            var sub = new Label { Text = Localization.T("upd_current", AppInfo.Version), AutoSize = false };
            sub.SetBounds(12, 42, 556, 20);
            Content.Controls.Add(title);
            Content.Controls.Add(sub);

            _notes.Font = Theme.UiFont(9.5f);
            _notes.SetBounds(12, 70, 556, 250);
            Content.Controls.Add(_notes);

            _status.SetBounds(12, 326, 556, installed ? 20 : 40);
            if (!installed) _status.Text = Localization.T("upd_portable_note");
            Content.Controls.Add(_status);
            _progress.SetBounds(12, 350, 556, 16);
            Content.Controls.Add(_progress);

            _skip.Text = Localization.T("upd_skip");
            _skip.SetBounds(12, 376, 300, 24);
            Content.Controls.Add(_skip);

            _download.Text = Localization.T(installed ? "upd_download" : "upd_open_page");
            _download.SetBounds(12, 408, 210, 36);
            _download.Click += async (s, e) => await OnDownloadAsync(installed);
            Content.Controls.Add(_download);

            _github.Text = Localization.T("upd_view_github");
            _github.SetBounds(228, 408, 170, 36);
            _github.Click += (s, e) => OpenUrl(_info.PageUrl);
            Content.Controls.Add(_github);

            _later.Text = Localization.T("upd_later");
            _later.SetBounds(404, 408, 164, 36);
            _later.Click += (s, e) => Close();
            Content.Controls.Add(_later);

            Theme.Apply(this);
            title.Font = Theme.UiFont(13f, FontStyle.Bold);
            ContentTextRenderer.Render(_notes, info.Notes.Length > 0 ? info.Notes : "Velin " + info.Version, false);
            _notes.LinkClicked += (s, e) => OpenUrl(e.LinkText ?? "");

            FormClosing += (s, e) =>
            {
                if (_skip.Checked)
                {
                    var settings = SettingsManager.Load();
                    settings.SkippedUpdateVersion = _info.Version.ToString();
                    SettingsManager.Save(settings);
                }
                try { _cts.Cancel(); } catch { }
            };
        }

        private async Task OnDownloadAsync(bool installed)
        {
            if (!installed) { OpenUrl(_info.PageUrl); return; }
            if (_busy) return;
            _busy = true;
            _download.Enabled = false;
            _progress.Visible = true;
            _status.Text = Localization.T("upd_downloading");
            try
            {
                var progress = new Progress<double>(v => { if (!IsDisposed) _progress.Value = Math.Clamp((int)(v * 100), 0, 100); });
                var path = await UpdateService.DownloadInstallerAsync(_info, progress, _cts.Token);
                if (IsDisposed) return;
                if (MessageBox.Show(this, Localization.T("upd_confirm_run"), "Velin", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    _status.Text = "";
                    return;
                }
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                Application.Exit();   // el instalador no puede reemplazar Velin.exe mientras esté abierto
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!IsDisposed) _status.Text = Localization.T("upd_error", ex.Message);
            }
            finally
            {
                _busy = false;
                if (!IsDisposed) { _download.Enabled = true; _progress.Visible = false; }
            }
        }

        private static void OpenUrl(string url)
        {
            if (!ProjectDetailsService.IsSafeUrl(url)) return;
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); } catch { }
        }
    }
}
