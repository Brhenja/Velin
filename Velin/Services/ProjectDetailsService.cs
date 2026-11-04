using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using Velin.Models;

namespace Velin.Services
{
    public class ProjectLink
    {
        public string Label { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public class ProjectImage
    {
        public string Url { get; set; } = "";
        public string FallbackUrl { get; set; } = ""; // por si el formato principal no se puede mostrar (p. ej. WebP)
        public string Title { get; set; } = "";
    }

    // Todo lo que se muestra en la ventana de detalles de un mod / pack / shader, ya normalizado
    // (igual para Modrinth y CurseForge).
    public class ProjectDetails
    {
        public ContentSource Source { get; set; }
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Author { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Body { get; set; } = "";
        public bool BodyIsHtml { get; set; }
        public string IconUrl { get; set; } = "";
        public long Downloads { get; set; }
        public string PageUrl { get; set; } = "";
        public List<ProjectImage> Gallery { get; } = new();
        public List<ProjectLink> Links { get; } = new();
    }

    public static class ProjectDetailsService
    {
        public static async Task<ProjectDetails> GetAsync(ContentSource source, string projectId, string fallbackAuthor = "")
        {
            return source == ContentSource.Modrinth
                ? ParseModrinth(await ContentSources.MrApi.GetProjectJsonAsync(projectId), fallbackAuthor)
                : await GetCurseForgeAsync(projectId, fallbackAuthor);
        }

        private static async Task<ProjectDetails> GetCurseForgeAsync(string projectId, string fallbackAuthor)
        {
            if (!int.TryParse(projectId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var modId))
                throw new ArgumentException("Id de CurseForge inválido.");
            var cf = CurseForgeApi.FromSettings();
            var modJson = await cf.GetModJsonAsync(modId);
            string descJson = "";
            try { descJson = await cf.GetModDescriptionJsonAsync(modId); } catch { /* sin descripción larga: se usa el resumen */ }
            return ParseCurseForge(modJson, descJson, fallbackAuthor);
        }

        // --- Modrinth ---
        public static ProjectDetails ParseModrinth(string json, string fallbackAuthor = "")
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            var d = new ProjectDetails
            {
                Source = ContentSource.Modrinth,
                Id = Str(r, "id"),
                Title = Str(r, "title"),
                Author = fallbackAuthor,
                Summary = Str(r, "description"),
                Body = Str(r, "body"),
                BodyIsHtml = false,
                IconUrl = Str(r, "icon_url"),
                Downloads = r.TryGetProperty("downloads", out var dl) && dl.ValueKind == JsonValueKind.Number ? dl.GetInt64() : 0
            };
            var slug = Str(r, "slug");
            var type = Str(r, "project_type");
            if (slug.Length > 0)
                d.PageUrl = $"https://modrinth.com/{(type.Length > 0 ? type : "mod")}/{Uri.EscapeDataString(slug)}";

            AddLink(d, "Modrinth", d.PageUrl);
            AddLink(d, "Código fuente", Str(r, "source_url"));
            AddLink(d, "Wiki", Str(r, "wiki_url"));
            AddLink(d, "Reportar problemas", Str(r, "issues_url"));
            AddLink(d, "Discord", Str(r, "discord_url"));

            if (r.TryGetProperty("gallery", out var gallery) && gallery.ValueKind == JsonValueKind.Array)
            {
                foreach (var g in gallery.EnumerateArray())
                {
                    var url = Str(g, "url");
                    var raw = Str(g, "raw_url");
                    if (url.Length == 0) { url = raw; raw = ""; }
                    if (url.Length == 0) continue;
                    d.Gallery.Add(new ProjectImage { Url = url, FallbackUrl = raw, Title = Str(g, "title") });
                }
            }
            return d;
        }

        // --- CurseForge ---
        public static ProjectDetails ParseCurseForge(string modJson, string descriptionJson, string fallbackAuthor = "")
        {
            using var doc = JsonDocument.Parse(modJson);
            var m = doc.RootElement.TryGetProperty("data", out var data) ? data : doc.RootElement;
            var d = new ProjectDetails
            {
                Source = ContentSource.CurseForge,
                Id = m.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt32().ToString(CultureInfo.InvariantCulture) : "",
                Title = Str(m, "name"),
                Summary = Str(m, "summary"),
                Author = fallbackAuthor,
                BodyIsHtml = true,
                Downloads = m.TryGetProperty("downloadCount", out var dc) && dc.ValueKind == JsonValueKind.Number ? (long)dc.GetDouble() : 0
            };
            if (d.Author.Length == 0 && m.TryGetProperty("authors", out var authors) && authors.ValueKind == JsonValueKind.Array)
                foreach (var a in authors.EnumerateArray()) { d.Author = Str(a, "name"); break; }

            if (m.TryGetProperty("logo", out var logo) && logo.ValueKind == JsonValueKind.Object)
            {
                d.IconUrl = Str(logo, "thumbnailUrl");
                if (d.IconUrl.Length == 0) d.IconUrl = Str(logo, "url");
            }

            if (m.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Object)
            {
                d.PageUrl = Str(links, "websiteUrl");
                AddLink(d, "CurseForge", d.PageUrl);
                AddLink(d, "Código fuente", Str(links, "sourceUrl"));
                AddLink(d, "Wiki", Str(links, "wikiUrl"));
                AddLink(d, "Reportar problemas", Str(links, "issuesUrl"));
            }

            if (m.TryGetProperty("screenshots", out var shots) && shots.ValueKind == JsonValueKind.Array)
                foreach (var s in shots.EnumerateArray())
                {
                    var url = Str(s, "url");
                    if (url.Length == 0) continue;
                    var thumb = Str(s, "thumbnailUrl");
                    d.Gallery.Add(new ProjectImage { Url = url, FallbackUrl = thumb, Title = Str(s, "title") });
                }

            if (!string.IsNullOrWhiteSpace(descriptionJson))
            {
                try
                {
                    using var dd = JsonDocument.Parse(descriptionJson);
                    if (dd.RootElement.TryGetProperty("data", out var html) && html.ValueKind == JsonValueKind.String)
                        d.Body = html.GetString() ?? "";
                }
                catch (JsonException) { }
            }
            return d;
        }

        private static string Str(JsonElement e, string name) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";

        // Solo se aceptan enlaces http(s): nada de file:// ni otros esquemas raros que vengan de una descripción ajena.
        public static bool IsSafeUrl(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp);

        private static void AddLink(ProjectDetails d, string label, string url)
        {
            if (IsSafeUrl(url)) d.Links.Add(new ProjectLink { Label = label, Url = url });
        }
    }
}
