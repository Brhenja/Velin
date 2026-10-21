using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SharpCompress.Archives;
using SharpCompress.Common;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Vault genérico tipo "carpeta de Windows": sirve tanto para el Vault personal
    // (archivos sueltos) como para la carpeta de mods de una instancia puntual.
    // Permite navegar subcarpetas (ej: las que deja una extracción de zip).
    public class VaultForm : VelinFormBase
    {
        private static readonly string[] ArchiveExtensions = { ".zip", ".rar", ".7z" };

        private readonly string _rootDir;
        private string _currentDir;
        private readonly ListView _list = new();
        private readonly ImageList _icons = new() { ImageSize = new Size(32, 32) };
        private readonly ContextMenuStrip _menu = new();
        private readonly TextBox _search = new();
        private readonly Button _btnUp = new();
        private readonly Label _path = new();
        private readonly Button _btnAdd = new();
        private readonly Button _btnNewFolder = new();
        private readonly Button _btnRefresh = new();
        private readonly Button _btnOpenFolder = new();
        private readonly Label _status = new();
        private FileSystemWatcher? _watcher;
        private readonly System.Windows.Forms.Timer _refreshDebounce;

        public VaultForm(string targetDir, string title) : base(title)
        {
            _rootDir = targetDir;
            _currentDir = targetDir;
            Directory.CreateDirectory(_rootDir);

            LockSize(560, 460);
            StartPosition = FormStartPosition.CenterParent;

            _btnUp.Text = "⬆";
            _btnUp.SetBounds(12, 12, 32, 24);
            _btnUp.Click += (s, e) => NavigateUp();
            Content.Controls.Add(_btnUp);

            _path.AutoEllipsis = true;
            _path.SetBounds(50, 12, 220, 24);
            _path.TextAlign = ContentAlignment.MiddleLeft;
            Content.Controls.Add(_path);

            _search.SetBounds(278, 12, 270, 24);
            _search.TextChanged += (s, e) => RefreshFiles();
            Content.Controls.Add(_search);

            _list.View = View.LargeIcon;
            _list.LargeImageList = _icons;
            _list.MultiSelect = true;
            _list.AllowDrop = true;
            _list.SetBounds(12, 44, 536, 320);
            _list.DragEnter += ListDragEnter;
            _list.DragDrop += ListDragDrop;
            _list.MouseUp += ListMouseUp;
            _list.DoubleClick += (s, e) => OpenOrEnterSelected();
            Content.Controls.Add(_list);

            BuildContextMenu();
            _list.ContextMenuStrip = _menu;

            _btnAdd.Text = Localization.T("btn_add_file");
            _btnAdd.SetBounds(12, 376, 110, 32);
            _btnAdd.Click += (s, e) => AddFiles();
            Content.Controls.Add(_btnAdd);

            _btnNewFolder.Text = Localization.T("btn_new_folder");
            _btnNewFolder.SetBounds(128, 376, 110, 32);
            _btnNewFolder.Click += (s, e) => CreateFolder();
            Content.Controls.Add(_btnNewFolder);

            _btnRefresh.Text = Localization.T("btn_refresh");
            _btnRefresh.SetBounds(244, 376, 90, 32);
            _btnRefresh.Click += (s, e) => RefreshFiles();
            Content.Controls.Add(_btnRefresh);

            _btnOpenFolder.Text = Localization.T("btn_open_folder");
            _btnOpenFolder.SetBounds(340, 376, 208, 32);
            _btnOpenFolder.Click += (s, e) => OpenInExplorer();
            Content.Controls.Add(_btnOpenFolder);

            _status.SetBounds(12, 416, 536, 20);
            Content.Controls.Add(_status);

            // Se actualiza sola si agregás/sacás archivos de la carpeta desde afuera
            // (por ejemplo, arrastrándolos en el Explorador de Windows mientras esta
            // ventana ya está abierta) — antes había que cerrar y volver a abrir.
            _refreshDebounce = new System.Windows.Forms.Timer { Interval = 300 };
            _refreshDebounce.Tick += (s, e) => { _refreshDebounce.Stop(); SafeInvokeRefresh(); };

            Load += (s, e) => NavigateTo(_rootDir);
            FormClosed += (s, e) => { _watcher?.Dispose(); _refreshDebounce.Dispose(); };

            Theme.Apply(this);
        }

        // --- Navegación de subcarpetas -------------------------------------------------

        private void NavigateTo(string dir)
        {
            _currentDir = dir;
            Directory.CreateDirectory(_currentDir);

            _watcher?.Dispose();
            _watcher = new FileSystemWatcher(_currentDir) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite };
            _watcher.Created += (s, e) => DebounceRefresh();
            _watcher.Deleted += (s, e) => DebounceRefresh();
            _watcher.Renamed += (s, e) => DebounceRefresh();
            _watcher.EnableRaisingEvents = true;

            var rel = Path.GetRelativePath(_rootDir, _currentDir);
            _path.Text = rel == "." ? "\\" : "\\" + rel;
            _btnUp.Enabled = !string.Equals(_currentDir.TrimEnd('\\', '/'), _rootDir.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

            RefreshFiles();
        }

        private void NavigateUp()
        {
            var parent = Directory.GetParent(_currentDir);
            if (parent != null) NavigateTo(parent.FullName);
        }

        private void DebounceRefresh()
        {
            try { if (!IsDisposed) BeginInvoke(new Action(() => { _refreshDebounce.Stop(); _refreshDebounce.Start(); })); }
            catch { }
        }

        private void SafeInvokeRefresh()
        {
            try { if (!IsDisposed) RefreshFiles(); } catch { }
        }

        // --- Menú contextual -------------------------------------------------------

        private void BuildContextMenu()
        {
            var open = new ToolStripMenuItem(Localization.T("ctx_open"));
            open.Click += (s, e) => OpenOrEnterSelected();
            var extract = new ToolStripMenuItem(Localization.T("ctx_extract"));
            extract.Click += (s, e) => ExtractSelected();
            var cut = new ToolStripMenuItem(Localization.T("ctx_cut"));
            cut.Click += (s, e) => CopySelectionToClipboard(cut: true);
            var copy = new ToolStripMenuItem(Localization.T("ctx_copy"));
            copy.Click += (s, e) => CopySelectionToClipboard(cut: false);
            var paste = new ToolStripMenuItem(Localization.T("ctx_paste"));
            paste.Click += (s, e) => PasteFromClipboard();
            var copyToInstance = new ToolStripMenuItem(Localization.T("ctx_copy_to_instance"));
            copyToInstance.Click += (s, e) => CopySelectedToInstance();
            var delete = new ToolStripMenuItem(Localization.T("ctx_delete"));
            delete.Click += (s, e) => DeleteSelected();

            _menu.Items.Add(open);
            _menu.Items.Add(extract);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(cut);
            _menu.Items.Add(copy);
            _menu.Items.Add(paste);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(copyToInstance);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(delete);

            _menu.Opening += (s, e) =>
            {
                var paths = GetSelectedPaths();
                extract.Visible = paths.Length > 0 && paths.All(p => !Directory.Exists(p) && IsArchive(p));
                copyToInstance.Visible = paths.Length > 0 && paths.All(p => !Directory.Exists(p));
            };
        }

        private static bool IsArchive(string path) =>
            ArchiveExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

        private void ListMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && _list.SelectedItems.Count == 0)
            {
                var hit = _list.HitTest(e.Location);
                if (hit.Item != null) hit.Item.Selected = true;
            }
        }

        // --- Listado -----------------------------------------------------------------

        private void RefreshFiles()
        {
            var selectedNames = _list.SelectedItems.Cast<ListViewItem>().Select(i => i.Text).ToHashSet();

            _list.Items.Clear();
            _icons.Images.Clear();

            Directory.CreateDirectory(_currentDir);
            var filter = _search.Text.Trim();

            foreach (var dirPath in Directory.GetDirectories(_currentDir).OrderBy(Path.GetFileName))
            {
                var name = Path.GetFileName(dirPath);
                if (!string.IsNullOrEmpty(filter) && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                AddItem(dirPath, name, isDirectory: true, selectedNames);
            }
            foreach (var filePath in Directory.GetFiles(_currentDir).OrderBy(Path.GetFileName))
            {
                var name = Path.GetFileName(filePath);
                if (!string.IsNullOrEmpty(filter) && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                AddItem(filePath, name, isDirectory: false, selectedNames);
            }
        }

        private void AddItem(string path, string name, bool isDirectory, System.Collections.Generic.HashSet<string> selectedNames)
        {
            var key = isDirectory ? "\0dir" : Path.GetExtension(name).ToLowerInvariant();
            if (string.IsNullOrEmpty(key)) key = "\0file";

            if (!_icons.Images.ContainsKey(key))
            {
                var ico = ShellIcon.Get(path, isDirectory);
                if (ico != null) _icons.Images.Add(key, ico);
            }

            var item = new ListViewItem(name, _icons.Images.ContainsKey(key) ? key : null) { Tag = path };
            try
            {
                if (isDirectory)
                    item.ToolTipText = Localization.T("vault_folder");
                else
                {
                    var info = new FileInfo(path);
                    item.ToolTipText = $"{FormatSize(info.Length)} — {info.LastWriteTime:dd/MM/yyyy HH:mm}";
                }
            }
            catch { }
            item.Selected = selectedNames.Contains(name);
            _list.Items.Add(item);
        }

        private static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = bytes;
            int u = 0;
            while (size >= 1024 && u < units.Length - 1) { size /= 1024; u++; }
            return $"{size:0.#} {units[u]}";
        }

        private string[] GetSelectedPaths()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (ListViewItem item in _list.SelectedItems)
                if (item.Tag is string p) list.Add(p);
            return list.ToArray();
        }

        // --- Acciones ------------------------------------------------------------------

        private void AddFiles()
        {
            using var ofd = new OpenFileDialog { Multiselect = true };
            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                foreach (var f in ofd.FileNames)
                    SafeCopy(f, Path.Combine(_currentDir, Path.GetFileName(f)));
                RefreshFiles();
            }
        }

        private void CreateFolder()
        {
            using var dlg = new PromptDialog(Localization.T("btn_new_folder"), Localization.T("new_folder_name"), Localization.T("new_folder_default"));
            if (dlg.ShowDialog(this) != DialogResult.OK) return;

            var name = dlg.Value;
            if (string.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                _status.Text = Localization.T("new_folder_invalid");
                return;
            }
            try
            {
                Directory.CreateDirectory(Path.Combine(_currentDir, name));
                RefreshFiles();
            }
            catch (Exception ex) { _status.Text = ex.Message; }
        }

        private void OpenInExplorer()
        {
            try { Process.Start(new ProcessStartInfo { FileName = _currentDir, UseShellExecute = true }); }
            catch (Exception ex) { _status.Text = ex.Message; }
        }

        private void OpenOrEnterSelected()
        {
            var paths = GetSelectedPaths();
            if (paths.Length == 0) return;
            if (Directory.Exists(paths[0])) { NavigateTo(paths[0]); return; }
            try { Process.Start(new ProcessStartInfo { FileName = paths[0], UseShellExecute = true }); }
            catch (Exception ex) { _status.Text = ex.Message; }
        }

        private void ExtractSelected()
        {
            var paths = GetSelectedPaths().Where(p => !Directory.Exists(p) && IsArchive(p)).ToArray();
            if (paths.Length == 0) return;

            int ok = 0, failed = 0;
            foreach (var path in paths)
            {
                try
                {
                    var destDir = Path.Combine(_currentDir, Path.GetFileNameWithoutExtension(path));
                    Directory.CreateDirectory(destDir);
                    var destRoot = Path.GetFullPath(destDir).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
                    using var archive = ArchiveFactory.OpenArchive(path);
                    foreach (var entry in archive.Entries.Where(en => !en.IsDirectory && !string.IsNullOrEmpty(en.Key)))
                    {
                        // Defensa contra archivos maliciosos con rutas tipo "..\..\algo":
                        // sólo escribimos si el destino final queda DENTRO de la carpeta de extracción.
                        var target = Path.GetFullPath(Path.Combine(destDir, entry.Key!.Replace('/', Path.DirectorySeparatorChar)));
                        if (!target.StartsWith(destRoot, StringComparison.OrdinalIgnoreCase)) continue;

                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        using var src = entry.OpenEntryStream();
                        using var dst = File.Create(target);
                        src.CopyTo(dst);
                    }
                    ok++;
                }
                catch { failed++; }
            }
            _status.Text = failed == 0 ? Localization.T("extract_ok", ok) : Localization.T("extract_partial", ok, failed);
            RefreshFiles();
        }

        private void DeleteSelected()
        {
            var paths = GetSelectedPaths();
            if (paths.Length == 0) return;
            foreach (var p in paths)
            {
                try { if (Directory.Exists(p)) Directory.Delete(p, true); else File.Delete(p); } catch { }
            }
            RefreshFiles();
        }

        private void CopySelectionToClipboard(bool cut)
        {
            var paths = GetSelectedPaths();
            if (paths.Length == 0) return;

            var data = new DataObject();
            var collection = new StringCollection();
            collection.AddRange(paths);
            data.SetFileDropList(collection);

            if (cut)
            {
                var effect = BitConverter.GetBytes((int)DragDropEffects.Move);
                data.SetData("Preferred DropEffect", new MemoryStream(effect));
            }

            Clipboard.SetDataObject(data, true);
        }

        private void PasteFromClipboard()
        {
            if (!Clipboard.ContainsFileDropList()) return;
            var files = Clipboard.GetFileDropList();

            bool move = false;
            try
            {
                var dataObj = Clipboard.GetDataObject();
                if (dataObj != null && dataObj.GetDataPresent("Preferred DropEffect")
                    && dataObj.GetData("Preferred DropEffect") is MemoryStream stream)
                {
                    var bytes = new byte[4];
                    stream.Read(bytes, 0, 4);
                    move = ((DragDropEffects)BitConverter.ToInt32(bytes, 0)).HasFlag(DragDropEffects.Move);
                }
            }
            catch { }

            foreach (var f in files)
            {
                if (f == null) continue;
                var dest = Path.Combine(_currentDir, Path.GetFileName(f));
                try
                {
                    if (Directory.Exists(f))
                    {
                        if (!Directory.Exists(dest)) CopyDirectory(f, dest);
                        if (move) Directory.Delete(f, true);
                    }
                    else if (move) { if (File.Exists(dest)) File.Delete(dest); File.Move(f, dest); }
                    else SafeCopy(f, dest);
                }
                catch { }
            }
            RefreshFiles();
        }

        private static void CopyDirectory(string src, string dest)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.GetFiles(src))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(src))
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
        }

        private void SafeCopy(string src, string dest)
        {
            try { File.Copy(src, dest, true); } catch { }
        }

        private void ListDragEnter(object? sender, DragEventArgs e)
        {
            e.Effect = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void ListDragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            foreach (var f in files)
            {
                var dest = Path.Combine(_currentDir, Path.GetFileName(f));
                if (Directory.Exists(f)) { if (!Directory.Exists(dest)) CopyDirectory(f, dest); }
                else SafeCopy(f, dest);
            }
            RefreshFiles();
        }

        private void CopySelectedToInstance()
        {
            var paths = GetSelectedPaths().Where(p => !Directory.Exists(p)).ToArray();
            if (paths.Length == 0) return;

            var instances = InstanceManager.LoadAll();
            if (instances.Count == 0)
            {
                MessageBox.Show(this, Localization.T("no_instances_yet"));
                return;
            }

            using var picker = new CopyToInstanceDialog(instances);
            if (picker.ShowDialog(this) == DialogResult.OK && picker.SelectedInstance != null)
            {
                var inst = picker.SelectedInstance;
                var destFolder = picker.SelectedSubfolder switch
                {
                    "mods" => inst.ModsDir,
                    "resourcepacks" => Path.Combine(inst.GameDir, "resourcepacks"),
                    "shaderpacks" => Path.Combine(inst.GameDir, "shaderpacks"),
                    _ => inst.GameDir
                };
                Directory.CreateDirectory(destFolder);
                foreach (var p in paths)
                    SafeCopy(p, Path.Combine(destFolder, Path.GetFileName(p)));
                _status.Text = Localization.T("copied_to", inst.Name);
            }
        }
    }

    // Íconos reales de Windows (incluye carpetas) vía Shell32, en vez de limitarnos a
    // los íconos asociados de archivos ejecutables.
    internal static class ShellIcon
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        public static Bitmap? Get(string path, bool isDirectory)
        {
            var shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_LARGEICON;
            uint attr = FILE_ATTRIBUTE_NORMAL;
            string lookupPath = path;
            if (isDirectory)
            {
                attr = FILE_ATTRIBUTE_DIRECTORY;
                flags |= SHGFI_USEFILEATTRIBUTES;
                lookupPath = "folder"; // no hace falta que exista; sólo queremos el ícono genérico
            }
            try
            {
                var res = SHGetFileInfo(lookupPath, attr, ref shfi, (uint)Marshal.SizeOf(shfi), flags);
                if (res == IntPtr.Zero || shfi.hIcon == IntPtr.Zero) return null;
                using var icon = (Icon)Icon.FromHandle(shfi.hIcon).Clone();
                var bmp = icon.ToBitmap();
                DestroyIcon(shfi.hIcon);
                return bmp;
            }
            catch { return null; }
        }
    }
}
