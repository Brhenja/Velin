using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Velin.Models
{
    public class VersionManifestRoot
    {
        [JsonPropertyName("latest")] public LatestVersions? Latest { get; set; }
        [JsonPropertyName("versions")] public List<VersionManifestEntry> Versions { get; set; } = new();
    }

    public class LatestVersions
    {
        [JsonPropertyName("release")] public string Release { get; set; } = "";
        [JsonPropertyName("snapshot")] public string Snapshot { get; set; } = "";
    }

    public class VersionManifestEntry
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("url")] public string Url { get; set; } = "";
    }

    public class MojangVersionJson
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("mainClass")] public string MainClass { get; set; } = "";
        [JsonPropertyName("inheritsFrom")] public string? InheritsFrom { get; set; }
        [JsonPropertyName("minecraftArguments")] public string? MinecraftArguments { get; set; }
        [JsonPropertyName("arguments")] public MojangArguments? Arguments { get; set; }
        [JsonPropertyName("downloads")] public MojangDownloads? Downloads { get; set; }
        [JsonPropertyName("libraries")] public List<MojangLibrary> Libraries { get; set; } = new();
        [JsonPropertyName("assetIndex")] public MojangAssetIndexRef? AssetIndex { get; set; }
        [JsonPropertyName("assets")] public string? Assets { get; set; }
        [JsonPropertyName("javaVersion")] public MojangJavaVersion? JavaVersion { get; set; }
    }

    public class MojangJavaVersion
    {
        [JsonPropertyName("majorVersion")] public int MajorVersion { get; set; } = 8;
    }

    public class MojangDownloads
    {
        [JsonPropertyName("client")] public MojangDownloadItem? Client { get; set; }
    }

    public class MojangDownloadItem
    {
        [JsonPropertyName("url")] public string Url { get; set; } = "";
        [JsonPropertyName("sha1")] public string Sha1 { get; set; } = "";
        [JsonPropertyName("size")] public long Size { get; set; }
    }

    public class MojangAssetIndexRef
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("url")] public string Url { get; set; } = "";
    }

    public class MojangArguments
    {
        [JsonPropertyName("game")] public List<JsonElement> Game { get; set; } = new();
        [JsonPropertyName("jvm")] public List<JsonElement> Jvm { get; set; } = new();
    }

    public class MojangLibrary
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("downloads")] public MojangLibraryDownloads? Downloads { get; set; }
        [JsonPropertyName("rules")] public List<MojangRule>? Rules { get; set; }
        [JsonPropertyName("natives")] public Dictionary<string, string>? Natives { get; set; }
        // Estilo Fabric/Quilt: sólo "name" (coordenadas maven) + "url" (repo base), sin "downloads"
        [JsonPropertyName("url")] public string? Url { get; set; }
    }

    public class MojangLibraryDownloads
    {
        [JsonPropertyName("artifact")] public MojangArtifact? Artifact { get; set; }
        [JsonPropertyName("classifiers")] public Dictionary<string, MojangArtifact>? Classifiers { get; set; }
    }

    public class MojangArtifact
    {
        [JsonPropertyName("path")] public string Path { get; set; } = "";
        [JsonPropertyName("url")] public string Url { get; set; } = "";
        [JsonPropertyName("sha1")] public string Sha1 { get; set; } = "";
        [JsonPropertyName("size")] public long Size { get; set; }
    }

    public class MojangRule
    {
        [JsonPropertyName("action")] public string Action { get; set; } = "allow";
        [JsonPropertyName("os")] public MojangOsRule? Os { get; set; }
    }

    public class MojangOsRule
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("arch")] public string? Arch { get; set; }
    }

    public class AssetIndexJson
    {
        [JsonPropertyName("objects")] public Dictionary<string, AssetObject> Objects { get; set; } = new();
    }

    public class AssetObject
    {
        [JsonPropertyName("hash")] public string Hash { get; set; } = "";
        [JsonPropertyName("size")] public long Size { get; set; }
    }
}
