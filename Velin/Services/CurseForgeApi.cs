using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Velin.Models;

namespace Velin.Services
{
    public class CurseForgeException : Exception
    {
        public CurseForgeException(string message) : base(message) { }
    }

    // Cliente mínimo de la API de CurseForge (v1). Minecraft = gameId 432.
    public sealed class CurseForgeApi
    {
        private static readonly HttpClient Http = CreateClient();
        private const string Base = "https://api.curseforge.com/v1";
        private const int GameId = 432;
        private readonly string _key;

        public CurseForgeApi(string apiKey) { _key = apiKey; }

        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.UserAgent);
            return c;
        }

        // Cada usuario usa su propia key de CurseForge (se carga en Configuración).
        public static string ResolveKey(string? savedKey) => (savedKey ?? "").Trim();

        public static bool IsConfigured => ResolveKey(SettingsManager.Load().CurseForgeApiKey).Length > 0;

        public static CurseForgeApi FromSettings()
        {
            var key = ResolveKey(SettingsManager.Load().CurseForgeApiKey);
            if (key.Length == 0)
                throw new CurseForgeException("Falta tu API key de CurseForge. Cargala en Configuración (se consigue gratis en console.curseforge.com).");
            return new CurseForgeApi(key);
        }

        private async Task<string> SendAsync(HttpMethod method, string url, object? body = null)
        {
            using var req = new HttpRequestMessage(method, url);
            req.Headers.Add("x-api-key", _key);
            req.Headers.Accept.ParseAdd("application/json");
            if (body != null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var resp = await Http.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();
            if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden || resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new CurseForgeException("CurseForge rechazó la API key o no permite esta descarga (" + (int)resp.StatusCode + ").");
            if (!resp.IsSuccessStatusCode)
                throw new CurseForgeException("CurseForge respondió con error " + (int)resp.StatusCode + ".");
            return text;
        }

        // classId: 6 mods · 4471 modpacks · 12 resource packs · 6552 shaders. loaderType: 1 Forge, 4 Fabric, 5 Quilt, 6 NeoForge (0 = cualquiera).
        public async Task<List<CfMod>> SearchAsync(int classId, string query, string? mcVersion, int loaderType, int pageSize = 30)
        {
            var url = $"{Base}/mods/search?gameId={GameId}&classId={classId}&searchFilter={Uri.EscapeDataString(query)}&sortField=2&sortOrder=desc&pageSize={pageSize}";
            if (!string.IsNullOrEmpty(mcVersion)) url += "&gameVersion=" + Uri.EscapeDataString(mcVersion);
            if (loaderType > 0) url += "&modLoaderType=" + loaderType;
            var json = await SendAsync(HttpMethod.Get, url);
            return JsonSerializer.Deserialize<CfListResponse<CfMod>>(json)?.Data ?? new List<CfMod>();
        }

        public async Task<List<CfFile>> GetFilesAsync(int modId, string? mcVersion, int loaderType)
        {
            var url = $"{Base}/mods/{modId}/files?pageSize=50";
            if (!string.IsNullOrEmpty(mcVersion)) url += "&gameVersion=" + Uri.EscapeDataString(mcVersion);
            if (loaderType > 0) url += "&modLoaderType=" + loaderType;
            var json = await SendAsync(HttpMethod.Get, url);
            return JsonSerializer.Deserialize<CfListResponse<CfFile>>(json)?.Data ?? new List<CfFile>();
        }

        // Para los archivos cuyo downloadUrl viene vacío: sólo funciona si el autor permite la descarga.
        public async Task<string?> GetDownloadUrlAsync(int modId, int fileId)
        {
            try
            {
                var json = await SendAsync(HttpMethod.Get, $"{Base}/mods/{modId}/files/{fileId}/download-url");
                var url = JsonSerializer.Deserialize<CfItemResponse<string>>(json)?.Data;
                return string.IsNullOrWhiteSpace(url) ? null : url;
            }
            catch (CurseForgeException) { return null; }
        }

        public async Task<List<CfFile>> GetFilesByIdsAsync(List<int> fileIds)
        {
            var json = await SendAsync(HttpMethod.Post, $"{Base}/mods/files", new { fileIds });
            return JsonSerializer.Deserialize<CfListResponse<CfFile>>(json)?.Data ?? new List<CfFile>();
        }

        public async Task<List<CfMod>> GetModsByIdsAsync(List<int> modIds)
        {
            var json = await SendAsync(HttpMethod.Post, $"{Base}/mods", new { modIds, filterPcOnly = true });
            return JsonSerializer.Deserialize<CfListResponse<CfMod>>(json)?.Data ?? new List<CfMod>();
        }
    }
}
