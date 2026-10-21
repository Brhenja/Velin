using System;
using System.Drawing;
using System.Windows.Forms;

namespace Velin.UI
{
    // Barra de progreso dibujada a mano (la nativa de Windows no se puede
    // recolorear: siempre sale verde). Usa el color del tema activo.
    public class VelinProgressBar : ProgressBar
    {
        public VelinProgressBar()
        {
            SetStyle(ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var rect = ClientRectangle;
            using (var bg = new SolidBrush(Theme.ControlBg))
                e.Graphics.FillRectangle(bg, rect);

            double pct = Maximum == Minimum ? 0 : (double)(Value - Minimum) / (Maximum - Minimum);
            int fillWidth = (int)(rect.Width * pct);
            if (fillWidth > 0)
            {
                using var fill = new SolidBrush(Theme.ProgressBarColor);
                e.Graphics.FillRectangle(fill, 0, 0, fillWidth, rect.Height);
            }

            using var pen = new Pen(Theme.Accent);
            e.Graphics.DrawRectangle(pen, 0, 0, rect.Width - 1, rect.Height - 1);
        }
    }
}
