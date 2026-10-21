using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Login con Microsoft/Xbox: abre el navegador y espera a que el usuario termine. Sin códigos ni keys.
    public class BrowserLoginForm : VelinFormBase
    {
        private readonly string _clientId;
        private readonly CancellationTokenSource _cts = new();
        private readonly Label _status = new();
        private readonly Button _useCode = new();
        private readonly Button _cancel = new() { DialogResult = DialogResult.Cancel };

        public MicrosoftAuthResult? Result { get; private set; }
        // Si el navegador no sirve (por ejemplo, está bloqueado), el usuario puede pasar al login por código.
        public bool UseDeviceCode { get; private set; }

        public BrowserLoginForm(string clientId) : base(Localization.T("dc_title"))
        {
            _clientId = clientId;
            LockSize(420, 200);
            StartPosition = FormStartPosition.CenterParent;

            var intro = new Label { Text = Localization.T("bl_intro"), AutoSize = false };
            intro.SetBounds(12, 12, 390, 64);
            Content.Controls.Add(intro);

            _status.Text = Localization.T("bl_waiting");
            _status.AutoSize = false;
            _status.SetBounds(12, 84, 390, 54);
            Content.Controls.Add(_status);

            _useCode.Text = Localization.T("btn_use_code");
            _useCode.SetBounds(12, 150, 200, 32);
            _useCode.Click += (s, e) => { UseDeviceCode = true; DialogResult = DialogResult.Cancel; };
            Content.Controls.Add(_useCode);

            _cancel.Text = Localization.T("btn_cancel");
            _cancel.SetBounds(292, 150, 110, 32);
            Content.Controls.Add(_cancel);
            CancelButton = _cancel;

            FormClosing += (s, e) => { try { _cts.Cancel(); } catch { } };
            Shown += async (s, e) => await RunAsync();
            Theme.Apply(this);
        }

        private async System.Threading.Tasks.Task RunAsync()
        {
            try
            {
                var result = await MicrosoftAuth.LoginWithBrowserAsync(_clientId, OpenUrl, _cts.Token);
                if (IsDisposed) return;
                Result = result;
                DialogResult = DialogResult.OK;
            }
            catch (OperationCanceledException) { /* el usuario cerró la ventana */ }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                _status.Text = ex.Message;
                _cancel.Text = Localization.T("btn_close");
            }
        }

        private static void OpenUrl(string url)
        {
            if (!url.StartsWith("https://login.microsoftonline.com/", StringComparison.OrdinalIgnoreCase))
                throw new MicrosoftAuthException("Dirección de login inesperada.");
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
    }
}
