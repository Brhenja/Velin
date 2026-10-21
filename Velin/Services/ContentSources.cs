using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Velin.Models;

namespace Velin.Services
{
    // Capa común sobre Modrinth y CurseForge: todas las ventanas buscan, listan versiones y descargan por acá,
    // así las dos fuentes se comportan (y se ven) exactamente igual.
    public static class ContentSources
    {
        public static readonly ModrinthApi MrApi = new();

        public static string SourceKey(ContentSource s) => s == ContentSource.CurseForge ? "curseforge" : "modrinth";

        public static ContentSource ParseSource(string? key) =>
            string.Equals(key, "curseforge", StringComparison.OrdinalIgnoreCase) ? ContentSource.CurseForge : ContentSource.Modrinth;

        public static int CurseForgeClass(ContentType t) => t switch
        {
            ContentType.Mod => CurseForgeService.ClassMods,
            ContentType.ModPack => CurseForgeService.ClassModpacks,
            ContentType.ResourcePack => CurseForgeService.ClassResourcePacks,
            ContentType.Shader => CurseForgeService.ClassShaders,
            _ => 0
        };

        public static string ModrinthType(ContentType t) => t switch
        {
            ContentType.Mod => "mod",
            ContentType.ModPack => "modpack",
            ContentType.ResourcePack => "resourcepack",
            ContentType.Shader => "shader",
            _ => "datapack"
        };

        // Los datapacks sólo se buscan en Modrinth.
        public static bool Supports(ContentSource s, ContentType t) => s == ContentSource.Modrinth || t != ContentType.DataPack;

        public static ContentType ToType(ContentKind kind) => kind switch
        {
            ContentKind.ResourcePack => ContentType.ResourcePack,
            ContentKind.Shader => ContentType.Shader,
            _ => ContentType.DataPack
        };

        // --- Búsqueda ---------------------------------------------------------------------------

        // instance puede ser null sólo para modpacks (no dependen de la versión de la instancia).
        public static async Task<List<ContentProject>> SearchAsync(ContentSource source, ContentType type, string query, Instance? instance)
        {
            if (type != ContentType.ModPack && instance == null) throw new ArgumentNullException(nameof(instance));
            if (!Supports(source, type)) return new List<ContentProject>();

            if (source == ContentSource.Modrinth)
            {
                List<ModrinthSearchHit> hits = type == ContentType.Mod
                    ? await MrApi.SearchAsync(query, instance!.McVersion, ModManager.LoaderCategoryName(instance.Loader))
                    : await MrApi.SearchProjectsAsync(query, ModrinthType(type), type == ContentType.ModPack ? null : instance!.McVersion);
                return hits.Select(h => new ContentProject
                {
                    Source = ContentSource.Modrinth, Id = h.ProjectId, Title = h.Title, Author = h.Author,
                    Description = h.Description, Downloads = h.Downloads
                }).ToList();
            }

            var cf = CurseForgeApi.FromSettings();
            var mods = await cf.SearchAsync(CurseForgeClass(type), query,
                type == ContentType.ModPack ? null : instance!.McVersion,
                type == ContentType.Mod ? CurseForgeService.LoaderId(instance!.Loader) : 0);
            return mods.Select(m => new ContentProject
            {
                Source = ContentSource.CurseForge, Id = m.Id.ToString(CultureInfo.InvariantCulture), Title = m.Name,
                Author = string.Join(", ", m.Authors.Select(a => a.Name).Take(2)),
                Description = m.Summary, Downloads = (long)m.DownloadCount
            }).ToList();
        }

        // --- Versiones --------------------------------------------------------------------------

        // Las más nuevas primero. Para mods/packs/shaders sólo las compatibles con la instancia;
        // para modpacks, todas (el modpack trae su propia versión de Minecraft).
        public static async Task<List<ContentVersion>> GetVersionsAsync(ContentSource source, ContentType type, string projectId, Instance? instance)
        {
            if (type != ContentType.ModPack && instance == null) throw new ArgumentNullException(nameof(instance));
            bool filter = type != ContentType.ModPack;

            if (source == ContentSource.Modrinth)
            {
                List<ModrinthVersion> versions = type == ContentType.Mod
                    ? await MrApi.GetVersionsAsync(projectId, instance!.McVersion, ModManager.LoaderCategoryName(instance.Loader))
                    : await MrApi.GetProjectVersionsAsync(projectId, filter ? instance!.McVersion : null);
                return versions.Select(v => FromModrinth(v, type)).Where(v => v != null).Select(v => v!).ToList();
            }

            var cf = CurseForgeApi.FromSettings();
            var files = await cf.GetFilesAsync(int.Parse(projectId, CultureInfo.InvariantCulture),
                filter ? instance!.McVersion : null,
                type == ContentType.Mod ? CurseForgeService.LoaderId(instance!.Loader) : 0);
            return files
                .OrderBy(f => f.ReleaseType == 1 ? 0 : f.ReleaseType == 2 ? 1 : 2)   // estables primero
                .ThenByDescending(f => f.FileDate)
                .Select(f => FromCurseForge(f, projectId)).ToList();
        }

        internal static ContentVersion? FromModrinth(ModrinthVersion v, ContentType type)
        {
            var file = (type == ContentType.ModPack ? v.Files.FirstOrDefault(f => f.Filename.EndsWith(".mrpack", StringComparison.OrdinalIgnoreCase)) : null)
                ?? v.Files.FirstOrDefault(f => f.Primary) ?? v.Files.FirstOrDefault();
            if (file == null) return null;
            DateTime.TryParse(v.DatePublished, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date);
            return new ContentVersion
            {
                Source = ContentSource.Modrinth,
                Id = v.Id,
                ProjectId = v.ProjectId,
                Name = string.IsNullOrWhiteSpace(v.VersionNumber) ? v.Name : v.VersionNumber,
                FileName = Path.GetFileName(file.Filename),
                Url = file.Url,
                Sha1 = file.Hashes?.Sha1,
                Size = file.Size,
                GameVersions = v.GameVersions,
                Loaders = v.Loaders,
                Date = date,
                Dependencies = v.Dependencies
                    .Where(d => string.Equals(d.DependencyType, "required", StringComparison.OrdinalIgnoreCase))
                    .Select(d => new ContentDependency { ProjectId = d.ProjectId ?? "", VersionId = d.VersionId }).ToList()
            };
        }

        internal static ContentVersion FromCurseForge(CfFile f, string? projectId)
        {
            return new ContentVersion
            {
                Source = ContentSource.CurseForge,
                Id = f.Id.ToString(CultureInfo.InvariantCulture),
                ProjectId = f.ModId > 0 ? f.ModId.ToString(CultureInfo.InvariantCulture) : projectId ?? "",
                Name = string.IsNullOrWhiteSpace(f.DisplayName) ? f.FileName : f.DisplayName,
                FileName = Path.GetFileName(f.FileName),
                Url = f.DownloadUrl,
                Sha1 = f.Hashes.FirstOrDefault(h => h.Algo == 1)?.Value,
                Size = f.FileLength,
                GameVersions = f.GameVersions,
                Date = f.FileDate,
                ReleaseType = f.ReleaseType,
                Dependencies = f.Dependencies.Where(d => d.RelationType == 3)
                    .Select(d => new ContentDependency { ProjectId = d.ModId.ToString(CultureInfo.InvariantCulture) }).ToList()
            };
        }

        // --- Descarga ---------------------------------------------------------------------------

        public static async Task DownloadAsync(ContentVersion v, string destPath)
        {
            var url = v.Url;
            if (string.IsNullOrEmpty(url) && v.Source == ContentSource.CurseForge)
            {
                url = await CurseForgeApi.FromSettings().GetDownloadUrlAsync(
                    int.Parse(v.ProjectId, CultureInfo.InvariantCulture), int.Parse(v.Id, CultureInfo.InvariantCulture));
                if (url == null)
                    throw new CurseForgeException($"El autor de \"{v.Name}\" no permite descargarlo desde otros programas. Bajalo a mano desde curseforge.com.");
            }
            if (string.IsNullOrEmpty(url)) throw new Exception($"\"{v.Name}\" no tiene un enlace de descarga.");
            await FileDownloader.DownloadAsync(url, destPath, string.IsNullOrEmpty(v.Sha1) ? null : v.Sha1, v.Size);
        }

        public static async Task<string> GetProjectTitleAsync(ContentSource source, string projectId)
        {
            if (source == ContentSource.Modrinth) return await MrApi.GetProjectTitleAsync(projectId);
            var mods = await CurseForgeApi.FromSettings().GetModsByIdsAsync(new List<int> { int.Parse(projectId, CultureInfo.InvariantCulture) });
            return mods.FirstOrDefault()?.Name ?? projectId;
        }

        // --- Modpacks ---------------------------------------------------------------------------

        // Baja la versión elegida del modpack y la instala como instancia NUEVA.
        public static async Task<Instance> InstallModpackAsync(ContentVersion version, IProgress<string>? progress,
            IProgress<double>? percent, Action<string>? onWarning)
        {
            var cache = Path.Combine(PathService.CacheDir, "modpacks");
            Directory.CreateDirectory(cache);
            var zipPath = Path.Combine(cache, ContentService.SafeFileName(version.FileName));
            try
            {
                progress?.Report($"Descargando {version.Name}...");
                await DownloadAsync(version, zipPath);
                return version.Source == ContentSource.Modrinth
                    ? await ModpackImporter.ImportMrpackAsync(zipPath, progress, percent, onWarning)
                    : await CurseForgeService.ImportModpackAsync(zipPath, CurseForgeApi.FromSettings(), progress, percent, onWarning);
            }
            finally { try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { } }
        }
    }
}
