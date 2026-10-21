using System;
using System.Drawing;
using System.Windows.Forms;

namespace Velin.UI
{
    // Barra de título propia: reemplaza la de Windows. Se puede arrastrar
    // (clickeando y arrastrando en cualquier parte vacía), y tiene sólo
    // minimizar y cerrar (sin maximizar: el launcher no se puede poner en
    // pantalla completa a propósito).
    public class TitleBar : Panel
    {
        private readonly Form _owner;
        private readonly Label _title = new();
        private readonly Button _btnMin = new() { Text = "–" };
        private readonly Button _btnClose = new() { Text = "×" };
        private PictureBox? _icon;

        private bool _dragging;
        private Point _dragCursorStart;
        private Point _dragFormStart;

        public TitleBar(Form owner, string title, bool showIcon = true)
        {
            _owner = owner;
            Tag = "titlebar";
            Dock = DockStyle.Top;
            Height = 34;

            if (showIcon)
            {
                try
                {
                    var icoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "velin.ico");
                    if (System.IO.File.Exists(icoPath))
                    {
                        using var ico = new Icon(icoPath, 20, 20);
                        _icon = new PictureBox
                        {
                            Tag = "titlebar",
                            Image = ico.ToBitmap(),
                            SizeMode = PictureBoxSizeMode.CenterImage,
                            Size = new Size(30, Height),
                            Location = new Point(4, 0),
                            BackColor = Color.Transparent
                        };
                        Controls.Add(_icon);
                        HookDrag(_icon);
                    }
                }
                catch { /* sin ícono no pasa nada grave */ }
            }

            _title.Tag = "titlebar";
            _title.Text = title;
            _title.AutoSize = false;
            _title.TextAlign = ContentAlignment.MiddleLeft;
            _title.Location = new Point(showIcon ? 38 : 8, 0);
            _title.Size = new Size(280, Height);
            _title.BackColor = Color.Transparent;
            Controls.Add(_title);
            HookDrag(_title);
            HookDrag(this);

            _btnClose.Tag = "titlebar";
            _btnClose.FlatStyle = FlatStyle.Flat;
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.Size = new Size(42, Height);
            _btnClose.Font = new Font("Segoe UI", 11f);
            _btnClose.Click += (s, e) => _owner.Close();

            _btnMin.Tag = "titlebar";
            _btnMin.FlatStyle = FlatStyle.Flat;
            _btnMin.FlatAppearance.BorderSize = 0;
            _btnMin.Size = new Size(42, Height);
            _btnMin.Font = new Font("Segoe UI", 11f);
            _btnMin.Click += (s, e) => _owner.WindowState = FormWindowState.Minimized;

            Controls.Add(_btnMin);
            Controls.Add(_btnClose);

            Resize += (s, e) => Reposition();
            Reposition();
        }

        private void HookDrag(Control c)
        {
            c.MouseDown += (s, e) =>
            {
                _dragging = true;
                _dragCursorStart = Cursor.Position;
                _dragFormStart = _owner.Location;
            };
            c.MouseMove += (s, e) =>
            {
                if (!_dragging) return;
                var dx = Cursor.Position.X - _dragCursorStart.X;
                var dy = Cursor.Position.Y - _dragCursorStart.Y;
                _owner.Location = new Point(_dragFormStart.X + dx, _dragFormStart.Y + dy);
            };
            c.MouseUp += (s, e) => _dragging = false;
        }

        public void ApplyColors(Color bg, Color fg)
        {
            BackColor = bg;
            _title.ForeColor = fg;
            _btnMin.BackColor = bg; _btnMin.ForeColor = fg;
            _btnClose.BackColor = bg; _btnClose.ForeColor = fg;
            _title.Font = Theme.TitleFont(13f);
            Reposition();
        }

        private void Reposition()
        {
            if (Width <= 0) return;
            _btnClose.Location = new Point(Width - _btnClose.Width, 0);
            _btnMin.Location = new Point(Width - _btnClose.Width - _btnMin.Width, 0);
        }
    }
}
