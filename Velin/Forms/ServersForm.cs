using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Lista de servidores de una instancia (servers.dat), con estado en vivo y entrada directa.
    public class ServersForm : VelinFormBase
    {
        private sealed class Row
        {
            public ServerEntry Entry { get; }
            public ServerStatus? Status { get; set; }
            public bool Direct { get; set; }
            public Row(ServerEntry e) { Entry = e; }
            public string Card
            {
                get
                {
                    string state = Status == null ? Localization.T("srv_checking")
                        : Status.Online ? $"{Status.Players}/{Status.MaxPlayers} · {Status.LatencyMs} ms · {Status.Version}"
                        : Localization.T("srv_offline");
                    return (Direct ? "▶ " : "") + Entry.Name + "\n" + Entry.Ip + " · " + state;
                }
            }
        }

        private readonly Instance _instance;
        private readonly List<ServerEntry> _servers = new();
        private readonly ListBox _list = new();
        private readonly Button _add = new(), _edit = new(), _remove = new(), _refresh = new(), _direct = new();
        private readonly Label _status = new();
        private int _pingToken;

        public ServersForm(Instance instance) : base(Localization.T("srv_title", instance.Name))
        {
            _instance = instance;
            LockSize(520, 420);
            StartPosition = FormStartPosition.CenterParent;

            _list.ItemHeight = 38;
            _list.DisplayMember = "Card";
            _list.SetBounds(12, 12, 496, 250);
            _list.SelectedIndexChanged += (s, e) => UpdateButtons();
            Content.Controls.Add(_list);

            Place(_add, "srv_add", 12, 272, 110, () => AddOrEdit(null));
            Place(_edit, "srv_edit", 128, 272, 110, () => AddOrEdit(Selected));
            Place(_remove, "srv_remove", 244, 272, 110, RemoveSelected);
            Place(_refresh, "srv_refresh", 360, 272, 148, () => _ = PingAllAsync());
            Place(_direct, "srv_direct", 12, 312, 496, ToggleDirect);

            _status.AutoSize = false;
            _status.Font = new Font("Segoe UI", 8f, FontStyle.Italic);
            _status.SetBounds(12, 354, 496, 56);
            Content.Controls.Add(_status);

            Load += async (s, e) => { LoadList(); await PingAllAsync(); };
            FormClosing += (s, e) => _pingToken++;
            Theme.Apply(this);
        }

        private void Place(Button b, string key, int x, int y, int w, Action onClick)
        {
            b.Text = Localization.T(key);
            b.SetBounds(x, y, w, 32);
            b.Click += (s, e) => onClick();
            Content.Controls.Add(b);
        }

        private Row? SelectedRow => _list.SelectedItem as Row;
        private ServerEntry? Selected => SelectedRow?.Entry;

        private void LoadList()
        {
            _servers.Clear();
            try { _servers.AddRange(ServerListService.Load(_instance)); }
            catch (Exception ex) { _status.Text = Localization.T("srv_read_error", ex.Message); }
            Rebuild(null);
        }

        private void Rebuild(ServerEntry? select)
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var s in _servers)
            {
                var idx = _list.Items.Add(new Row(s) { Direct = IsDirect(s) });
                if (ReferenceEquals(s, select)) _list.SelectedIndex = idx;
            }
            _list.EndUpdate();
            if (_status.Text.Length == 0 || !_status.Text.StartsWith("⚠"))
                _status.Text = _servers.Count == 0 ? Localization.T("srv_empty") : Localization.T("srv_hint");
            UpdateButtons();
        }

        private bool IsDirect(ServerEntry s) =>
            _instance.JoinServer.Length > 0 && string.Equals(_instance.JoinServer.Trim(), s.Ip.Trim(), StringComparison.OrdinalIgnoreCase);

        private void UpdateButtons()
        {
            var has = Selected != null;
            _edit.Enabled = has; _remove.Enabled = has; _direct.Enabled = has;
            _direct.Text = has && IsDirect(Selected!) ? Localization.T("srv_direct_off") : Localization.T("srv_direct");
        }

        private async Task PingAllAsync()
        {
            var token = ++_pingToken;
            var rows = _list.Items.Cast<Row>().ToList();
            foreach (var r in rows) r.Status = null;
            _list.Invalidate();
            await Task.WhenAll(rows.Select(async r =>
            {
                var st = await ServerPinger.PingAsync(r.Entry.Ip);
                if (token != _pingToken || IsDisposed) return;
                r.Status = st;
                _list.Invalidate();
            }));
        }

        private bool Save()
        {
            try { ServerListService.Save(_instance, _servers); return true; }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localization.T("error_operation") + "\n\n" + ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void AddOrEdit(ServerEntry? existing)
        {
            using var nameDlg = new PromptDialog(Localization.T(existing == null ? "srv_add" : "srv_edit"), Localization.T("srv_name_prompt"), existing?.Name ?? "");
            if (nameDlg.ShowDialog(this) != DialogResult.OK) return;
            using var ipDlg = new PromptDialog(Localization.T(existing == null ? "srv_add" : "srv_edit"), Localization.T("srv_ip_prompt"), existing?.Ip ?? "");
            if (ipDlg.ShowDialog(this) != DialogResult.OK) return;
            var name = nameDlg.Value.Trim(); var ip = ipDlg.Value.Trim();
            if (ip.Length == 0) { MessageBox.Show(this, Localization.T("srv_ip_required")); return; }
            if (name.Length == 0) name = ip;

            ServerEntry entry;
            if (existing == null) { entry = new ServerEntry { Name = name, Ip = ip }; _servers.Add(entry); }
            else
            {
                // Si era el servidor de entrada directa, la entrada directa sigue apuntando al mismo servidor.
                bool wasDirect = IsDirect(existing);
                existing.Name = name; existing.Ip = ip; entry = existing;
                if (wasDirect) { _instance.JoinServer = ip; _instance.Save(); }
            }
            if (!Save()) return;
            Rebuild(entry);
            _ = PingAllAsync();
        }

        private void RemoveSelected()
        {
            var s = Selected; if (s == null) return;
            if (MessageBox.Show(this, Localization.T("srv_confirm_remove", s.Name), "Velin", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            _servers.Remove(s);
            if (Save()) Rebuild(null);
        }

        private void ToggleDirect()
        {
            var s = Selected; if (s == null) return;
            _instance.JoinServer = IsDirect(s) ? "" : s.Ip.Trim();
            _instance.Save();
            Rebuild(s);
        }
    }
}
