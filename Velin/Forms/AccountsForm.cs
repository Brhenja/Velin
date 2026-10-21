using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Cuentas del launcher: offline y Microsoft (premium). La de Microsoft necesita un Client ID de Azure aprobado por Mojang.
    public class AccountsForm : VelinFormBase
    {
        private sealed class Row
        {
            public Account Account { get; }
            private readonly bool _isDefault;
            public Row(Account a, bool isDefault) { Account = a; _isDefault = isDefault; }
            public override string ToString() => (_isDefault ? "★ " : "") + Account;
        }

        private readonly ListBox _list = new();
        private readonly Button _addOffline = new();
        private readonly Button _addMicrosoft = new();
        private readonly Button _remove = new();
        private readonly Button _makeDefault = new();

        public AccountsForm() : base(Localization.T("acc_title"))
        {
            LockSize(460, 362);
            StartPosition = FormStartPosition.CenterParent;

            _list.SetBounds(12, 12, 430, 150);
            _list.SelectedIndexChanged += (s, e) => UpdateButtons();
            Content.Controls.Add(_list);

            _addOffline.Text = Localization.T("acc_add_offline");
            _addOffline.SetBounds(12, 214, 140, 32);
            _addOffline.Click += (s, e) => AddOffline();
            Content.Controls.Add(_addOffline);

            _addMicrosoft.Text = Localization.T("acc_add_microsoft");
            _addMicrosoft.SetBounds(12, 172, 430, 34);
            _addMicrosoft.Click += (s, e) => AddMicrosoft();
            Content.Controls.Add(_addMicrosoft);

            _remove.Text = Localization.T("acc_remove");
            _remove.SetBounds(158, 214, 122, 32);
            _remove.Click += (s, e) => RemoveSelected();
            Content.Controls.Add(_remove);

            _makeDefault.Text = Localization.T("acc_make_default");
            _makeDefault.SetBounds(286, 214, 156, 32);
            _makeDefault.Click += (s, e) => MakeDefault();
            Content.Controls.Add(_makeDefault);

            var hintDefault = new Label { Text = Localization.T("acc_default_hint"), AutoSize = false };
            hintDefault.SetBounds(12, 254, 430, 18);
            hintDefault.Font = new Font("Segoe UI", 7.5f, FontStyle.Italic);
            Content.Controls.Add(hintDefault);

            // Aviso mientras la app de Velin todavía no tiene su Client ID aprobado por Mojang.
            var settings0 = SettingsManager.Load();
            var info = new Label { AutoSize = false };
            info.Font = new Font("Segoe UI", 7.5f, FontStyle.Italic);
            info.SetBounds(12, 276, 430, 34);
            info.Text = MicrosoftAuth.IsConfigured(settings0.MicrosoftClientId) ? Localization.T("acc_ms_hint") : Localization.T("acc_ms_unavailable");
            Content.Controls.Add(info);

            var close = new Button { Text = Localization.T("btn_close"), DialogResult = DialogResult.OK };
            close.SetBounds(332, 318, 110, 32);
            Content.Controls.Add(close);
            AcceptButton = close;

            Reload();
            Theme.Apply(this);
        }

        private Account? Selected => (_list.SelectedItem as Row)?.Account;

        private void Reload()
        {
            var settings = SettingsManager.Load();
            var selectedId = Selected?.Id;
            _list.Items.Clear();
            foreach (var a in AccountManager.LoadAll())
            {
                var idx = _list.Items.Add(new Row(a, a.Id == settings.DefaultAccountId));
                if (a.Id == selectedId) _list.SelectedIndex = idx;
            }
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            _remove.Enabled = Selected != null;
            _makeDefault.Enabled = Selected != null;
        }

        private void AddOffline()
        {
            using var dlg = new PromptDialog(Localization.T("acc_add_offline"), Localization.T("acc_offline_prompt"), SettingsManager.Load().DefaultUsername);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var name = dlg.Value;
            if (!Regex.IsMatch(name, "^[A-Za-z0-9_]{1,16}$"))
            {
                MessageBox.Show(this, Localization.T("acc_invalid_name"), "Velin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var acc = AccountManager.AddOffline(name);
            EnsureDefault(acc);
            Reload();
        }

        private void AddMicrosoft()
        {
            var clientId = MicrosoftAuth.ResolveClientId(SettingsManager.Load().MicrosoftClientId);
            if (clientId.Length == 0)
            {
                MessageBox.Show(this, Localization.T("acc_ms_unavailable"), "Velin", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MicrosoftAuthResult? result = null;
            using (var browser = new BrowserLoginForm(clientId))
            {
                if (browser.ShowDialog(this) == DialogResult.OK) result = browser.Result;
                else if (browser.UseDeviceCode)
                {
                    using var code = new DeviceCodeForm(clientId);
                    if (code.ShowDialog(this) == DialogResult.OK) result = code.Result;
                }
            }
            if (result == null) return;

            try
            {
                var acc = AccountManager.AddOrUpdateMicrosoft(result);
                EnsureDefault(acc);
                Reload();
                MessageBox.Show(this, Localization.T("acc_added", acc.Username), "Velin");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localization.T("error_operation") + "\n\n" + ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // La primera cuenta que se agrega pasa a ser la predeterminada.
        private static void EnsureDefault(Account acc)
        {
            var settings = SettingsManager.Load();
            if (string.IsNullOrEmpty(settings.DefaultAccountId) || AccountManager.Get(settings.DefaultAccountId) == null)
            {
                settings.DefaultAccountId = acc.Id;
                SettingsManager.Save(settings);
            }
        }

        private void MakeDefault()
        {
            var acc = Selected;
            if (acc == null) return;
            var settings = SettingsManager.Load();
            settings.DefaultAccountId = acc.Id;
            SettingsManager.Save(settings);
            Reload();
        }

        private void RemoveSelected()
        {
            var acc = Selected;
            if (acc == null) return;
            if (MessageBox.Show(this, Localization.T("acc_confirm_remove", acc.ToString()), "Velin", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            AccountManager.Remove(acc.Id);
            var settings = SettingsManager.Load();
            if (settings.DefaultAccountId == acc.Id)
            {
                settings.DefaultAccountId = "";
                SettingsManager.Save(settings);
            }
            Reload();
        }
    }
}
