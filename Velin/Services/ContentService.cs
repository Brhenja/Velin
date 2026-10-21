using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Velin.Models;

namespace Velin.Services
{
    public enum ContentKind { ResourcePack, Shader, DataPack }

    // Resource packs, shaders y datapacks de una instancia (carpetas del juego + instalación desde Modrinth).
    public static class ContentService
    {
        public static string ModrinthType(ContentKind kind) => kind switch
        {
            ContentKind.ResourcePack => "resourcepack",
            ContentKind.Shader => "shader",
            _ => "datapack"
        };

        // Los datapacks viven dentro de cada mundo: saves\<mundo>\datapacks.
        public static string FolderFor(Instance instance, ContentKind kind, string? world = null) => kind switch
        {
            ContentKind.ResourcePack => Path.Combine(instance.GameDir, "resourcepacks"),
            ContentKind.Shader => Path.Combine(instance.GameDir, "shaderpacks"),
            _ => Path.Combine(instance.GameDir, "saves", world ?? "", "datapacks")
        };

        // Archivos (.zip, etc.) y carpetas que hay en la carpeta del contenido, ordenados por nombre.
        public static List<string> ListInstalled(string folder)
        {
            var result = new List<string>();
            if (!Directory.Exists(folder)) return result;
            result.AddRange(Directory.GetDirectories(folder));
            result.AddRange(Directory.GetFiles(folder).Where(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase)));
            return result.OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static string SafeFileName(string name)
        {
            name = Path.GetFileName(name ?? "");
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Length == 0 ? "archivo" : name;
        }

        // Baja la versión más nueva compatible con la versión de Minecraft de la instancia (de Modrinth o CurseForge).
        public static async Task<string> InstallAsync(Instance instance, ContentSource source, ContentType type, string projectId, string title,
            string folder, Action<string>? status = null)
        {
            status?.Invoke($"Buscando versiones de {title}...");
            var versions = await ContentSources.GetVersionsAsync(source, type, projectId, instance);
            if (versions.Count == 0)
                throw new Exception($"No hay versiones de {title} para Minecraft {instance.McVersion}.");

            var version = versions[0];
            Directory.CreateDirectory(folder);
            var dest = Path.Combine(folder, SafeFileName(version.FileName));
            status?.Invoke($"Descargando {title}...");
            await ContentSources.DownloadAsync(version, dest);
            return dest;
        }

        public static void Remove(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            else if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
