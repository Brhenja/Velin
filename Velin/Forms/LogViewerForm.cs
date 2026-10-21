using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Velin.UI;

namespace Velin.Forms
{
    // Consola en vivo: va mostrando lo que escribe Minecraft en velin-launch.log mientras el juego corre.
    // No es modal y no tiene dueño, así que sigue visible aunque el launcher principal se oculte.
    public class LogViewerForm : VelinFormBase
    {
        private const int MaxLines = 5000;
        private static readonly string[] ProblemWords = { "warn", "error", "exception", "fatal", "caused by" };

        private readonly string _path;
        private readonly TextBox _box = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false };
        private readonly CheckBox _onlyProblems = new();
        private readonly CheckBox _pause = new();
        private readonly System.Windows.Forms.Timer _timer = new() { Interval = 700 };
        private readonly List<string> _lines = new();
        private long _position;
        private string _partial = "";

        public LogViewerForm(string title, string logPath) : base(title)
        {
            _path = logPath;
            LockSize(720, 470);
            StartPosition = FormStartPosition.CenterScreen;

            _box.Font = new Font("Consolas", 8.5f);
            _box.SetBounds(12, 12, 690, 366);
            Content.Controls.Add(_box);

            _onlyProblems.Text = Localization.T("console_only_problems");
            _onlyProblems.AutoSize = true;
            _onlyProblems.Location = new Point(12, 390);
            _onlyProblems.CheckedChanged += (s, e) => Render();
            Content.Controls.Add(_onlyProblems);

            _pause.Text = Localization.T("console_pause");
            _pause.AutoSize = true;
            _pause.Location = new Point(12, 414);
            Content.Controls.Add(_pause);

            AddButton(Localization.T("btn_copy"), 330, 90, () => { try { Clipboard.SetText(_box.Text); } catch { } });
            AddButton(Localization.T("btn_open_log"), 426, 150, () =>
            {
                try { Process.Start(new ProcessStartInfo { FileName = _path, UseShellExecute = true }); } catch { }
            });
            AddButton(Localization.T("btn_close"), 582, 120, Close);

            _timer.Tick += (s, e) => { if (!_pause.Checked) Poll(); };
            Shown += (s, e) => { Poll(); _timer.Start(); };
            FormClosed += (s, e) => { _timer.Stop(); _timer.Dispose(); };

            Theme.Apply(this);
        }

        private void AddButton(string text, int x, int width, Action onClick)
        {
            var b = new Button { Text = text };
            b.SetBounds(x, 392, width, 32);
            b.Click += (s, e) => onClick();
            Content.Controls.Add(b);
        }

        private void Poll()
        {
            try
            {
                if (!File.Exists(_path)) return;
                using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (fs.Length < _position)
                {
                    // El log se volvió a crear (nuevo lanzamiento): arrancamos de cero.
                    _position = 0; _partial = ""; _lines.Clear(); _box.Clear();
                }
                if (fs.Length == _position) return;

                fs.Seek(_position, SeekOrigin.Begin);
                string text;
                using (var reader = new StreamReader(fs, Encoding.UTF8, false, 4096, leaveOpen: true))
                    text = reader.ReadToEnd();
                _position = fs.Length;

                var parts = (_partial + text).Split('\n');
                _partial = parts[parts.Length - 1];

                var sb = new StringBuilder();
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    var line = parts[i].TrimEnd('\r');
                    _lines.Add(line);
                    if (!_onlyProblems.Checked || IsProblem(line)) sb.Append(line).Append(Environment.NewLine);
                }
                if (_lines.Count > MaxLines) _lines.RemoveRange(0, _lines.Count - MaxLines);

                if (sb.Length > 0)
                {
                    if (_box.TextLength > 400_000) { Render(); return; }
                    _box.AppendText(sb.ToString());
                }
            }
            catch { /* si el log está ocupado justo ahora, se reintenta en el próximo ciclo */ }
        }

        private void Render()
        {
            var sb = new StringBuilder();
            foreach (var line in _lines)
                if (!_onlyProblems.Checked || IsProblem(line)) sb.Append(line).Append(Environment.NewLine);
            _box.Text = sb.ToString();
            _box.SelectionStart = _box.TextLength;
            _box.SelectionLength = 0;
            _box.ScrollToCaret();
        }

        private static bool IsProblem(string line)
        {
            foreach (var w in ProblemWords)
                if (line.Contains(w, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
