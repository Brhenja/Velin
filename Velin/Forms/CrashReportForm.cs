using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Velin.UI;

namespace Velin.Forms
{
    // Se muestra cuando Minecraft se cierra con un código de error: las últimas líneas de lo que
    // escribió el juego, las causas probables que reconoció el analizador (si hay alguna), y accesos al log completo y al crash report si existe.
    public class CrashReportForm : VelinFormBase
    {
        private readonly TextBox _text = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false };

        // causes: ya traducidas, una por línea (vacía si el analizador no reconoció nada).
        public CrashReportForm(string message, IReadOnlyList<string> causes, string logText, string logPath, string? crashReportPath)
            : base(Localization.T("crash_title"))
        {
            LockSize(660, 520);
            StartPosition = FormStartPosition.CenterParent;

            var header = new Label { Text = message, AutoSize = false };
            header.SetBounds(12, 8, 630, 34);
            Content.Controls.Add(header);

            // Causas probables: lo primero que se lee, antes del log crudo.
            var causesText = causes.Count > 0
                ? Localization.T("crash_causes_title") + Environment.NewLine + string.Join(Environment.NewLine, causes.Select(c => "• " + c))
                : Localization.T("crash_none");
            var causesLabel = new Label { Text = causesText, AutoSize = false };
            causesLabel.SetBounds(12, 44, 630, 116);
            Content.Controls.Add(causesLabel);

            _text.Font = new Font("Consolas", 8.5f);
            _text.Text = logText;
            _text.SetBounds(12, 166, 630, 290);
            Content.Controls.Add(_text);

            int x = 12;
            AddButton(Localization.T("btn_open_log"), x, 190, () => OpenFile(logPath));
            x += 196;
            if (!string.IsNullOrEmpty(crashReportPath))
            {
                var report = crashReportPath;
                AddButton(Localization.T("btn_open_crash_report"), x, 170, () => OpenFile(report));
                x += 176;
            }
            AddButton(Localization.T("btn_copy"), x, 90, () =>
            {
                try { Clipboard.SetText(_text.Text); } catch { }
            });
            AddButton(Localization.T("btn_close"), 552, 90, Close);

            // Que se vea el final del log (lo más reciente) y no quede todo el texto seleccionado.
            Shown += (s, e) =>
            {
                _text.SelectionStart = _text.TextLength;
                _text.SelectionLength = 0;
                _text.ScrollToCaret();
            };

            Theme.Apply(this);
        }

        private void AddButton(string text, int x, int width, Action onClick)
        {
            var b = new Button { Text = text };
            b.SetBounds(x, 464, width, 32);
            b.Click += (s, e) => onClick();
            Content.Controls.Add(b);
        }

        private static void OpenFile(string path)
        {
            try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); }
            catch { /* si no se puede abrir, no pasa nada */ }
        }
    }
}
