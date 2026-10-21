using System;
using System.Collections.Generic;
using System.IO;
using Velin.Models;

namespace Velin.Services
{
    public class ServerEntry
    {
        public string Name { get; set; } = "";
        public string Ip { get; set; } = "";
        // Resto de los datos que guarda el juego (ícono, hidden, acceptTextures...): se conservan al guardar.
        public NbtCompound Raw { get; set; } = new();
    }

    // Lista de servidores de una instancia (archivo servers.dat del juego).
    public static class ServerListService
    {
        public static string FilePath(Instance instance) => Path.Combine(instance.GameDir, "servers.dat");

        public static List<ServerEntry> Load(Instance instance)
        {
            var result = new List<ServerEntry>();
            var path = FilePath(instance);
            if (!File.Exists(path)) return result;

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var root = Nbt.ReadRoot(fs, out _);
            if (root.Get("servers")?.Value is not NbtList list) return result;
            foreach (var item in list.Items)
            {
                if (item.Value is not NbtCompound c) continue;
                result.Add(new ServerEntry { Name = c.GetString("name"), Ip = c.GetString("ip"), Raw = c });
            }
            return result;
        }

        // Guarda la lista. Antes deja una copia del archivo anterior (servers.dat.velin-bak).
        public static void Save(Instance instance, List<ServerEntry> servers)
        {
            Directory.CreateDirectory(instance.GameDir);
            var path = FilePath(instance);

            NbtCompound root = new();
            if (File.Exists(path))
            {
                try { File.Copy(path, path + ".velin-bak", true); } catch { }
                try
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    root = Nbt.ReadRoot(fs, out _);
                }
                catch { root = new NbtCompound(); }
            }

            var list = new NbtList { ElementType = NbtType.Compound };
            foreach (var s in servers)
            {
                s.Raw.Set("name", NbtTag.String(s.Name));
                s.Raw.Set("ip", NbtTag.String(s.Ip));
                list.Items.Add(NbtTag.Compound(s.Raw));
            }
            root.Set("servers", NbtTag.List(list));

            var tmp = path + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                Nbt.WriteRoot(fs, "", root);
            File.Move(tmp, path, true);
        }
    }
}
