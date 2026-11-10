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

    // Una página de resultados de búsqueda (las dos fuentes se pueden recorrer por páginas).
    public class ContentPage
    {
        // CurseForge no deja pasar de los primeros 10.000 resultados (index + pageSize <= 10000).
        public const int MaxReachable = 10000;

        public List<ContentProject> Items { get; set; } = new();
        public int Page { get; set; }          // desde 0
        public int PageSize { get; set; } = 30;
        public int Total { get; set; }         // total de resultados que informa la fuente

        public int TotalPages
        {
            get
            {
                if (PageSize <= 0) return 1;
                var reachable = Math.Min(Total, MaxReachable);
                return Math.Max(1, (reachable + PageSize - 1) / PageSize);
            }
        }

        public bool HasPrevious => Page > 0;
        public bool HasNext => Page + 1 < TotalPages;
    }

    public enum ContentSort { Relevance, Downloads, Newest, Updated }

    // Filtros del buscador. Una vacío (null) significa "cualquiera".
    public class ContentFilter
    {
        public ContentSort Sort { get; set; } = ContentSort.Relevance;
        public string? CategoryId { get; set; }          // Modrinth: nombre de la categoría; CurseForge: id numérico
        public string? McVersion { get; set; }
        public ModLoaderType? Loader { get; set; }
    }

    public class ContentCategory
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public override string ToString() => Name;
    }
}
