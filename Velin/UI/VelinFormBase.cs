using System;
using System.Drawing;
using System.Windows.Forms;

namespace Velin.UI
{
    // Base para todas las ventanas del launcher (salvo el splash, que es un caso especial):
    // sin bordes ni barra de título de Windows, con nuestra propia TitleBar (arrastrable,
    // minimizar/cerrar), tamaño fijo (no se puede maximizar ni poner en pantalla completa)
    // y el fondo con el degradé del tema activo.
    public class VelinFormBase : Form
    {
        protected readonly Panel Content = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        private readonly TitleBar _titleBar;

        public VelinFormBase(string title)
        {
            // Usamos coordenadas fijas a mano en todos los formularios; si dejamos el
            // auto-escalado por fuente de WinForms activado, en algunas pantallas puede
            // desalinear o "cortar" controles. Lo apagamos y confiamos en el DPI awareness
            // del app.manifest para que Windows no estire la ventana.
            AutoScaleMode = AutoScaleMode.None;

            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;

            try
            {
                var icoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "velin.ico");
                if (System.IO.File.Exists(icoPath)) Icon = new Icon(icoPath);
            }
            catch { }

            _titleBar = new TitleBar(this, title);
            // IMPORTANTE: Content (Dock=Fill) va PRIMERO y _titleBar (Dock=Top) DESPUÉS.
            // Si se agregan al revés, el panel de contenido "gana" el espacio de arriba
            // y tapa un pedazo de la barra de título (justo el bug que reportaste).
            Controls.Add(Content);
            Controls.Add(_titleBar);

            Load += (s, e) => _titleBar.ApplyColors(Theme.TitleBarBg, Theme.TitleBarText);

            // Atajos de Windows (Win+flecha arriba, arrastrar al borde) pueden maximizar
            // la ventana aunque no tenga botón de maximizar. La volvemos a Normal apenas pasa.
            Resize += (s, e) => { if (WindowState == FormWindowState.Maximized) WindowState = FormWindowState.Normal; };
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Theme.PaintBackground(e.Graphics, ClientRectangle);
        }

        // Fija el tamaño de la ventana (ancho x alto de CONTENIDO, sin contar la barra de
        // título) y evita que se pueda redimensionar o maximizar. Antes esto no sumaba la
        // altura de la barra de título, así que el contenido real tenía menos lugar del
        // que pensaba cada ventana — ahora sí la suma.
        protected void LockSize(int width, int height)
        {
            ClientSize = new Size(width, height + _titleBar.Height);
            MinimumSize = Size;
            MaximumSize = Size;
        }

        // Por si alguna ventana se abre con el launcher ya corriendo y el tema cambia
        // en Configuración sin tener que cerrar y volver a abrir Velin.
        public void RefreshTheme()
        {
            _titleBar.ApplyColors(Theme.TitleBarBg, Theme.TitleBarText);
            Theme.Apply(this);
            Invalidate(true);
        }
    }
}
