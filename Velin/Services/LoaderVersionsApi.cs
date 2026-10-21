using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Velin.Services
{
    // Compara versiones por número ("21.1.219" > "21.1.99"), no por texto.
    public sealed class VersionComparer : IComparer<string>
    {
        public static readonly VersionComparer Instance = new();

        public int Compare(string? a, string? b)
        {
            var pa = Parts(a ?? ""); var pb = Parts(b ?? "");
            for (int i = 0; i < Math.Max(pa.Count, pb.Count); i++)
            {
                long x = i < pa.Count ? pa[i] : 0, y = i < pb.Count ? pb[i] : 0;
                if (x != y) return x.CompareTo(y);
            }
            return string.CompareOrdinal(a, b);
        }

        private static List<long> Parts(string v)
        {
            var list = new List<long>();
            foreach (Match m in Regex.Matches(v, @"\d+"))
                if (long.TryParse(m.Value, out var n)) list.Add(n);
            return list;
        }
    }

    public static class LoaderVersionsApi
    {
        private static readonly HttpClient _http = new HttpClient();

        public static async Task<List<string>> GetFabricVersionsAsync(string mcVersion)
        {
            var json = await _http.GetStringAsync($"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}");
            using var doc = JsonDocument.Parse(json);
            var list = new List<string>();
            foreach (var el in doc.RootElement.EnumerateArray())
                list.Add(el.GetProperty("loader").GetProperty("version").GetString()!);
            return list;
        }

        public static async Task<List<string>> GetQuiltVersionsAsync(string mcVersion)
        {
            var json = await _http.GetStringAsync($"https://meta.quiltmc.org/v3/versions/loader/{mcVersion}");
            using var doc = JsonDocument.Parse(json);
            var list = new List<string>();
            foreach (var el in doc.RootElement.EnumerateArray())
                list.Add(el.GetProperty("loader").GetProperty("version").GetString()!);
            return list;
        }

        public static async Task<List<string>> GetForgeVersionsAsync(string mcVersion)
        {
            var json = await _http.GetStringAsync("https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json");
            using var doc = JsonDocument.Parse(json);
            var promos = doc.RootElement.GetProperty("promos");
            var list = new List<string>();
            foreach (var prop in promos.EnumerateObject())
            {
                if (prop.Name.StartsWith(mcVersion + "-"))
                    list.Add(prop.Value.GetString()!);
            }
            return list.Distinct().OrderByDescending(v => v, VersionComparer.Instance).ToList();
        }

        // NeoForge para 1.20.1 se publicó con el esquema viejo (artefacto "net/neoforged/forge", versión
        // "1.20.1-47.1.x"). Desde 1.20.2 usa "net/neoforged/neoforge" con versiones "20.2.x", "21.1.x"...
        public const string LegacyNeoForgeMc = "1.20.1";

        public static bool IsLegacyNeoForge(string mcVersion, string loaderVersion) =>
            mcVersion == LegacyNeoForgeMc || loaderVersion.StartsWith(LegacyNeoForgeMc + "-");

        // Los modpacks de CurseForge guardan "47.1.106" a secas; Velin usa "1.20.1-47.1.106" para 1.20.1.
        public static string NormalizeNeoForgeVersion(string mcVersion, string loaderVersion) =>
            mcVersion == LegacyNeoForgeMc && !loaderVersion.StartsWith(LegacyNeoForgeMc + "-")
                ? LegacyNeoForgeMc + "-" + loaderVersion
                : loaderVersion;

        public static async Task<List<string>> GetNeoForgeVersionsAsync(string mcVersion)
        {
            if (mcVersion == LegacyNeoForgeMc)
            {
                var legacyXml = await _http.GetStringAsync("https://maven.neoforged.net/releases/net/neoforged/forge/maven-metadata.xml");
                var prefix = LegacyNeoForgeMc + "-";
                var all = XDocument.Parse(legacyXml).Descendants("version").Select(v => v.Value)
                    .Where(v => v.StartsWith(prefix)).ToList();
                var stableLegacy = all.Where(v => !v.Substring(prefix.Length).Contains('-')).ToList();
                return (stableLegacy.Count > 0 ? stableLegacy : all)
                    .OrderByDescending(v => v, VersionComparer.Instance).ToList();
            }

            var xml = await _http.GetStringAsync("https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml");
            var doc = XDocument.Parse(xml);
            var versions = doc.Descendants("version").Select(v => v.Value).ToList();
            // NeoForge versiona tipo "21.1.100" para Minecraft "1.21.1"
            var suffix = mcVersion.StartsWith("1.") ? mcVersion.Substring(2) : mcVersion;
            var matching = versions.Where(v => v.StartsWith(suffix + ".")).ToList();
            // Las versiones estables van primero; las beta/alpha sólo si no hay ninguna estable.
            var stable = matching.Where(v => !v.Contains('-')).ToList();
            var pick = stable.Count > 0 ? stable : matching;
            return pick.OrderByDescending(v => v, VersionComparer.Instance).ToList();
        }
    }
}
