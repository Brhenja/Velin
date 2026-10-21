using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Muestra el código del login de Microsoft y espera a que el usuario lo ingrese en la web.
    public class DeviceCodeForm : VelinFormBase
    {
        private readonly string _clientId;
        private readonly CancellationTokenSource _cts = new();
        private readonly Label _intro = new();
        private readonly Label _code = new();
        private readonly Label _url = new();
        private readonly Label _status = new();
        private readonly Button _copy = new();
        private readonly Button _open = new();
        private readonly Button _cancel = new() { DialogResult = DialogResult.Cancel };
        private string _verificationUri = "";

        public MicrosoftAuthResult? Result { get; private set; }

        public DeviceCodeForm(string clientId) : base(Localization.T("dc_title"))
        {
            _clientId = clientId;
            LockSize(420, 270);
            StartPosition = FormStartPosition.CenterParent;

            _intro.Text = Localization.T("dc_intro");
            _intro.AutoSize = false;
            _intro.SetBounds(12, 12, 390, 40);
            Content.Controls.Add(_intro);

            _code.Text = "· · · · · · · ·";
            _code.AutoSize = false;
            _code.TextAlign = ContentAlignment.MiddleCenter;
            _code.Font = new Font("Consolas", 22f, FontStyle.Bold);
            _code.SetBounds(12, 58, 390, 48);
            Content.Controls.Add(_code);

            _url.AutoSize = false;
            _url.TextAlign = ContentAlignment.MiddleCenter;
            _url.SetBounds(12, 108, 390, 22);
            Content.Controls.Add(_url);

            _copy.Text = Localization.T("btn_copy_code");
            _copy.SetBounds(12, 138, 190, 30);
            _copy.Enabled = false;
            _copy.Click += (s, e) => { try { Clipboard.SetText(_code.Text); } catch { } };
            Content.Controls.Add(_copy);

            _open.Text = Localization.T("btn_open_page");
            _open.SetBounds(212, 138, 190, 30);
            _open.Enabled = false;
            _open.Click += (s, e) => OpenPage();
            Content.Controls.Add(_open);

            _status.Text = Localization.T("dc_waiting");
            _status.AutoSize = false;
            _status.SetBounds(12, 178, 390, 50);
            Content.Controls.Add(_status);

            _cancel.Text = Localization.T("btn_cancel");
            _cancel.SetBounds(292, 232, 110, 30);
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
                var info = await MicrosoftAuth.StartDeviceFlowAsync(_clientId, _cts.Token);
                if (IsDisposed) return;
                _verificationUri = info.VerificationUri;
                _code.Text = info.UserCode;
                _url.Text = info.VerificationUri;
                _copy.Enabled = true;
                _open.Enabled = true;
                try { Clipboard.SetText(info.UserCode); } catch { }
                OpenPage();

                var result = await MicrosoftAuth.CompleteDeviceFlowAsync(_clientId, info, _cts.Token);
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

        private void OpenPage()
        {
            if (!_verificationUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
            try { Process.Start(new ProcessStartInfo { FileName = _verificationUri, UseShellExecute = true }); }
            catch { /* si no abre, el usuario puede ir a mano: la dirección está en pantalla */ }
        }
    }
}
