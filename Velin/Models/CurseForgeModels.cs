using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Velin.Models
{
    public class CfListResponse<T> { [JsonPropertyName("data")] public List<T> Data { get; set; } = new(); }
    public class CfItemResponse<T> { [JsonPropertyName("data")] public T? Data { get; set; } }

    public class CfMod
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("summary")] public string Summary { get; set; } = "";
        [JsonPropertyName("classId")] public int ClassId { get; set; }
        [JsonPropertyName("downloadCount")] public double DownloadCount { get; set; }
        [JsonPropertyName("links")] public CfLinks? Links { get; set; }
        [JsonPropertyName("authors")] public List<CfAuthor> Authors { get; set; } = new();
    }

    public class CfLinks { [JsonPropertyName("websiteUrl")] public string WebsiteUrl { get; set; } = ""; }
    public class CfAuthor { [JsonPropertyName("name")] public string Name { get; set; } = ""; }

    public class CfFile
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("modId")] public int ModId { get; set; }
        [JsonPropertyName("displayName")] public string DisplayName { get; set; } = "";
        [JsonPropertyName("fileName")] public string FileName { get; set; } = "";
        [JsonPropertyName("releaseType")] public int ReleaseType { get; set; }   // 1 release, 2 beta, 3 alpha
        [JsonPropertyName("fileDate")] public DateTime FileDate { get; set; }
        [JsonPropertyName("fileLength")] public long FileLength { get; set; }
        [JsonPropertyName("downloadUrl")] public string? DownloadUrl { get; set; } // null si el autor no permite apps de terceros
        [JsonPropertyName("gameVersions")] public List<string> GameVersions { get; set; } = new();
        [JsonPropertyName("hashes")] public List<CfHash> Hashes { get; set; } = new();
        [JsonPropertyName("dependencies")] public List<CfDependency> Dependencies { get; set; } = new();
    }

    // relationType: 1 incluida, 2 opcional, 3 OBLIGATORIA, 4 herramienta, 5 incompatible, 6 incluida en el archivo
    public class CfDependency
    {
        [JsonPropertyName("modId")] public int ModId { get; set; }
        [JsonPropertyName("relationType")] public int RelationType { get; set; }
    }

    public class CfHash
    {
        [JsonPropertyName("value")] public string Value { get; set; } = "";
        [JsonPropertyName("algo")] public int Algo { get; set; }                  // 1 = SHA1, 2 = MD5
    }
}
