using System.Drawing;
using System.Windows.Forms;
using Velin.Models;
using Velin.UI;

namespace Velin.Forms
{
    // Piezas de interfaz compartidas por las ventanas de mods, modpacks, resource packs, shaders y datapacks,
    // para que se vean y se usen EXACTAMENTE igual con Modrinth y con CurseForge.
    internal static class ContentUi
    {
        public static ContentSource SourceOf(ComboBox combo) =>
            combo.SelectedIndex == 1 ? ContentSource.CurseForge : ContentSource.Modrinth;

        public static string SourceName(ContentSource s) => s == ContentSource.CurseForge ? "CurseForge" : "Modrinth";

        public static string HintText(ContentSource s) => Localization.T("btn_search") + " (" + SourceName(s) + ")";

        // Fila superior idéntica en todas las ventanas: [Fuente ▼] [buscador] [Buscar] y debajo el texto de ayuda.
        // Los datapacks sólo existen en Modrinth: el selector queda fijo.
        public static Label AddSearchBar(Control content, ComboBox source, TextBox search, Button button, bool lockToModrinth)
        {
            source.DropDownStyle = ComboBoxStyle.DropDownList;
            source.Items.Clear();
            source.Items.Add("Modrinth");
            source.Items.Add("CurseForge");
            source.SelectedIndex = 0;
            source.Enabled = !lockToModrinth;
            source.SetBounds(12, 14, 112, 24);
            content.Controls.Add(source);

            search.SetBounds(130, 14, 206, 26);
            content.Controls.Add(search);

            button.Text = Localization.T("btn_search");
            button.SetBounds(342, 12, 90, 28);
            content.Controls.Add(button);

            var hint = new Label
            {
                Text = HintText(ContentSource.Modrinth),
                AutoSize = true,
                Location = new Point(12, 50),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic)
            };
            content.Controls.Add(hint);
            return hint;
        }
    }
}
