using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class MainForm : VelinFormBase
    {
        private static readonly string[] SortModes = { "name", "lastPlayed", "playTime" };

        private readonly ComboBox _sort = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _group = new() { DropDownStyle = ComboBoxStyle.DropDownList };
        private bool _loadingGroups;
        private readonly Button _btnAccounts = new();
        private readonly Button _btnCancel = new();
        private CancellationTokenSource? _launchCts;
        private Process? _launchProcess;
        private readonly Label _accountLabel = new();
        private readonly ListBox _list = new();
        private readonly ContextMenuStrip _menu = new();
        private readonly Button _btnNew = new();
        private readonly Button _btnImport = new();
        private readonly Button _btnPlay = new();
        private readonly Button _btnMods = new();
        private readonly Button _btnCheckUpdates = new();
        private readonly Button _btnDelete = new();
        private readonly Button _btnMore = new();
        private readonly Button _btnOpenFolder = new();
        private readonly Button _btnInfo = new();
        private readonly Button _btnVault = new();
        private readonly Button _btnSettings = new();
        private readonly VelinProgressBar _progress = new() { Visible = false };
        private readonly Label _status = new();
        private readonly PictureBox _gifCorner = new() { SizeMode = PictureBoxSizeMode.Zoom };

        public MainForm() : base(Localization.T("main_title"))
        {
            LockSize(660, 530);
            StartPosition = FormStartPosition.CenterScreen;

            // Orden de la lista (se recuerda entre sesiones).
            _sort.Items.AddRange(new object[] { Localization.T("sort_name"), Localization.T("sort_last"), Localization.T("sort_time") });
            var savedSort = Array.IndexOf(SortModes, SettingsManager.Load().InstanceSort);
            _sort.SelectedIndex = savedSort >= 0 ? savedSort : 0;
            _sort.SetBounds(12, 12, 146, 24);
            _sort.SelectedIndexChanged += (s, e) => OnSortChanged();
            Content.Controls.Add(_sort);

            // Filtro por grupo/categoría de instancias.
            _group.SetBounds(166, 12, 146, 24);
            _group.SelectedIndexChanged += (s, e) => OnGroupChanged();
            Content.Controls.Add(_group);

            // Cada instancia se muestra como una tarjeta de dos líneas: nombre + versión · loader · tiempo jugado.
            _list.ItemHeight = 38;
            _list.SetBounds(12, 42, 300, 340);
            _list.SelectedIndexChanged += (s, e) => UpdateButtons();
            _list.MouseDown += ListMouseDown;
            Content.Controls.Add(_list);

            _accountLabel.AutoSize = false;
            _accountLabel.SetBounds(12, 388, 300, 20);
            Content.Controls.Add(_accountLabel);

            BuildMenu();
            _list.ContextMenuStrip = _menu;

            _btnNew.Text = Localization.T("btn_new_instance");
            _btnNew.SetBounds(330, 12, 200, 32);
            _btnNew.Click += (s, e) => CreateInstance();
            Content.Controls.Add(_btnNew);

            _btnImport.Text = Localization.T("btn_import");
            _btnImport.SetBounds(536, 12, 94, 32);
            _btnImport.Click += async (s, e) => await ImportAsync();
            Content.Controls.Add(_btnImport);

            _btnPlay.Text = Localization.T("btn_play");
            _btnPlay.SetBounds(330, 52, 300, 32);
            _btnPlay.Click += async (s, e) => await PlayAsync();
            Content.Controls.Add(_btnPlay);

            // Único botón de contenido: mods, modpacks, mundos, packs, shaders, servidores, capturas, backups...
            _btnMods.Text = Localization.T("btn_content");
            _btnMods.SetBounds(330, 92, 300, 32);
            _btnMods.Click += (s, e) => OpenContent();
            Content.Controls.Add(_btnMods);

            _btnCheckUpdates.Text = Localization.T("btn_check_updates");
            _btnCheckUpdates.SetBounds(330, 132, 300, 32);
            _btnCheckUpdates.Click += async (s, e) => await CheckUpdatesAsync();
            Content.Controls.Add(_btnCheckUpdates);

            _btnDelete.Text = Localization.T("btn_delete");
            _btnDelete.SetBounds(330, 172, 147, 32);
            _btnDelete.Click += async (s, e) => await DeleteInstanceAsync();
            Content.Controls.Add(_btnDelete);

            _btnMore.Text = Localization.T("btn_more");
            _btnMore.SetBounds(483, 172, 147, 32);
            _btnMore.Click += (s, e) => _menu.Show(_btnMore, new System.Drawing.Point(0, _btnMore.Height));
            Content.Controls.Add(_btnMore);

            _btnOpenFolder.Text = Localization.T("btn_open_folder");
            _btnOpenFolder.SetBounds(330, 212, 300, 32);
            _btnOpenFolder.Click += (s, e) => OpenInstanceFolder();
            Content.Controls.Add(_btnOpenFolder);

            _btnInfo.Text = Localization.T("btn_info");
            _btnInfo.SetBounds(330, 252, 96, 32);
            _btnInfo.Click += (s, e) => OpenInfo();
            Content.Controls.Add(_btnInfo);

            _btnVault.Text = Localization.T("btn_vault");
            _btnVault.SetBounds(432, 252, 96, 32);
            _btnVault.Click += (s, e) => OpenVault();
            Content.Controls.Add(_btnVault);

            _btnSettings.Text = Localization.T("btn_settings");
            _btnSettings.SetBounds(534, 252, 96, 32);
            _btnSettings.Click += (s, e) => OpenSettings();
            Content.Controls.Add(_btnSettings);

            _btnAccounts.Text = Localization.T("btn_accounts");
            _btnAccounts.SetBounds(330, 292, 300, 32);
            _btnAccounts.Click += (s, e) => OpenAccounts();
            Content.Controls.Add(_btnAccounts);

            // Botón Cancelar: aparece mientras se descarga/instala/carga Minecraft para iniciar.
            _btnCancel.Text = Localization.T("btn_cancel");
            _btnCancel.SetBounds(520, 488, 110, 30);
            _btnCancel.Visible = false;
            _btnCancel.Click += (s, e) => CancelLaunch();
            Content.Controls.Add(_btnCancel);

            _progress.SetBounds(12, 464, 620, 18);
            Content.Controls.Add(_progress);

            _status.SetBounds(12, 486, 500, 40);
            Content.Controls.Add(_status);

            // Mascota animada: sólo decora el tema Flash, en el hueco vacío que queda
            // debajo de los botones.
            _gifCorner.SetBounds(500, 344, 130, 84);
            try
            {
                var gifPath = Path.Combine(AppContext.BaseDirectory, "Assets", "flash_mascot.gif");
                if (File.Exists(gifPath)) _gifCorner.Image = System.Drawing.Image.FromFile(gifPath);
            }
            catch { /* si falla, simplemente no se muestra */ }
            Content.Controls.Add(_gifCorner);
            _gifCorner.BringToFront();

            Load += (s, e) => { Reload(); UpdateGifVisibility(); };

            Theme.Apply(this);
        }

        // Menú de acciones sobre la instancia elegida: clic derecho en la lista o botón "Más".
        private void BuildMenu()
        {
            void Add(string key, Action action)
            {
                var item = new ToolStripMenuItem(Localization.T(key));
                item.Click += (s, e) => action();
                _menu.Items.Add(item);
            }

            Add("btn_info", OpenInfo);
            Add("ctx_content", OpenContent);
            Add("ctx_launch_options", OpenLaunchOptions);
            Add("ctx_change_version", () => _ = ChangeVersionAsync());
            Add("ctx_backups", OpenBackups);
            Add("ctx_console", OpenConsole);
            Add("ctx_group", SetGroup);
            _menu.Items.Add(new ToolStripSeparator());
            Add("ctx_duplicate", () => _ = DuplicateAsync());
            Add("ctx_rename", RenameInstance);
            Add("ctx_export", () => _ = ExportAsync());
            _menu.Items.Add(new ToolStripSeparator());
            Add("btn_delete", () => _ = DeleteInstanceAsync());

            _menu.Opening += (s, e) => { if (Selected == null) e.Cancel = true; };
        }

        private void ListMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            int index = _list.IndexFromPoint(e.Location);
            if (index >= 0) _list.SelectedIndex = index;
        }

        // El GIF animado sólo tiene sentido con la estética "Flash" — con el tema
        // medieval queda oculto.
        private void UpdateGifVisibility()
        {
            _gifCorner.Visible = Theme.Current.Name.Contains("Flash", StringComparison.OrdinalIgnoreCase);
        }

        private void OnSortChanged()
        {
            var settings = SettingsManager.Load();
            settings.InstanceSort = SortModes[Math.Max(0, _sort.SelectedIndex)];
            SettingsManager.Save(settings);
            Reload();
        }

        private void Reload()
        {
            PathService.EnsureAll();
            var selectedName = Selected?.Name;
            var all = InstanceManager.LoadAll();
            var mode = SortModes[Math.Max(0, _sort.SelectedIndex)];
            RebuildGroupFilter(all);
            var groupFilter = SelectedGroupFilter();
            if (groupFilter.Length > 0)
                all = all.Where(i => string.Equals(i.Group, groupFilter, StringComparison.OrdinalIgnoreCase)).ToList();

            IEnumerable<Instance> sorted = mode switch
            {
                "lastPlayed" => all.OrderByDescending(i => i.LastPlayedUtc ?? DateTime.MinValue).ThenBy(i => i.Name),
                "playTime" => all.OrderByDescending(i => i.TotalPlaySeconds).ThenBy(i => i.Name),
                _ => all.OrderBy(i => i.Name)
            };

            _list.BeginUpdate();
            _list.DisplayMember = "ListCard";
            _list.Items.Clear();
            foreach (var inst in sorted) _list.Items.Add(inst);
            _list.EndUpdate();

            if (selectedName != null) SelectByName(selectedName);
            UpdateButtons();
        }

        private string SelectedGroupFilter() =>
            _group.SelectedIndex > 0 ? (_group.SelectedItem?.ToString() ?? "") : "";

        // Rearma la lista de grupos (con "Todos" primero) conservando el filtro elegido.
        private void RebuildGroupFilter(List<Instance> all)
        {
            _loadingGroups = true;
            try
            {
                var wanted = _group.Items.Count > 0 ? SelectedGroupFilter() : SettingsManager.Load().GroupFilter;
                var groups = all.Select(i => i.Group).Where(g => !string.IsNullOrWhiteSpace(g))
                    .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(g => g, StringComparer.OrdinalIgnoreCase).ToList();
                _group.Items.Clear();
                _group.Items.Add(Localization.T("group_all"));
                foreach (var g in groups) _group.Items.Add(g);
                var idx = wanted.Length > 0 ? groups.FindIndex(g => string.Equals(g, wanted, StringComparison.OrdinalIgnoreCase)) + 1 : 0;
                _group.SelectedIndex = idx > 0 ? idx : 0;
            }
            finally { _loadingGroups = false; }
        }

        private void OnGroupChanged()
        {
            if (_loadingGroups) return;
            var settings = SettingsManager.Load();
            settings.GroupFilter = SelectedGroupFilter();
            SettingsManager.Save(settings);
            Reload();
        }

        private void SelectByName(string name)
        {
            foreach (var item in _list.Items)
            {
                if (item is Instance i && i.Name == name) { _list.SelectedItem = item; return; }
            }
        }

        private Instance? Selected => _list.SelectedItem as Instance;

        private void UpdateButtons()
        {
            var enabled = Selected != null;
            _btnPlay.Enabled = enabled;
            _btnMods.Enabled = enabled;
            _btnDelete.Enabled = enabled;
            _btnMore.Enabled = enabled;
            _btnCheckUpdates.Enabled = enabled;
            _btnOpenFolder.Enabled = enabled;
            _btnInfo.Enabled = enabled;
            UpdateAccountLabel();
        }

        // Muestra con qué cuenta se va a jugar la instancia elegida.
        private void UpdateAccountLabel()
        {
            var inst = Selected;
            if (inst == null) { _accountLabel.Text = ""; return; }
            var acc = AccountManager.Resolve(inst, SettingsManager.Load());
            var who = acc != null ? acc.ToString() : inst.Username + " (offline)";
            _accountLabel.Text = Localization.T("lbl_account_current", who);
        }

        private void SetBusy(bool busy)
        {
            var hasSelection = Selected != null;
            _btnNew.Enabled = !busy;
            _btnImport.Enabled = !busy;
            _sort.Enabled = !busy;
            _btnPlay.Enabled = !busy && hasSelection;
            _btnMods.Enabled = !busy && hasSelection;
            _btnDelete.Enabled = !busy && hasSelection;
            _btnMore.Enabled = !busy && hasSelection;
            _btnCheckUpdates.Enabled = !busy && hasSelection;
            _btnOpenFolder.Enabled = !busy && hasSelection;
            _btnInfo.Enabled = !busy && hasSelection;
            _btnAccounts.Enabled = !busy;
            _group.Enabled = !busy;
        }

        // --- Ventanas auxiliares ------------------------------------------------------------

        private void OpenInfo()
        {
            var inst = Selected;
            if (inst == null) return;
            using var form = new InstanceInfoForm(inst);
            form.ShowDialog(this);
            Reload(); // por si cambiaste RAM/usuario
        }

        private void OpenLaunchOptions()
        {
            var inst = Selected;
            if (inst == null) return;
            using var form = new LaunchOptionsForm(inst);
            form.ShowDialog(this);
            UpdateAccountLabel();
        }

        // Panel de contenido: mods, mundos, resource packs, shaders, datapacks, capturas, backups y consola.
        private void OpenContent()
        {
            var inst = Selected;
            if (inst == null) return;
            if (inst.McVersion.Length == 0)
            {
                MessageBox.Show(this, Localization.T("cj_need_version"), "Velin", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using var form = new InstanceContentForm(inst);
            form.ShowDialog(this);
            Reload();
        }

        private void OpenAccounts()
        {
            using var form = new AccountsForm();
            form.ShowDialog(this);
            UpdateAccountLabel();
        }

        private void OpenBackups()
        {
            var inst = Selected;
            if (inst == null) return;
            using var form = new BackupsForm(inst);
            form.ShowDialog(this);
        }

        // Consola en vivo (no modal, sin dueño: queda visible aunque el launcher se oculte).
        private void OpenConsole()
        {
            var inst = Selected;
            if (inst == null) return;
            new LogViewerForm(Localization.T("console_title", inst.Name), GameLauncher.LogPathFor(inst)).Show();
        }

        private void SetGroup()
        {
            var inst = Selected;
            if (inst == null) return;
            using var dlg = new PromptDialog(Localization.T("ctx_group").Replace("...", ""), Localization.T("prompt_group"), inst.Group);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            inst.Group = dlg.Value;
            inst.Save();
            Reload();
            SelectByName(inst.Name);
        }

        // Cambia la versión de Minecraft y/o el loader. Si el backup automático está activado, guarda los mundos antes.
        private async Task ChangeVersionAsync()
        {
            var inst = Selected;
            if (inst == null) return;

            using var form = new ChangeVersionForm(inst);
            if (form.ShowDialog(this) != DialogResult.OK) return;

            SetBusy(true);
            try
            {
                if (!await BackupIfEnabledAsync(inst, "version")) return;
                InstanceManager.ChangeVersion(inst, form.NewMcVersion, form.NewLoader, form.NewLoaderVersion);
                Reload();
                SelectByName(inst.Name);
                MessageBox.Show(this, Localization.T("cv_done", inst.McVersion, inst.Loader), "Velin");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localization.T("error_operation") + "\n\n" + ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _status.Text = "";
                SetBusy(false);
            }
        }

        // Atajos: Ctrl+N nueva · Ctrl+I importar · F5 actualizar · F2 renombrar · Supr eliminar · Enter jugar
        // (Supr y Enter sólo funcionan con el foco en la lista, para no pisar la escritura en otros campos).
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_btnNew.Enabled)
            {
                switch (keyData)
                {
                    case Keys.Control | Keys.N: CreateInstance(); return true;
                    case Keys.Control | Keys.I: _ = ImportAsync(); return true;
                    case Keys.F5: Reload(); return true;
                }
            }
            if (_list.Focused && Selected != null && _btnNew.Enabled)
            {
                switch (keyData)
                {
                    case Keys.F2: RenameInstance(); return true;
                    case Keys.Delete: _ = DeleteInstanceAsync(); return true;
                    case Keys.Enter: _ = PlayAsync(); return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void CreateInstance()
        {
            using var form = new NewInstanceForm();
            if (form.ShowDialog(this) == DialogResult.OK)
                Reload();
        }

        private void OpenSettings()
        {
            using var form = new SettingsForm();
            form.ShowDialog(this);
            // Si cambiaste el tema, se ve reflejado al toque sin tener que reabrir Velin.
            RefreshTheme();
            UpdateGifVisibility();
        }

        private void OpenVault()
        {
            using var form = new VaultForm(PathService.VaultDir, Localization.T("vault_title"));
            form.ShowDialog(this);
        }

        private void OpenInstanceFolder()
        {
            var inst = Selected;
            if (inst == null) return;
            inst.EnsureFolders();
            // Abre la interfaz propia del Vault apuntando a la carpeta raíz de la instancia.
            using var form = new VaultForm(inst.InstanceDir, Localization.T("btn_open_folder") + " — " + inst.Name);
            form.ShowDialog(this);
        }

        // --- Jugar -----------------------------------------------------------------------------

        // Pregunta la versión base de Minecraft de un .jar propio (la usan las librerías, los assets y Java).
        private async Task<bool> AskBaseVersionAsync(Instance inst)
        {
            using var dlg = new PromptDialog(Localization.T("cj_title", inst.Name), Localization.T("cj_prompt", inst.CustomJar), "");
            if (dlg.ShowDialog(this) != DialogResult.OK) return false;
            var version = dlg.Value.Trim();
            try
            {
                var manifest = await MinecraftDownloader.GetManifestAsync();
                var match = manifest.Versions.FirstOrDefault(v => string.Equals(v.Id, version, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                {
                    MessageBox.Show(this, Localization.T("cj_unknown_version", version), "Velin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                inst.McVersion = match.Id;
                inst.Save();
                Reload();
                SelectByName(inst.Name);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localization.T("error_operation") + "\n\n" + ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private async Task PlayAsync()
        {
            var inst = Selected;
            if (inst == null) return;

            // Instancia con .jar propio cuyo .jar no indica la versión: se pregunta una vez cuál es la versión base.
            if (inst.McVersion.Length == 0 && !await AskBaseVersionAsync(inst)) return;

            // Si la RAM pedida es más de la que hay libre ahora, se avisa antes de trabar la PC.
            var mem = SystemInfo.GetMemory();
            if (mem is { } m && inst.RamMB > m.FreeMB - 256)
            {
                var answer = MessageBox.Show(this, Localization.T("ram_warning_launch", inst.RamMB, m.FreeMB, m.TotalMB),
                    "Velin", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return;
            }

            SetBusy(true);
            _progress.Visible = true;
            _progress.Value = 0;

            var progress = new Progress<string>(msg => _status.Text = msg);
            // Progreso REAL: calculado a partir de bytes/archivos efectivamente
            // descargados (ver GameLauncher/MinecraftDownloader), no una animación.
            var percent = new Progress<double>(p => _progress.Value = Math.Max(0, Math.Min(100, (int)(p * 100))));

            Process? mcProcess = null;
            var launchStarted = DateTime.Now;
            _launchCts = new CancellationTokenSource();
            var ct = _launchCts.Token;
            _btnCancel.Text = Localization.T("btn_cancel");
            _btnCancel.Enabled = true;
            _btnCancel.Visible = true;
            try
            {
                mcProcess = await GameLauncher.LaunchAsync(inst, progress, percent,
                    onWarning: msg => MessageBox.Show(this, msg, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Warning), ct: ct);
                _launchProcess = mcProcess;

                // Mientras Minecraft carga (hasta que aparece su ventana) el launcher sigue visible y se puede cancelar.
                _status.Text = Localization.T("status_loading_game");
                await WaitForGameWindowAsync(mcProcess, ct);
                _status.Text = Localization.T("status_launching");
            }
            catch (OperationCanceledException)
            {
                // Cancelado por el usuario: si el juego ya había arrancado, se cierra.
                KillLaunchedProcess(mcProcess);
                mcProcess = null;
                _status.Text = Localization.T("status_cancelled");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localization.T("error_launch") + "\n\n" + ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _status.Text = "";
            }
            finally
            {
                _btnCancel.Visible = false;
                _launchProcess = null;
                _launchCts?.Dispose();
                _launchCts = null;
                SetBusy(false);
                var t = new System.Windows.Forms.Timer { Interval = 1200 };
                t.Tick += (s, e) => { _progress.Visible = false; _progress.Value = 0; t.Stop(); t.Dispose(); };
                t.Start();
            }

            if (mcProcess != null)
                await HideWhilePlayingAsync(inst, mcProcess, launchStarted);
        }

        // Espera a que aparezca la ventana de Minecraft (o a que el proceso termine). Máximo 3 minutos.
        private static async Task WaitForGameWindowAsync(Process proc, CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromMinutes(3))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (proc.HasExited) return;
                    proc.Refresh();
                    if (proc.MainWindowHandle != IntPtr.Zero) return;
                }
                catch { return; }
                await Task.Delay(500, ct);
            }
        }

        private static void KillLaunchedProcess(Process? proc)
        {
            try { if (proc != null && !proc.HasExited) proc.Kill(true); } catch { }
        }

        // Botón Cancelar: corta las descargas/instalación y, si Minecraft ya estaba cargando, lo cierra.
        private void CancelLaunch()
        {
            _btnCancel.Enabled = false;
            _status.Text = Localization.T("status_cancelling");
            try { _launchCts?.Cancel(); } catch { }
            KillLaunchedProcess(_launchProcess);
        }

        // Mientras Minecraft está abierto, el launcher se esconde y se calla (música incluida).
        // Cuando lo cerrás vuelve a aparecer, anota el tiempo jugado y, si el juego terminó con
        // un error, muestra qué pasó.
        private async Task HideWhilePlayingAsync(Instance inst, Process mcProcess, DateTime launchStarted)
        {
            var played = Stopwatch.StartNew();
            bool keepOpen = SettingsManager.Load().KeepLauncherOpen;
            try
            {
                if (keepOpen)
                    new LogViewerForm(Localization.T("console_title", inst.Name), GameLauncher.LogPathFor(inst)).Show();
                else
                    Hide();
                SoundManager.StopMusic();
                await mcProcess.WaitForExitAsync();
            }
            catch { /* si algo falla monitoreando el proceso, igual lo mostramos de nuevo abajo */ }
            finally
            {
                Show();
                WindowState = FormWindowState.Normal;
                Activate();
                SoundManager.SetMusicEnabled(SoundManager.MusicEnabled);
            }

            try
            {
                inst.TotalPlaySeconds += (long)played.Elapsed.TotalSeconds;
                inst.LastPlayedUtc = DateTime.UtcNow;
                inst.Save();
            }
            catch { }
            Reload();

            int exitCode = 0;
            try { exitCode = mcProcess.ExitCode; } catch { }
            if (exitCode != 0)
                await ShowCrashReportAsync(inst, exitCode, launchStarted);
        }

        private async Task ShowCrashReportAsync(Instance inst, int exitCode, DateTime launchStarted)
        {
            await Task.Delay(400); // deja que termine de escribirse el log
            var logPath = GameLauncher.LogPathFor(inst);
            var tail = LaunchLog.ReadTail(logPath, 60);
            if (string.IsNullOrWhiteSpace(tail)) tail = Localization.T("crash_no_log");

            using var form = new CrashReportForm(Localization.T("crash_message", exitCode), tail, logPath,
                FindLatestCrashReport(inst, launchStarted));
            form.ShowDialog(this);
        }

        private static string? FindLatestCrashReport(Instance inst, DateTime launchStarted)
        {
            try
            {
                var dir = Path.Combine(inst.GameDir, "crash-reports");
                if (!Directory.Exists(dir)) return null;
                return new DirectoryInfo(dir).GetFiles("crash-*.txt")
                    .Where(f => f.LastWriteTime >= launchStarted.AddSeconds(-5))
                    .OrderByDescending(f => f.LastWriteTime)
                    .Select(f => f.FullName)
                    .FirstOrDefault();
            }
            catch { return null; }
        }

        // --- Mods ---------------------------------------------------------------------------------

        private async Task CheckUpdatesAsync()
        {
            var inst = Selected;
            if (inst == null) return;
            SetBusy(true);
            try
            {
                var api = new ModrinthApi();

                // Primero se reconocen los mods agregados a mano (por su hash), para que
                // también ellos entren en el chequeo de actualizaciones.
                _status.Text = Localization.T("status_identifying");
                try { await ModManager.IdentifyLocalModsAsync(inst, api, msg => _status.Text = msg); } catch { }

                _status.Text = Localization.T("status_checking_updates");
                var updates = await ModManager.CheckUpdatesAsync(inst);
                if (updates.Count == 0)
                {
                    MessageBox.Show(this, Localization.T("all_mods_updated"), "Velin");
                    return;
                }

                var names = string.Join("\n", updates.Select(u => $"- {u.Current.ProjectTitle}: {u.Latest.Name}"));
                var res = MessageBox.Show(this, $"{Localization.T("updates_available")}\n\n{names}\n\n{Localization.T("update_now_q")}", "Velin", MessageBoxButtons.YesNo);
                if (res != DialogResult.Yes) return;

                if (!await BackupIfEnabledAsync(inst, "mods")) return;

                int newDependencies = 0;
                var problems = new List<string>();
                foreach (var u in updates)
                {
                    try
                    {
                        var result = await ModManager.ApplyUpdateAsync(inst, u, msg => _status.Text = msg);
                        newDependencies += Math.Max(0, result.Installed.Count - 1);
                        problems.AddRange(result.Warnings);
                    }
                    catch (Exception ex)
                    {
                        problems.Add(Localization.T("update_failed_item", u.Current.ProjectTitle, ex.Message));
                    }
                }

                MessageBox.Show(this, newDependencies > 0
                    ? Localization.T("mods_updated_deps", newDependencies)
                    : Localization.T("mods_updated"), "Velin");

                if (problems.Count > 0)
                    MessageBox.Show(this, Localization.T("deps_warning_title") + "\n\n- " + string.Join("\n- ", problems.Distinct()),
                        "Velin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localization.T("error_updates") + "\n\n" + ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _status.Text = "";
                SetBusy(false);
            }
        }

        // Si el backup automático de mundos está activado en Configuración, lo hace.
        // Devuelve false si falló y preferís cancelar la operación.
        private async Task<bool> BackupIfEnabledAsync(Instance inst, string reason)
        {
            if (!SettingsManager.Load().BackupWorldsEnabled || !BackupService.HasWorlds(inst)) return true;

            _status.Text = Localization.T("status_backup");
            try
            {
                await BackupService.BackupWorldsAsync(inst, reason);
                return true;
            }
            catch (Exception ex)
            {
                var answer = MessageBox.Show(this, Localization.T("backup_failed_continue", ex.Message),
                    "Velin", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                return answer == DialogResult.Yes;
            }
        }

        // --- Gestión de instancias --------------------------------------------------------------

        private async Task DeleteInstanceAsync()
        {
            var inst = Selected;
            if (inst == null) return;
            var res = MessageBox.Show(this, Localization.T("confirm_delete", inst.Name), "Velin", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (res != DialogResult.Yes) return;

            SetBusy(true);
            try
            {
                if (!await BackupIfEnabledAsync(inst, "delete")) return;
                InstanceManager.Delete(inst);
                Reload();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localization.T("error_operation") + "\n\n" + ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _status.Text = "";
                SetBusy(false);
            }
        }

        private async Task DuplicateAsync()
        {
            var inst = Selected;
            if (inst == null) return;

            var suggested = InstanceManager.UniqueName(inst.Name + " (copia)");
            using var dlg = new PromptDialog(Localization.T("ctx_duplicate").Replace("...", ""), Localization.T("prompt_duplicate"), suggested);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            var name = dlg.Value;
            if (!InstanceManager.IsValidName(name)) { MessageBox.Show(this, Localization.T("err_invalid_name")); return; }
            if (InstanceManager.Exists(name)) { MessageBox.Show(this, Localization.T("err_name_exists")); return; }

            SetBusy(true);
            _status.Text = Localization.T("status_duplicating");
            try
            {
                await InstanceManager.DuplicateAsync(inst, name);
                Reload();
                SelectByName(name);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localization.T("error_operation") + "\n\n" + ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _status.Text = "";
                SetBusy(false);
            }
        }

        private void RenameInstance()
        {
            var inst = Selected;
            if (inst == null) return;

            using var dlg = new PromptDialog(Localization.T("ctx_rename").Replace("...", ""), Localization.T("prompt_rename"), inst.Name);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            var name = dlg.Value;
            if (name == inst.Name) return;
            if (!InstanceManager.IsValidName(name)) { MessageBox.Show(this, Localization.T("err_invalid_name")); return; }

            try
            {
                InstanceManager.Rename(inst, name);
                Reload();
                SelectByName(name);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localization.T("error_operation") + "\n\n" + ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ExportAsync()
        {
            var inst = Selected;
            if (inst == null) return;

            using var sfd = new SaveFileDialog { Filter = Localization.T("export_filter"), FileName = inst.Name + ".zip" };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            SetBusy(true);
            _status.Text = Localization.T("status_exporting");
            try
            {
                await InstanceManager.ExportAsync(inst, sfd.FileName);
                MessageBox.Show(this, Localization.T("export_done", sfd.FileName), "Velin");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localization.T("error_operation") + "\n\n" + ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _status.Text = "";
                SetBusy(false);
            }
        }

        // Importa un .mrpack (modpack de Modrinth) o un .zip exportado por Velin.
        private async Task ImportAsync()
        {
            using var ofd = new OpenFileDialog { Filter = Localization.T("import_filter") };
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            bool isModpack = string.Equals(Path.GetExtension(ofd.FileName), ".mrpack", StringComparison.OrdinalIgnoreCase);

            SetBusy(true);
            _progress.Visible = true;
            _progress.Value = 0;
            _status.Text = Localization.T("status_importing");

            var progress = new Progress<string>(msg => _status.Text = msg);
            var percent = new Progress<double>(p => _progress.Value = Math.Max(0, Math.Min(100, (int)(p * 100))));
            var warnings = new List<string>();

            try
            {
                bool isCurseForge = !isModpack && CurseForgeService.IsCurseForgeZip(ofd.FileName);
                Instance imported = isModpack
                    ? await ModpackImporter.ImportMrpackAsync(ofd.FileName, progress, percent, w => warnings.Add(w))
                    : isCurseForge
                        ? await CurseForgeService.ImportModpackAsync(ofd.FileName, CurseForgeApi.FromSettings(), progress, percent, w => warnings.Add(w))
                        : await InstanceManager.ImportExportedAsync(ofd.FileName);

                Reload();
                SelectByName(imported.Name);
                MessageBox.Show(this, Localization.T("import_done", imported.Name), "Velin");

                foreach (var w in warnings)
                    MessageBox.Show(this, w, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Localization.T("error_operation") + "\n\n" + ex.Message, "Velin", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _status.Text = "";
                _progress.Visible = false;
                _progress.Value = 0;
                SetBusy(false);
            }
        }
    }
}
