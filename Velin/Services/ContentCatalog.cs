using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Velin.Models;

namespace Velin.Services
{
    // Datos para los filtros del buscador: categorías de cada fuente y lista de versiones de Minecraft. Se piden una sola vez por sesión.
    public static class ContentCatalog
    {
        private static readonly Dictionary<string, List<ContentCategory>> CategoryCache = new();
        private static List<string>? _versions;

        public static async Task<List<ContentCategory>> GetCategoriesAsync(ContentSource source, ContentType type)
        {
            var key = source + ":" + type;
            lock (CategoryCache) if (CategoryCache.TryGetValue(key, out var cached)) return cached;

            List<ContentCategory> list;
            if (source == ContentSource.Modrinth)
            {
                list = type == ContentType.DataPack
                    ? new List<ContentCategory>()     // los datapacks no tienen una lista propia de categorías
                    : ParseModrinthCategories(await ContentSources.MrApi.GetCategoriesJsonAsync(), ContentSources.ModrinthType(type));
            }
            else
            {
                var classId = ContentSources.CurseForgeClass(type);
                list = classId == 0 ? new List<ContentCategory>() : ParseCurseForgeCategories(await CurseForgeApi.FromSettings().GetCategoriesJsonAsync(classId), classId);
            }
            lock (CategoryCache) CategoryCache[key] = list;
            return list;
        }

        // Versiones "release" de Minecraft, de la más nueva a la más vieja.
        public static async Task<List<string>> GetMinecraftVersionsAsync()
        {
            if (_versions != null) return _versions;
            var manifest = await MinecraftDownloader.GetManifestAsync();
            _versions = manifest.Versions.Where(v => v.Type == "release").Select(v => v.Id).ToList();
            return _versions;
        }

        // [{"name":"adventure","project_type":"mod","header":"categories"}, ...]
        public static List<ContentCategory> ParseModrinthCategories(string json, string projectType)
        {
            var result = new List<ContentCategory>();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return result;
            foreach (var c in doc.RootElement.EnumerateArray())
            {
                if (Str(c, "project_type") != projectType) continue;
                var header = Str(c, "header");
                if (header != "categories" && header != "resolutions") continue;   // sin loaders ni "features"
                var name = Str(c, "name");
                if (name.Length > 0) result.Add(new ContentCategory { Id = name, Name = Pretty(name) });
            }
            return result.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        // {"data":[{"id":406,"name":"World Gen","classId":6,"isClass":false,"parentCategoryId":6}, ...]}
        public static List<ContentCategory> ParseCurseForgeCategories(string json, int classId)
        {
            var result = new List<ContentCategory>();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return result;
            foreach (var c in data.EnumerateArray())
            {
                if (c.TryGetProperty("isClass", out var isClass) && isClass.ValueKind == JsonValueKind.True) continue;
                var cls = c.TryGetProperty("classId", out var ci) && ci.ValueKind == JsonValueKind.Number ? ci.GetInt32() : 0;
                var parent = c.TryGetProperty("parentCategoryId", out var pi) && pi.ValueKind == JsonValueKind.Number ? pi.GetInt32() : 0;
                if (cls != classId || parent != classId) continue;   // sólo el primer nivel, para que la lista sea corta
                var id = c.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt32() : 0;
                var name = Str(c, "name");
                if (id > 0 && name.Length > 0) result.Add(new ContentCategory { Id = id.ToString(CultureInfo.InvariantCulture), Name = name });
            }
            return result.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        // "game-mechanics" -> "Game mechanics"
        public static string Pretty(string slug)
        {
            var s = slug.Replace('-', ' ').Replace('_', ' ').Trim();
            return s.Length == 0 ? slug : char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private static string Str(JsonElement e, string name) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
    }
}
