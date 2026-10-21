using System.Drawing;
using System.Windows.Forms;
using Velin.UI;

namespace Velin.Forms
{
    // Diálogo mínimo para pedir un texto (ej: nombre de una carpeta nueva).
    public class PromptDialog : VelinFormBase
    {
        private readonly TextBox _input = new();
        public string Value => _input.Text.Trim();

        public PromptDialog(string title, string label, string initial = "") : base(title)
        {
            LockSize(340, 120);
            StartPosition = FormStartPosition.CenterParent;

            Content.Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(12, 12) });
            _input.Text = initial;
            _input.SetBounds(12, 36, 300, 24);
            Content.Controls.Add(_input);

            var ok = new Button { Text = Localization.T("btn_ok"), DialogResult = DialogResult.OK };
            ok.SetBounds(142, 72, 80, 30);
            Content.Controls.Add(ok);
            var cancel = new Button { Text = Localization.T("btn_cancel"), DialogResult = DialogResult.Cancel };
            cancel.SetBounds(230, 72, 82, 30);
            Content.Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
            Theme.Apply(this);
            Load += (s, e) => _input.Focus();
        }
    }
}
