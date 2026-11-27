using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Velin.Models;
using Velin.UI;

namespace Velin.Forms
{
    // Lista de versiones de un mod para la versión de Minecraft de la instancia (la última arriba), para elegir una más vieja.
    public class VersionPickerForm : VelinFormBase
    {
        private readonly ListBox _list = new() { ItemHeight = 34 };

        public ContentVersion? Selected => (_list.SelectedItem as Item)?.Version;

        private class Item
        {
            public readonly ContentVersion Version;
            public Item(ContentVersion v) { Version = v; }

            public override string ToString()
            {
                var tag = Version.ReleaseType == 2 ? " · beta" : Version.ReleaseType == 3 ? " · alpha" : "";
                var date = Version.Date.Year > 2000 ? " · " + Version.Date.ToString("yyyy-MM-dd") : "";
                return $"{Version.Name}\n{Version.FileName}{date}{tag}";
            }
        }

        public VersionPickerForm(string modTitle, string mcVersion, List<ContentVersion> versions)
            : base(Localization.T("pick_version_title", modTitle))
        {
            LockSize(520, 410);
            StartPosition = FormStartPosition.CenterParent;

            var hint = new Label { Text = Localization.T("pick_version_hint", mcVersion), AutoSize = false };
            hint.SetBounds(12, 10, 484, 40);
            Content.Controls.Add(hint);

            _list.SetBounds(12, 54, 484, 276);
            foreach (var v in versions) _list.Items.Add(new Item(v));
            if (_list.Items.Count > 0) _list.SelectedIndex = 0;
            _list.DoubleClick += (s, e) => { if (_list.SelectedItem != null) DialogResult = DialogResult.OK; };
            Content.Controls.Add(_list);

            var ok = new Button { Text = Localization.T("btn_ok"), DialogResult = DialogResult.OK };
            ok.SetBounds(316, 342, 88, 32);
            Content.Controls.Add(ok);
            var cancel = new Button { Text = Localization.T("btn_cancel"), DialogResult = DialogResult.Cancel };
            cancel.SetBounds(410, 342, 86, 32);
            Content.Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
            Theme.Apply(this);
        }
    }
}
