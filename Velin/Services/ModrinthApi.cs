using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Velin.Models;

namespace Velin.Services
{
    public class ModrinthApi
    {
        private static readonly HttpClient _http = new HttpClient();
        private const string Base = "https://api.modrinth.com/v2";

        static ModrinthApi()
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.UserAgent);
        }

        public async Task<List<ModrinthSearchHit>> SearchAsync(string query, string mcVersion, string loader, int limit = 30)
        {
            var facets = $"[[\"project_type:mod\"],[\"versions:{mcVersion}\"],[\"categories:{loader}\"]]";
            var url = $"{Base}/search?query={Uri.EscapeDataString(query)}&limit={limit}&facets={Uri.EscapeDataString(facets)}";
            var json = await _http.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<ModrinthSearchResult>(json);
            return result?.Hits ?? new List<ModrinthSearchHit>();
        }

        // Búsqueda genérica por tipo de proyecto: "resourcepack", "shader", "datapack" o "modpack".
        // mcVersion = null no filtra por versión de Minecraft.
        public async Task<List<ModrinthSearchHit>> SearchProjectsAsync(string query, string projectType, string? mcVersion, int limit = 30)
        {
            var facets = new List<string> { $"[\"project_type:{projectType}\"]" };
            if (!string.IsNullOrEmpty(mcVersion)) facets.Add($"[\"versions:{mcVersion}\"]");
            var facetJson = "[" + string.Join(",", facets) + "]";
            var url = $"{Base}/search?query={Uri.EscapeDataString(query)}&limit={limit}&facets={Uri.EscapeDataString(facetJson)}";
            var json = await _http.GetStringAsync(url);
            var result = JsonSerializer.Deserialize<ModrinthSearchResult>(json);
            return result?.Hits ?? new List<ModrinthSearchHit>();
        }

        // Versiones de un proyecto (las más nuevas primero), opcionalmente sólo las de una versión de Minecraft.
        public async Task<List<ModrinthVersion>> GetProjectVersionsAsync(string projectId, string? mcVersion)
        {
            var url = $"{Base}/project/{Uri.EscapeDataString(projectId)}/version";
            if (!string.IsNullOrEmpty(mcVersion))
                url += "?game_versions=" + Uri.EscapeDataString($"[\"{mcVersion}\"]");
            var json = await _http.GetStringAsync(url);
            return JsonSerializer.Deserialize<List<ModrinthVersion>>(json) ?? new List<ModrinthVersion>();
        }

        public async Task<List<ModrinthVersion>> GetVersionsAsync(string projectId, string mcVersion, string loader)
        {
            var url = $"{Base}/project/{projectId}/version?game_versions=[\"{mcVersion}\"]&loaders=[\"{loader}\"]";
            var json = await _http.GetStringAsync(url);
            return JsonSerializer.Deserialize<List<ModrinthVersion>>(json) ?? new List<ModrinthVersion>();
        }

        // Busca en Modrinth la versión de mod a la que pertenece un archivo, por su hash SHA1.
        // Devuelve null si Modrinth no lo conoce (404); cualquier otro error se propaga.
        public async Task<ModrinthVersion?> GetVersionByFileHashAsync(string sha1)
        {
            using var resp = await _http.GetAsync($"{Base}/version_file/{sha1}?algorithm=sha1");
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ModrinthVersion>(json);
        }

        public async Task<ModrinthVersion?> GetVersionByIdAsync(string versionId)
        {
            using var resp = await _http.GetAsync($"{Base}/version/{Uri.EscapeDataString(versionId)}");
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ModrinthVersion>(json);
        }

        public async Task<string> GetProjectTitleAsync(string projectId)
        {
            try
            {
                var json = await _http.GetStringAsync($"{Base}/project/{Uri.EscapeDataString(projectId)}");
                var project = JsonSerializer.Deserialize<ModrinthProject>(json);
                return string.IsNullOrWhiteSpace(project?.Title) ? projectId : project!.Title;
            }
            catch { return projectId; }
        }
    }
}
