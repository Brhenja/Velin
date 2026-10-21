using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Velin.Models;
using Velin.UI;

namespace Velin.Forms
{
    public class CopyToInstanceDialog : VelinFormBase
    {
        private readonly ComboBox _instance = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _folder = new() { DropDownStyle = ComboBoxStyle.DropDownList };

        public Instance? SelectedInstance => _instance.SelectedItem as Instance;
        public string SelectedSubfolder => (string)(_folder.SelectedItem ?? "mods");

        public CopyToInstanceDialog(List<Instance> instances) : base(Localization.T("btn_copy_to_instance"))
        {
            LockSize(320, 215);
            StartPosition = FormStartPosition.CenterParent;

            Content.Controls.Add(new Label { Text = Localization.T("lbl_instance"), AutoSize = true, Location = new Point(16, 12) });
            foreach (var i in instances) _instance.Items.Add(i);
            _instance.DisplayMember = "Name";
            _instance.SelectedIndex = 0;
            _instance.SetBounds(16, 34, 280, 24);
            Content.Controls.Add(_instance);

            Content.Controls.Add(new Label { Text = Localization.T("lbl_destination"), AutoSize = true, Location = new Point(16, 68) });
            _folder.Items.AddRange(new object[] { "mods", "resourcepacks", "shaderpacks", "." });
            _folder.SelectedIndex = 0;
            _folder.SetBounds(16, 90, 280, 24);
            Content.Controls.Add(_folder);

            var ok = new Button { Text = Localization.T("btn_ok"), DialogResult = DialogResult.OK };
            ok.SetBounds(130, 134, 85, 30);
            Content.Controls.Add(ok);

            var cancel = new Button { Text = Localization.T("btn_cancel"), DialogResult = DialogResult.Cancel };
            cancel.SetBounds(222, 134, 85, 30);
            Content.Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;

            Theme.Apply(this);
        }
    }
}
