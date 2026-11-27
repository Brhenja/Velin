using System;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Panel único de "Contenido" de una instancia: mods, modpacks, mundos, servidores, packs, shaders,
    // datapacks, capturas, backups y consola. Mods, modpacks, resource packs y shaders se buscan en Modrinth o
    // CurseForge desde su propia ventana (selector de fuente arriba a la izquierda).
    public class InstanceContentForm : VelinFormBase
    {
        private readonly Instance _instance;
        private int _count;

        // withModsAndModpacks = false en el estilo nuevo de la ventana principal, donde "Mods" y "Modpacks" ya son botones propios.
        public InstanceContentForm(Instance instance, bool withModsAndModpacks = true) : base(Localization.T("ic_title", instance.Name))
        {
            _instance = instance;
            StartPosition = FormStartPosition.CenterParent;

            if (withModsAndModpacks)
            {
                Add("ic_mods", () => { using var f = new ModBrowserForm(_instance); f.ShowDialog(this); });
                Add("ic_modpacks", () => { using var f = new ModpackBrowserForm(); f.ShowDialog(this); });
            }
            Add("ic_worlds", () => { using var f = new WorldsForm(_instance); f.ShowDialog(this); });
            Add("ic_servers", () => { using var f = new ServersForm(_instance); f.ShowDialog(this); });
            Add("ic_resourcepacks", () => { using var f = new ContentBrowserForm(_instance, ContentKind.ResourcePack); f.ShowDialog(this); });
            Add("ic_shaders", () => { using var f = new ContentBrowserForm(_instance, ContentKind.Shader); f.ShowDialog(this); });
            Add("ic_datapacks", () => { using var f = new ContentBrowserForm(_instance, ContentKind.DataPack); f.ShowDialog(this); });
            Add("ic_screenshots", () => { using var f = new ScreenshotsForm(_instance); f.ShowDialog(this); });
            Add("ctx_backups", () => { using var f = new BackupsForm(_instance); f.ShowDialog(this); });
            Add("ctx_console", () => new LogViewerForm(Localization.T("console_title", _instance.Name), GameLauncher.LogPathFor(_instance)).Show());

            int rows = (_count + 1) / 2;
            LockSize(420, 12 + rows * 48 + 18);
            Theme.Apply(this);
        }

        // Dos columnas; cada botón va en el siguiente lugar libre.
        private void Add(string key, Action onClick)
        {
            int col = _count % 2, row = _count / 2;
            _count++;
            var b = new Button { Text = Localization.T(key) };
            b.SetBounds(12 + col * 200, 12 + row * 48, 192, 40);
            b.Click += (s, e) => onClick();
            Content.Controls.Add(b);
        }
    }
}
