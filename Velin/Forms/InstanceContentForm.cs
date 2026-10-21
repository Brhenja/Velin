using System;
using System.Windows.Forms;
using Velin.Models;
using Velin.Services;
using Velin.UI;

namespace Velin.Forms
{
    // Panel único de "Mods y contenido" de una instancia: mods, modpacks, mundos, servidores, packs, shaders,
    // datapacks, capturas, backups y consola. Mods, modpacks, resource packs y shaders se buscan en Modrinth o
    // CurseForge desde su propia ventana (selector de fuente arriba a la izquierda).
    public class InstanceContentForm : VelinFormBase
    {
        private readonly Instance _instance;

        public InstanceContentForm(Instance instance) : base(Localization.T("ic_title", instance.Name))
        {
            _instance = instance;
            LockSize(420, 270);
            StartPosition = FormStartPosition.CenterParent;

            Add("ic_mods", 0, 0, () => { using var f = new ModBrowserForm(_instance); f.ShowDialog(this); });
            Add("ic_modpacks", 1, 0, () => { using var f = new ModpackBrowserForm(); f.ShowDialog(this); });
            Add("ic_worlds", 0, 1, () => { using var f = new WorldsForm(_instance); f.ShowDialog(this); });
            Add("ic_servers", 1, 1, () => { using var f = new ServersForm(_instance); f.ShowDialog(this); });
            Add("ic_resourcepacks", 0, 2, () => { using var f = new ContentBrowserForm(_instance, ContentKind.ResourcePack); f.ShowDialog(this); });
            Add("ic_shaders", 1, 2, () => { using var f = new ContentBrowserForm(_instance, ContentKind.Shader); f.ShowDialog(this); });
            Add("ic_datapacks", 0, 3, () => { using var f = new ContentBrowserForm(_instance, ContentKind.DataPack); f.ShowDialog(this); });
            Add("ic_screenshots", 1, 3, () => { using var f = new ScreenshotsForm(_instance); f.ShowDialog(this); });
            Add("ctx_backups", 0, 4, () => { using var f = new BackupsForm(_instance); f.ShowDialog(this); });
            Add("ctx_console", 1, 4, () => new LogViewerForm(Localization.T("console_title", _instance.Name), GameLauncher.LogPathFor(_instance)).Show());

            Theme.Apply(this);
        }

        private void Add(string key, int col, int row, Action onClick)
        {
            var b = new Button { Text = Localization.T(key) };
            b.SetBounds(12 + col * 200, 12 + row * 48, 192, 40);
            b.Click += (s, e) => onClick();
            Content.Controls.Add(b);
        }
    }
}
