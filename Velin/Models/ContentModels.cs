using System;
using System.Collections.Generic;
using System.Linq;

namespace Velin.Models
{
    public enum ContentSource { Modrinth, CurseForge }
    public enum ContentType { Mod, ModPack, ResourcePack, Shader, DataPack }

    // Un proyecto (mod, modpack, pack...) encontrado en Modrinth o CurseForge, con el mismo formato para las dos fuentes.
    public class ContentProject
    {
        public ContentSource Source { get; set; }
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public long Downloads { get; set; }
        public override string ToString() => $"{Title}\n{Author} · {Downloads:N0}";
    }

    public class ContentDependency
    {
        public string ProjectId { get; set; } = "";
        public string? VersionId { get; set; }
    }

    // Una versión descargable de un proyecto (un archivo), también igual para las dos fuentes.
    public class ContentVersion
    {
        public ContentSource Source { get; set; }
        public string Id { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public string Name { get; set; } = "";
        public string FileName { get; set; } = "";
        public string? Url { get; set; }              // en CurseForge puede venir vacío (se pide aparte)
        public string? Sha1 { get; set; }
        public long Size { get; set; }
        public List<string> GameVersions { get; set; } = new();
        public List<string> Loaders { get; set; } = new();
        public DateTime Date { get; set; }
        public int ReleaseType { get; set; } = 1;     // 1 release, 2 beta, 3 alpha
        public List<ContentDependency> Dependencies { get; set; } = new();   // sólo las OBLIGATORIAS

        public override string ToString()
        {
            var mc = string.Join("/", GameVersions.Where(g => g.Length > 0 && char.IsDigit(g[0])).Take(2));
            var text = Name;
            if (mc.Length > 0) text += " · MC " + mc;
            if (Loaders.Count > 0) text += " · " + string.Join("/", Loaders.Take(2));
            return text;
        }
    }
}
