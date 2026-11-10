using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Gestor de skins de una cuenta.
    //  - Premium: muestra la skin actual, sube una de la biblioteca (o un .png) y restaura la de por defecto.
    //  - Offline: Mojang no sirve skins para cuentas offline; la biblioteca se usa para copiar la skin a una instancia
    //    en el formato del mod CustomSkinLoader (que es el que la muestra dentro del juego).
    public class SkinsForm : VelinFormBase
    {
        private const int SkinScale = 5;

        private readonly Account _account;
        private readonly CancellationTokenSource _cts = new();

        private readonly Label _lblCurrent = new() { AutoSize = true };
        private readonly Label _lblSelected = new() { AutoSize = true };
        private readonly Label _currentNote = new() { AutoSize = false };
        private readonly PictureBox _curFront = Preview();
        private readonly PictureBox _curBack = Preview();
        private readonly PictureBox _selFront = Preview();
        private readonly PictureBox _selBack = Preview();

        private readonly ListBox _list = new() { ItemHeight = 26 };
        private readonly Button _btnAdd = new();
        private readonly Button _btnSaveCurrent = new();
        private readonly Button _btnRename = new();
        private readonly Button _btnDelete = new();
        private readonly ComboBox _model = new() { DropDownStyle = ComboBoxStyle.DropDownList };

        private readonly ComboBox _instances = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Button _btnApply = new();
        private readonly Button _btnReset = new();
        private readonly Label _hint = new() { AutoSize = false };
        private readonly Label _status = new() { AutoSize = false };

        private SkinInfo? _activeSkin;
        private byte[]? _activePng;
        private bool _loadingModel;

        private static PictureBox Preview() => new() { SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };

        public SkinsForm(Account account) : base(Localization.T("skins_title", account.Username))
        {
            _account = account;
            LockSize(700, 440);
            StartPosition = FormStartPosition.CenterParent;
            bool premium = account.IsMicrosoft;

            _lblCurrent.Text = Localization.T("skins_current");
            _lblCurrent.Location = new Point(12, 12);
            _lblSelected.Text = Localization.T("skins_selected");
            _lblSelected.Location = new Point(200, 12);
            Content.Controls.Add(_lblCurrent);
            Content.Controls.Add(_lblSelected);

            _curFront.SetBounds(12, 36, 80, 160);   _curBack.SetBounds(96, 36, 80, 160);
            _selFront.SetBounds(200, 36, 80, 160);  _selBack.SetBounds(284, 36, 80, 160);
            foreach (var p in new[] { _curFront, _curBack, _selFront, _selBack }) Content.Controls.Add(p);

            _currentNote.SetBounds(12, 36, 164, 160);
            _currentNote.TextAlign = ContentAlignment.MiddleCenter;
            _currentNote.Visible = false;
            Content.Controls.Add(_currentNote);
            _currentNote.BringToFront();

            _list.SetBounds(380, 12, 308, 184);
            _list.SelectedIndexChanged += (s, e) => OnSelectionChanged();
            Content.Controls.Add(_list);

            _btnAdd.Text = Localization.T("skins_add");
            _btnAdd.SetBounds(380, 204, 152, 28);
            _btnAdd.Click += (s, e) => AddFromFile();
            Content.Controls.Add(_btnAdd);

            _btnSaveCurrent.Text = Localization.T("skins_save_current");
            _btnSaveCurrent.SetBounds(536, 204, 152, 28);
            _btnSaveCurrent.Click += (s, e) => SaveCurrent();
            _btnSaveCurrent.Visible = premium;
            Content.Controls.Add(_btnSaveCurrent);

            _btnRename.Text = Localization.T("skins_rename");
            _btnRename.SetBounds(380, 238, 100, 28);
            _btnRename.Click += (s, e) => RenameSelected();
            Content.Controls.Add(_btnRename);

            _btnDelete.Text = Localization.T("skins_delete");
            _btnDelete.SetBounds(484, 238, 100, 28);
            _btnDelete.Click += (s, e) => DeleteSelected();
            Content.Controls.Add(_btnDelete);

            _model.Items.AddRange(new object[] { Localization.T("skins_model_classic"), Localization.T("skins_model_slim") });
            _model.SetBounds(588, 240, 100, 24);
            _model.SelectedIndexChanged += (s, e) => OnModelChanged();
            Content.Controls.Add(_model);

            if (premium)
            {
                _btnApply.Text = Localization.T("skins_apply");
                _btnApply.SetBounds(12, 204, 352, 34);
                _btnReset.Text = Localization.T("skins_reset");
                _btnReset.SetBounds(12, 244, 352, 30);
                _btnReset.Click += async (s, e) => await ResetAsync();
                Content.Controls.Add(_btnReset);
            }
            else
            {
                foreach (var inst in InstanceManager.LoadAll()) _instances.Items.Add(inst);
                if (_instances.Items.Count > 0) _instances.SelectedIndex = 0;
                _instances.SetBounds(12, 204, 352, 24);
                Content.Controls.Add(_instances);
                _btnApply.Text = Localization.T("skins_apply_offline");
                _btnApply.SetBounds(12, 236, 352, 34);
            }
            _btnApply.Click += async (s, e) => await ApplyAsync();
            Content.Controls.Add(_btnApply);

            _hint.Text = Localization.T(premium ? "skins_hint_premium" : "skins_hint_offline");
            _hint.Font = Theme.PlainFont(8f, FontStyle.Italic);
            _hint.SetBounds(12, 282, 676, 64);
            Content.Controls.Add(_hint);

            _status.SetBounds(12, 352, 560, 36);
            Content.Controls.Add(_status);

            var close = new Button { Text = Localization.T("btn_close"), DialogResult = DialogResult.OK };
            close.SetBounds(578, 360, 110, 32);
            Content.Controls.Add(close);
            AcceptButton = close;

            Theme.Apply(this);
            _hint.Font = Theme.PlainFont(8f, FontStyle.Italic);

            ReloadList(null);
            UpdateButtons();
            Shown += async (s, e) => await LoadCurrentAsync();
            FormClosed += (s, e) =>
            {
                _cts.Cancel();
                _cts.Dispose();
                ClearImage(_curFront); ClearImage(_curBack); ClearImage(_selFront); ClearImage(_selBack);
            };
        }

        // --- Skin actual (premium) ---------------------------------------------------------------

        private async Task<string> GetTokenAsync()
        {
            var clientId = MicrosoftAuth.ResolveClientId(SettingsManager.Load().MicrosoftClientId);
            var session = await AccountManager.GetSessionAsync(_account, new Instance(), clientId, _cts.Token);
            if (string.IsNullOrEmpty(session.AccessToken) || session.AccessToken == "0")
                throw new SkinException("No hay una sesión de Microsoft válida. Volvé a iniciar sesión desde Cuentas.");
            return session.AccessToken;
        }

        private async Task LoadCurrentAsync()
        {
            if (!_account.IsMicrosoft)
            {
                _currentNote.Text = Localization.T("skins_current_offline");
                _currentNote.Visible = true;
                return;
            }

            SetBusy(true, Localization.T("skins_loading"));
            try
            {
                var token = await GetTokenAsync();
                var info = await MojangSkinService.GetActiveSkinAsync(token, _cts.Token);
                _activeSkin = info;
                _activePng = null;
                if (info == null)
                {
                    ShowCurrentNote(Localization.T("skins_current_default"));
                }
                else
                {
                    var png = await MojangSkinService.DownloadSkinAsync(info.Url, _cts.Token);
                    _activePng = png;
                    SkinFaceCache.Store(_account.Uuid, png);
                    ShowCurrent(png, info.Slim);
                }
                SetBusy(false, "");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                SetBusy(false, Localization.T("skins_error", ex.Message));
            }
        }

        private void ShowCurrentNote(string text)
        {
            ClearImage(_curFront); ClearImage(_curBack);
            _currentNote.Text = text;
            _currentNote.Visible = true;
        }

        private void ShowCurrent(byte[] png, bool slim)
        {
            using var skin = SkinRenderer.Load(png);
            if (skin == null) { ShowCurrentNote(Localization.T("skins_unreadable")); return; }
            _currentNote.Visible = false;
            SetImage(_curFront, SkinRenderer.RenderFront(skin, slim, SkinScale));
            SetImage(_curBack, SkinRenderer.RenderBack(skin, slim, SkinScale));
        }

        // --- Biblioteca ---------------------------------------------------------------------------

        private SkinEntry? Selected => _list.SelectedItem as SkinEntry;

        private void ReloadList(string? selectId)
        {
            _list.Items.Clear();
            foreach (var e in SkinLibrary.List()) _list.Items.Add(e);
            if (selectId != null)
                for (int i = 0; i < _list.Items.Count; i++)
                    if (((SkinEntry)_list.Items[i]).Id == selectId) { _list.SelectedIndex = i; break; }
            if (_list.SelectedIndex < 0 && _list.Items.Count > 0) _list.SelectedIndex = 0;
            OnSelectionChanged();
        }

        private void OnSelectionChanged()
        {
            var entry = Selected;
            _loadingModel = true;
            _model.SelectedIndex = entry?.Slim == true ? 1 : 0;
            _loadingModel = false;

            ClearImage(_selFront); ClearImage(_selBack);
            if (entry != null)
            {
                var png = SkinLibrary.ReadPng(entry);
                using var skin = png == null ? null : SkinRenderer.Load(png);
                if (skin != null)
                {
                    SetImage(_selFront, SkinRenderer.RenderFront(skin, entry.Slim, SkinScale));
                    SetImage(_selBack, SkinRenderer.RenderBack(skin, entry.Slim, SkinScale));
                }
            }
            UpdateButtons();
        }

        private void OnModelChanged()
        {
            if (_loadingModel) return;
            var entry = Selected;
            if (entry == null) return;
            SkinLibrary.Update(entry.Id, slim: _model.SelectedIndex == 1);
            ReloadList(entry.Id);
        }

        private void UpdateButtons()
        {
            bool has = Selected != null;
            _btnRename.Enabled = has;
            _btnDelete.Enabled = has;
            _model.Enabled = has;
            _btnApply.Enabled = has && (_account.IsMicrosoft || _instances.Items.Count > 0);
            _btnSaveCurrent.Enabled = _activePng != null;
        }

        private void AddFromFile()
        {
            using var dlg = new OpenFileDialog { Filter = "Skin (*.png)|*.png", Title = Localization.T("skins_add") };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var png = File.ReadAllBytes(dlg.FileName);
                var error = SkinLibrary.ValidatePng(png);
                if (error != null) { MessageBox.Show(this, error, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

                using var prompt = new PromptDialog(Localization.T("skins_name_title"), Localization.T("skins_name_label"), Path.GetFileNameWithoutExtension(dlg.FileName));
                if (prompt.ShowDialog(this) != DialogResult.OK) return;

                using var skin = SkinRenderer.Load(png);
                bool slim = skin != null && SkinRenderer.GuessSlim(skin);
                var entry = SkinLibrary.Add(prompt.Value, png, slim);
                ReloadList(entry.Id);
                _status.Text = Localization.T("skins_added", entry.Name);
            }
            catch (Exception ex) { _status.Text = Localization.T("skins_error", ex.Message); }
        }

        private void SaveCurrent()
        {
            if (_activePng == null) return;
            using var prompt = new PromptDialog(Localization.T("skins_name_title"), Localization.T("skins_name_label"), _account.Username);
            if (prompt.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var entry = SkinLibrary.Add(prompt.Value, _activePng, _activeSkin?.Slim ?? false);
                ReloadList(entry.Id);
                _status.Text = Localization.T("skins_added", entry.Name);
            }
            catch (Exception ex) { _status.Text = Localization.T("skins_error", ex.Message); }
        }

        private void RenameSelected()
        {
            var entry = Selected;
            if (entry == null) return;
            using var prompt = new PromptDialog(Localization.T("skins_name_title"), Localization.T("skins_name_label"), entry.Name);
            if (prompt.ShowDialog(this) != DialogResult.OK) return;
            SkinLibrary.Update(entry.Id, name: prompt.Value);
            ReloadList(entry.Id);
        }

        private void DeleteSelected()
        {
            var entry = Selected;
            if (entry == null) return;
            if (MessageBox.Show(this, Localization.T("skins_confirm_delete", entry.Name), "Velin", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            SkinLibrary.Remove(entry.Id);
            ReloadList(null);
        }

        // --- Aplicar ------------------------------------------------------------------------------

        private async Task ApplyAsync()
        {
            var entry = Selected;
            if (entry == null) return;
            var png = SkinLibrary.ReadPng(entry);
            if (png == null) { _status.Text = Localization.T("skins_unreadable"); return; }

            if (!_account.IsMicrosoft) { ApplyOffline(entry, png); return; }

            SetBusy(true, Localization.T("skins_uploading"));
            try
            {
                var token = await GetTokenAsync();
                await MojangSkinService.UploadSkinAsync(token, png, entry.Slim, _cts.Token);
                SkinFaceCache.Store(_account.Uuid, png);
                _activePng = png;
                _activeSkin = new SkinInfo { Slim = entry.Slim };
                ShowCurrent(png, entry.Slim);
                SetBusy(false, Localization.T("skins_applied", entry.Name));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                SetBusy(false, Localization.T("skins_error", ex.Message));
            }
        }

        // Cuentas offline: se copia a <instancia>\minecraft\CustomSkinLoader\LocalSkin\skins\<usuario>.png
        private void ApplyOffline(SkinEntry entry, byte[] png)
        {
            if (_instances.SelectedItem is not Instance inst) return;
            try
            {
                var target = SkinLibrary.CustomSkinLoaderPath(inst.GameDir, _account.Username);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllBytes(target, png);
                _status.Text = Localization.T("skins_offline_done", entry.Name, inst.Name);
            }
            catch (Exception ex) { _status.Text = Localization.T("skins_error", ex.Message); }
        }

        private async Task ResetAsync()
        {
            if (MessageBox.Show(this, Localization.T("skins_confirm_reset"), "Velin", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            SetBusy(true, Localization.T("skins_uploading"));
            try
            {
                var token = await GetTokenAsync();
                await MojangSkinService.ResetSkinAsync(token, _cts.Token);
                _activeSkin = null;
                _activePng = null;
                try { File.Delete(Path.Combine(SkinFaceCache.Dir, MojangSkinService.NormalizeUuid(_account.Uuid) + ".png")); } catch { }
                ShowCurrentNote(Localization.T("skins_current_default"));
                SetBusy(false, Localization.T("skins_reset_done"));
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (IsDisposed) return;
                SetBusy(false, Localization.T("skins_error", ex.Message));
            }
        }

        // --- Utilidades ---------------------------------------------------------------------------

        private void SetBusy(bool busy, string message)
        {
            _status.Text = message;
            _btnApply.Enabled = !busy;
            _btnReset.Enabled = !busy;
            _btnAdd.Enabled = !busy;
            if (!busy) UpdateButtons();
        }

        private static void SetImage(PictureBox box, Image img)
        {
            var old = box.Image;
            box.Image = img;
            old?.Dispose();
        }

        private static void ClearImage(PictureBox box)
        {
            var old = box.Image;
            box.Image = null;
            old?.Dispose();
        }
    }
}
