using System;
using System.Drawing;
using System.Windows.Forms;
using Velin.Models;
using Velin.UI;

namespace Velin.Forms
{
    // Barra "◀ Página X de Y ▶" para recorrer los resultados de búsqueda de a 30.
    public class ResultsPager : Panel
    {
        private readonly Button _prev = new() { Text = "◀" };
        private readonly Button _next = new() { Text = "▶" };
        private readonly Label _label = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };
        private ContentPage? _state;

        // Se dispara con el número de página (desde 0) que se pidió.
        public event Action<int>? PageRequested;

        public ResultsPager(int width)
        {
            BackColor = Color.Transparent;
            Size = new Size(width, 26);
            _prev.SetBounds(0, 0, 70, 26);
            _next.SetBounds(width - 70, 0, 70, 26);
            _label.SetBounds(76, 0, width - 152, 26);
            _prev.Click += (s, e) => { if (_state is { HasPrevious: true }) PageRequested?.Invoke(_state.Page - 1); };
            _next.Click += (s, e) => { if (_state is { HasNext: true }) PageRequested?.Invoke(_state.Page + 1); };
            Controls.Add(_prev);
            Controls.Add(_label);
            Controls.Add(_next);
            SetState(null);
        }

        // null = sin resultados / error: botones apagados y sin texto.
        public void SetState(ContentPage? page)
        {
            _state = page;
            _prev.Enabled = page is { HasPrevious: true };
            _next.Enabled = page is { HasNext: true };
            _label.Text = page == null || page.Items.Count == 0 ? "" : Localization.T("pager_label", page.Page + 1, page.TotalPages);
            _prev.Invalidate();
            _next.Invalidate();
        }

        // Mientras se busca no se puede cambiar de página.
        public void SetBusy(bool busy)
        {
            if (busy) { _prev.Enabled = false; _next.Enabled = false; }
            else SetState(_state);
        }
    }
}
