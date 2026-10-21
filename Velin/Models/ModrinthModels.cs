using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Velin.Models
{
    public class ModrinthSearchResult
    {
        [JsonPropertyName("hits")] public List<ModrinthSearchHit> Hits { get; set; } = new();
    }

    public class ModrinthSearchHit
    {
        [JsonPropertyName("project_id")] public string ProjectId { get; set; } = "";
        [JsonPropertyName("slug")] public string Slug { get; set; } = "";
        [JsonPropertyName("title")] public string Title { get; set; } = "";
        [JsonPropertyName("description")] public string Description { get; set; } = "";
        [JsonPropertyName("author")] public string Author { get; set; } = "";
        [JsonPropertyName("downloads")] public long Downloads { get; set; }
    }

    public class ModrinthVersion
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("version_number")] public string VersionNumber { get; set; } = "";
        [JsonPropertyName("game_versions")] public List<string> GameVersions { get; set; } = new();
        [JsonPropertyName("loaders")] public List<string> Loaders { get; set; } = new();
        [JsonPropertyName("files")] public List<ModrinthFile> Files { get; set; } = new();
        [JsonPropertyName("date_published")] public string DatePublished { get; set; } = "";
        [JsonPropertyName("project_id")] public string ProjectId { get; set; } = "";
        [JsonPropertyName("dependencies")] public List<ModrinthDependency> Dependencies { get; set; } = new();
    }

    // Dependencia de una versión de mod. dependency_type: required, optional, incompatible, embedded.
    public class ModrinthDependency
    {
        [JsonPropertyName("version_id")] public string? VersionId { get; set; }
        [JsonPropertyName("project_id")] public string? ProjectId { get; set; }
        [JsonPropertyName("dependency_type")] public string DependencyType { get; set; } = "";
    }

    public class ModrinthProject
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("title")] public string Title { get; set; } = "";
    }

    public class ModrinthFile
    {
        [JsonPropertyName("url")] public string Url { get; set; } = "";
        [JsonPropertyName("filename")] public string Filename { get; set; } = "";
        [JsonPropertyName("primary")] public bool Primary { get; set; }
        [JsonPropertyName("hashes")] public ModrinthHashes? Hashes { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
    }

    public class ModrinthHashes
    {
        [JsonPropertyName("sha1")] public string Sha1 { get; set; } = "";
        [JsonPropertyName("sha512")] public string Sha512 { get; set; } = "";
    }

    public class InstalledModsIndex
    {
        public List<InstalledMod> Mods { get; set; } = new();
        // Hashes SHA1 de .jar locales que Modrinth ya dijo no conocer, para no volver a preguntar.
        public List<string> UnidentifiedSha1 { get; set; } = new();
    }

    public class InstalledMod
    {
        // De dónde vino el mod: "modrinth" (por defecto, también para los índices viejos) o "curseforge".
        public string Source { get; set; } = "modrinth";
        public string ProjectId { get; set; } = "";
        public string ProjectTitle { get; set; } = "";
        public string VersionId { get; set; } = "";
        public string Filename { get; set; } = "";
        public string Sha1 { get; set; } = "";
    }
}
