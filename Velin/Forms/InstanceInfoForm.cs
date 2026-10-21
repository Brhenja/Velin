using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    public class InstanceInfoForm : VelinFormBase
    {
        private readonly Instance _instance;
        private readonly NumericUpDown _ram = new() { Minimum = 512, Maximum = 32768, Increment = 512 };
        private readonly Label _ramHint = new();
        private readonly TextBox _username = new();
        private readonly Label _sizeLabel = new();
        private readonly Button _btnBackup = new();

        public InstanceInfoForm(Instance instance) : base(Localization.T("info_title", instance.Name))
        {
            _instance = instance;
            LockSize(420, 520);
            StartPosition = FormStartPosition.CenterParent;

            int y = 12;
            AddReadOnlyRow(Localization.T("lbl_mc_version"), instance.McVersion, ref y);
            AddReadOnlyRow(Localization.T("lbl_loader"), instance.Loader.ToString(), ref y);
            AddReadOnlyRow(Localization.T("info_loader_version"), instance.LoaderVersion ?? "—", ref y);

            Content.Controls.Add(new Label { Text = Localization.T("lbl_ram"), AutoSize = true, Location = new Point(12, y) });
            y += 22;
            _ram.Value = Math.Clamp(instance.RamMB, 512, 32768);
            _ram.SetBounds(12, y, 150, 24);
            Content.Controls.Add(_ram);
            y += 28;

            // Ayuda según la RAM de esta PC (con un ⚠ si te pasás de lo recomendado).
            _ramHint.AutoSize = false;
            _ramHint.Font = new Font("Segoe UI", 7.5f, FontStyle.Italic);
            _ramHint.Text = RamHint.Build((int)_ram.Value);
            _ramHint.SetBounds(12, y, 396, 30);
            Content.Controls.Add(_ramHint);
            _ram.ValueChanged += (s, e) => _ramHint.Text = RamHint.Build((int)_ram.Value);
            y += 36;

            Content.Controls.Add(new Label { Text = Localization.T("lbl_username"), AutoSize = true, Location = new Point(12, y) });
            y += 22;
            _username.Text = instance.Username;
            _username.SetBounds(12, y, 200, 24);
            Content.Controls.Add(_username);
            y += 36;

            AddReadOnlyRow(Localization.T("info_folder"), instance.InstanceDir, ref y, small: true);

            _sizeLabel.AutoSize = true;
            _sizeLabel.Location = new Point(12, y);
            _sizeLabel.Text = Localization.T("info_calculating");
            Content.Controls.Add(_sizeLabel);
            y += 24;

            // Estadísticas de uso.
            var played = Instance.FormatPlayTime(instance.TotalPlaySeconds);
            if (played.Length == 0) played = "0 min";
            var last = instance.LastPlayedUtc?.ToLocalTime().ToString("g") ?? Localization.T("info_never");
            Content.Controls.Add(new Label
            {
                Text = Localization.T("info_played", played) + "   ·   " + Localization.T("info_last_played", last),
                AutoSize = true,
                Location = new Point(12, y)
            });
            y += 28;

            var options = new Button { Text = Localization.T("btn_launch_options") };
            options.SetBounds(12, y, 260, 28);
            options.Click += (s, e) =>
            {
                using var form = new LaunchOptionsForm(_instance);
                form.ShowDialog(this);
            };
            Content.Controls.Add(options);
            y += 34;

            _btnBackup.Text = Localization.T("btn_backup_now");
            _btnBackup.SetBounds(12, y, 200, 28);
            _btnBackup.Click += async (s, e) => await BackupNowAsync();
            Content.Controls.Add(_btnBackup);

            var backups = new Button { Text = Localization.T("btn_open_backups") };
            backups.SetBounds(218, y, 120, 28);
            backups.Click += (s, e) => OpenBackups();
            Content.Controls.Add(backups);
            y += 40;

            var save = new Button { Text = Localization.T("btn_save") };
            save.SetBounds(12, y, 110, 32);
            save.Click += OnSave;
            Content.Controls.Add(save);

            var close = new Button { Text = Localization.T("btn_cancel") };
            close.SetBounds(130, y, 110, 32);
            close.Click += (s, e) => Close();
            Content.Controls.Add(close);

            Load += async (s, e) => _sizeLabel.Text = Localization.T("info_size", await CalculateSizeAsync());

            Theme.Apply(this);
        }

        private void AddReadOnlyRow(string label, string value, ref int y, bool small = false)
        {
            Content.Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(12, y) });
            y += 20;
            var valueLabel = new Label
            {
                Text = value,
                AutoSize = false,
                Font = small ? new Font("Segoe UI", 7.5f) : Font
            };
            valueLabel.SetBounds(12, y, 396, small ? 32 : 20);
            Content.Controls.Add(valueLabel);
            y += small ? 36 : 26;
        }

        private Task<string> CalculateSizeAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(_instance.InstanceDir)) return "0 MB";
                    long bytes = Directory.EnumerateFiles(_instance.InstanceDir, "*", SearchOption.AllDirectories)
                        .Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });
                    double mb = bytes / 1024.0 / 1024.0;
                    return mb >= 1024 ? $"{mb / 1024.0:0.##} GB" : $"{mb:0.#} MB";
                }
                catch { return "—"; }
            });
        }

        // Backup manual: funciona siempre, esté o no activado el automático en Configuración.
        private async Task BackupNowAsync()
        {
            _btnBackup.Enabled = false;
            try
            {
                var path = await BackupService.BackupWorldsAsync(_instance, "manual");
                MessageBox.Show(this, path == null ? Localization.T("backup_no_worlds") : Localization.T("backup_done", path), "Velin");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localization.T("error_operation") + "\n\n" + ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnBackup.Enabled = true;
            }
        }

        private void OpenBackups()
        {
            var dir = BackupService.BackupDirFor(_instance);
            Directory.CreateDirectory(dir);
            using var form = new VaultForm(dir, Localization.T("btn_open_backups") + " — " + _instance.Name);
            form.ShowDialog(this);
        }

        private void OnSave(object? sender, EventArgs e)
        {
            _instance.RamMB = (int)_ram.Value;
            _instance.Username = string.IsNullOrWhiteSpace(_username.Text) ? "Player" : _username.Text.Trim();
            _instance.Save();
            Close();
        }
    }
}
