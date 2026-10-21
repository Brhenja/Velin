using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Velin.Services;
using Velin.Models;
using Velin.UI;

namespace Velin.Forms
{
    // Opciones de lanzamiento por instancia. Todas son opcionales: dejarlas vacías (o en 0)
    // significa que el juego arranca exactamente como siempre.
    public class LaunchOptionsForm : VelinFormBase
    {
        private readonly Instance _instance;
        private readonly TextBox _java = new();
        private readonly NumericUpDown _width = new() { Minimum = 0, Maximum = 7680 };
        private readonly NumericUpDown _height = new() { Minimum = 0, Maximum = 4320 };
        private readonly CheckBox _fullscreen = new();
        private readonly TextBox _server = new();
        private readonly TextBox _world = new();
        private readonly TextBox _extra = new();
        private readonly ComboBox _account = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly List<Account> _accounts = new();

        public LaunchOptionsForm(Instance instance) : base(Localization.T("lo_title", instance.Name))
        {
            _instance = instance;
            LockSize(420, 474);
            StartPosition = FormStartPosition.CenterParent;

            int y = 12;

            // Cuenta con la que se juega esta instancia (la primera opción usa la cuenta por defecto).
            Content.Controls.Add(new Label { Text = Localization.T("lo_account"), AutoSize = true, Location = new Point(12, y) });
            y += 22;
            _accounts.AddRange(AccountManager.LoadAll());
            _account.Items.Add(Localization.T("lo_account_default"));
            foreach (var a in _accounts) _account.Items.Add(a.ToString());
            var accIdx = _accounts.FindIndex(a => a.Id == instance.AccountId);
            _account.SelectedIndex = accIdx >= 0 ? accIdx + 1 : 0;
            _account.SetBounds(12, y, 396, 24);
            Content.Controls.Add(_account);
            y += 36;

            Content.Controls.Add(new Label { Text = Localization.T("lo_java"), AutoSize = true, Location = new Point(12, y) });
            y += 22;
            _java.Text = instance.JavaPath;
            _java.SetBounds(12, y, 300, 24);
            Content.Controls.Add(_java);
            var browse = new Button { Text = Localization.T("btn_browse") };
            browse.SetBounds(318, y - 1, 90, 26);
            browse.Click += (s, e) =>
            {
                using var dlg = new OpenFileDialog { Filter = "Java (java.exe; javaw.exe)|java.exe;javaw.exe|*.*|*.*" };
                if (dlg.ShowDialog(this) == DialogResult.OK) _java.Text = dlg.FileName;
            };
            Content.Controls.Add(browse);
            y += 30;

            // Java encontrados en el equipo (Program Files, JAVA_HOME y los que baja Velin).
            var detected = JavaScanner.Scan();
            var javaPick = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            javaPick.Items.Add(Localization.T("lo_java_detected"));
            foreach (var j in detected) javaPick.Items.Add(j.ToString());
            javaPick.SelectedIndex = 0;
            javaPick.SetBounds(12, y, 396, 24);
            javaPick.SelectedIndexChanged += (s, e) =>
            {
                if (javaPick.SelectedIndex > 0) _java.Text = detected[javaPick.SelectedIndex - 1].ExePath;
            };
            Content.Controls.Add(javaPick);
            y += 36;

            Content.Controls.Add(new Label { Text = Localization.T("lo_resolution"), AutoSize = true, Location = new Point(12, y) });
            y += 22;
            _width.Value = Math.Clamp(instance.WindowWidth, 0, 7680);
            _width.SetBounds(12, y, 90, 24);
            Content.Controls.Add(_width);
            Content.Controls.Add(new Label { Text = "×", AutoSize = true, Location = new Point(108, y + 3) });
            _height.Value = Math.Clamp(instance.WindowHeight, 0, 4320);
            _height.SetBounds(126, y, 90, 24);
            Content.Controls.Add(_height);
            y += 36;

            _fullscreen.Text = Localization.T("lo_fullscreen");
            _fullscreen.AutoSize = true;
            _fullscreen.Checked = instance.Fullscreen;
            _fullscreen.Location = new Point(12, y);
            _fullscreen.CheckedChanged += (s, e) => UpdateSizeControls();
            Content.Controls.Add(_fullscreen);
            y += 30;

            y = AddTextRow(Localization.T("lo_join_server"), _server, instance.JoinServer, y);
            y = AddTextRow(Localization.T("lo_join_world"), _world, instance.JoinWorld, y);
            y = AddTextRow(Localization.T("lo_extra_args"), _extra, instance.ExtraGameArgs, y);

            var save = new Button { Text = Localization.T("btn_save") };
            save.SetBounds(12, y, 110, 32);
            save.Click += OnSave;
            Content.Controls.Add(save);

            var cancel = new Button { Text = Localization.T("btn_cancel") };
            cancel.SetBounds(130, y, 110, 32);
            cancel.Click += (s, e) => Close();
            Content.Controls.Add(cancel);

            UpdateSizeControls();
            Theme.Apply(this);
        }

        private int AddTextRow(string label, TextBox box, string value, int y)
        {
            Content.Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(12, y) });
            y += 22;
            box.Text = value;
            box.SetBounds(12, y, 396, 24);
            Content.Controls.Add(box);
            return y + 34;
        }

        // En pantalla completa el tamaño de ventana no se usa.
        private void UpdateSizeControls()
        {
            _width.Enabled = !_fullscreen.Checked;
            _height.Enabled = !_fullscreen.Checked;
        }

        private void OnSave(object? sender, EventArgs e)
        {
            var java = _java.Text.Trim();
            if (java.Length > 0 && !File.Exists(java))
            {
                MessageBox.Show(this, Localization.T("lo_java") + "\n\n" + java, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _instance.AccountId = _account.SelectedIndex > 0 ? _accounts[_account.SelectedIndex - 1].Id : "";
            _instance.JavaPath = java;
            _instance.WindowWidth = (int)_width.Value;
            _instance.WindowHeight = (int)_height.Value;
            _instance.Fullscreen = _fullscreen.Checked;
            _instance.JoinServer = _server.Text.Trim();
            _instance.JoinWorld = _world.Text.Trim();
            _instance.ExtraGameArgs = _extra.Text.Trim();
            _instance.Save();
            Close();
        }
    }
}
