using System.Drawing;
using System.Windows.Forms;
using Velin.Services;

namespace Velin.UI
{
    // Escalado por DPI. Las ventanas de Velin se diseñaron a 96 DPI (100 %) con coordenadas fijas escritas a mano: el texto
    // crece solo con el DPI de la pantalla, pero los controles no, y en 125 % o 150 % el texto quedaba cortado.
    // Acá se escala el diseño completo de una ventana (posiciones, tamaños, altura de las filas de las listas y columnas)
    // por la misma razón que crece el texto. A 100 % no hace nada.
    public static class DpiScaler
    {
        // Un valor pensado a 96 DPI, ya convertido al DPI real del monitor del control (para lo que se arma en tiempo de ejecución).
        public static int Px(Control c, int value) => DpiMath.Scale(value, 96, c.DeviceDpi);

        public static void ScaleChildren(Control parent, float ratio)
        {
            foreach (Control child in parent.Controls) ScaleOne(child, ratio);
        }

        private static void ScaleOne(Control c, float r)
        {
            switch (c.Dock)
            {
                case DockStyle.None:
                    c.Bounds = new Rectangle(DpiMath.Scale(c.Left, r), DpiMath.Scale(c.Top, r), DpiMath.Scale(c.Width, r), DpiMath.Scale(c.Height, r));
                    break;
                case DockStyle.Top:
                case DockStyle.Bottom:
                    c.Height = DpiMath.Scale(c.Height, r);
                    break;
                case DockStyle.Left:
                case DockStyle.Right:
                    c.Width = DpiMath.Scale(c.Width, r);
                    break;
            }

            if (c is ListBox lb && lb.DrawMode == DrawMode.OwnerDrawFixed) lb.ItemHeight = DpiMath.Scale(lb.ItemHeight, r);
            else if (c is ListView lv) foreach (ColumnHeader col in lv.Columns) col.Width = DpiMath.Scale(col.Width, r);

            // Sólo se baja a los hijos de los contenedores propios; los controles compuestos (NumericUpDown, ComboBox...) se acomodan solos.
            if (c is Panel || c is UserControl || c is GroupBox) ScaleChildren(c, r);
        }
    }
}
