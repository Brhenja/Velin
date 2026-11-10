using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Actualizar un modpack de Modrinth instalado a otra versión, conservando mundos y configuración.
    public class ModpackUpdateForm : VelinFormBase
    {
        private readonly Instance _instance;
        private readonly ComboBox _versions = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Label _info = new() { AutoSize = false };
        private readonly Label _note = new() { AutoSize = false };
        private readonly Label _status = new() { AutoSize = false };
        private readonly VelinProgressBar _progress = new() { Minimum = 0, Maximum = 100, Visible = false };
        private readonly Button _update = new();
        private readonly Button _close = new();

        public bool Updated { get; private set; }

        private sealed class Item
        {
            public ContentVersion Version { get; }
            public bool Installed { get; }
            public Item(ContentVersion v, bool installed) { Version = v; Installed = installed; }
            public override string ToString() => Version.Name + (Installed ? "  ✓" : "");
        }

        public ModpackUpdateForm(Instance instance) : base(Localization.T("mpu_title", instance.Name))
        {
            _instance = instance;
            LockSize(520, 330);
            StartPosition = FormStartPosition.CenterParent;

            _info.Text = Localization.T("mpu_installed", string.IsNullOrEmpty(instance.ModpackVersionName) ? "?" : instance.ModpackVersionName);
            _info.SetBounds(12, 12, 496, 22);
            _versions.SetBounds(12, 40, 496, 24);
            _versions.Enabled = false;
            _note.Text = Localization.T("mpu_note");
            _note.SetBounds(12, 74, 496, 78);
            _status.SetBounds(12, 160, 496, 40);
            _progress.SetBounds(12, 206, 496, 16);
            _update.Text = Localization.T("mpu_update");
            _update.SetBounds(12, 244, 220, 36);
            _update.Enabled = false;
            _update.Click += async (s, e) => await UpdateAsync();
            _close.Text = Localization.T("btn_close");
            _close.SetBounds(398, 244, 110, 36);
            _close.Click += (s, e) => Close();
            foreach (Control c in new Control[] { _info, _versions, _note, _status, _progress, _update, _close }) Content.Controls.Add(c);

            Theme.Apply(this);
            _note.Font = new System.Drawing.Font("Segoe UI", 8f, System.Drawing.FontStyle.Italic);
            Shown += async (s, e) => await LoadVersionsAsync();
        }

        private async Task LoadVersionsAsync()
        {
            _status.Text = Localization.T("mpu_loading");
            try
            {
                var versions = await ContentSources.GetVersionsAsync(ContentSource.Modrinth, ContentType.ModPack, _instance.ModpackProjectId, null);
                if (IsDisposed) return;
                foreach (var v in versions.Take(40)) _versions.Items.Add(new Item(v, v.Id == _instance.ModpackVersionId));
                if (_versions.Items.Count > 0)
                {
                    // La más nueva que no sea la instalada queda elegida.
                    var firstOther = _versions.Items.Cast<Item>().ToList().FindIndex(i => !i.Installed);
                    _versions.SelectedIndex = firstOther >= 0 ? firstOther : 0;
                    _versions.Enabled = true;
                    _update.Enabled = true;
                    _status.Text = "";
                }
                else _status.Text = Localization.T("mp_no_file");
            }
            catch (Exception ex) { if (!IsDisposed) _status.Text = Localization.T("error_operation") + " " + ex.Message; }
        }

        private async Task UpdateAsync()
        {
            if (_versions.SelectedItem is not Item item) return;
            if (item.Installed &&
                MessageBox.Show(this, Localization.T("mpu_same_version"), "Velin", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            _update.Enabled = false; _versions.Enabled = false; _close.Enabled = false;
            _progress.Visible = true; _progress.Value = 0;
            var warnings = new List<string>();
            try
            {
                var progress = new Progress<string>(m => { if (!IsDisposed) _status.Text = m; });
                var percent = new Progress<double>(p => { if (!IsDisposed) _progress.Value = Math.Clamp((int)(p * 100), 0, 100); });
                await ContentSources.UpdateModpackAsync(_instance, item.Version, progress, percent, w => warnings.Add(w));
                Updated = true;
                MessageBox.Show(this, Localization.T("mpu_done", item.Version.Name), "Velin", MessageBoxButtons.OK, MessageBoxIcon.Information);
                foreach (var w in warnings) MessageBox.Show(this, w, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localization.T("error_operation") + "\n\n" + ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _status.Text = "";
            }
            finally
            {
                if (!IsDisposed) { _progress.Visible = false; _update.Enabled = true; _versions.Enabled = true; _close.Enabled = true; }
            }
        }
    }
}
