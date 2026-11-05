using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Velin.Services
{
    public class SkinException : Exception
    {
        public SkinException(string message) : base(message) { }
    }

    public class SkinInfo
    {
        public string Url { get; set; } = "";
        public bool Slim { get; set; }
        public string Variant => Slim ? "slim" : "classic";
    }

    // Skins de cuentas Microsoft: ver la actual, subir una nueva y restaurar la de por defecto.
    //  - Con token (api.minecraftservices.com): GET /minecraft/profile, POST /minecraft/profile/skins, DELETE .../skins/active.
    //  - Sin token (sessionserver.mojang.com): sólo la skin pública de un UUID (para mostrar la cara en la ventana principal).
    public static class MojangSkinService
    {
        private const string ServicesBase = "https://api.minecraftservices.com";
        private const long MaxDownloadBytes = 1024 * 1024;

        private static readonly HttpClient Http = Create();

        private static HttpClient Create()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.UserAgent);
            return c;
        }

        // --- Con token --------------------------------------------------------------------------

        public static async Task<SkinInfo?> GetActiveSkinAsync(string accessToken, CancellationToken ct = default)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, ServicesBase + "/minecraft/profile");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var resp = await Http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw new SkinException(DescribeError(resp, body));
            return ParseProfileSkin(body);
        }

        public static async Task UploadSkinAsync(string accessToken, byte[] png, bool slim, CancellationToken ct = default)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(slim ? "slim" : "classic"), "variant");
            var file = new ByteArrayContent(png);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "file", "skin.png");

            using var req = new HttpRequestMessage(HttpMethod.Post, ServicesBase + "/minecraft/profile/skins") { Content = form };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var resp = await Http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) throw new SkinException(DescribeError(resp, await resp.Content.ReadAsStringAsync(ct)));
        }

        public static async Task ResetSkinAsync(string accessToken, CancellationToken ct = default)
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, ServicesBase + "/minecraft/profile/skins/active");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var resp = await Http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) throw new SkinException(DescribeError(resp, await resp.Content.ReadAsStringAsync(ct)));
        }

        // --- Sin token --------------------------------------------------------------------------

        public static async Task<SkinInfo?> GetPublicSkinAsync(string uuid, CancellationToken ct = default)
        {
            var id = NormalizeUuid(uuid);
            if (id.Length == 0) return null;
            using var resp = await Http.GetAsync("https://sessionserver.mojang.com/session/minecraft/profile/" + id, ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NoContent || resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) throw new SkinException(DescribeError(resp, body));
            return ParseSessionSkin(body);
        }

        // Descarga el PNG de una skin (sólo de los servidores de texturas de Mojang).
        public static async Task<byte[]> DownloadSkinAsync(string url, CancellationToken ct = default)
        {
            if (!IsMojangTextureUrl(url)) throw new SkinException("La URL de la skin no es de Mojang.");
            var safe = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? "https://" + url.Substring(7) : url;
            using var resp = await Http.GetAsync(safe, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode) throw new SkinException("No se pudo descargar la skin (HTTP " + (int)resp.StatusCode + ").");
            if (resp.Content.Headers.ContentLength is > MaxDownloadBytes) throw new SkinException("La skin es demasiado grande.");
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            if (bytes.LongLength > MaxDownloadBytes) throw new SkinException("La skin es demasiado grande.");
            return bytes;
        }

        // --- Interpretación de respuestas (pública para poder probarla sin red) ---------------------

        // /minecraft/profile → {"id":..,"name":..,"skins":[{"id":..,"state":"ACTIVE","url":..,"variant":"CLASSIC"}]}
        public static SkinInfo? ParseProfileSkin(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("skins", out var skins) || skins.ValueKind != JsonValueKind.Array) return null;
                SkinInfo? first = null;
                foreach (var s in skins.EnumerateArray())
                {
                    var url = Str(s, "url");
                    if (url.Length == 0) continue;
                    var info = new SkinInfo { Url = url, Slim = string.Equals(Str(s, "variant"), "SLIM", StringComparison.OrdinalIgnoreCase) };
                    if (string.Equals(Str(s, "state"), "ACTIVE", StringComparison.OrdinalIgnoreCase)) return info;
                    first ??= info;
                }
                return first;
            }
            catch (JsonException) { return null; }
        }

        // sessionserver → {"properties":[{"name":"textures","value":"<base64 de {textures:{SKIN:{url,metadata:{model}}}}>"}]}
        public static SkinInfo? ParseSessionSkin(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Array) return null;
                foreach (var p in props.EnumerateArray())
                {
                    if (Str(p, "name") != "textures") continue;
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(Str(p, "value")));
                    using var tex = JsonDocument.Parse(decoded);
                    if (!tex.RootElement.TryGetProperty("textures", out var textures) || !textures.TryGetProperty("SKIN", out var skin)) return null;
                    var url = Str(skin, "url");
                    if (url.Length == 0) return null;
                    var slim = skin.TryGetProperty("metadata", out var meta) && string.Equals(Str(meta, "model"), "slim", StringComparison.OrdinalIgnoreCase);
                    return new SkinInfo { Url = url, Slim = slim };
                }
            }
            catch (Exception ex) when (ex is JsonException or FormatException) { }
            return null;
        }

        public static bool IsMojangTextureUrl(string? url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
            if (u.Scheme != Uri.UriSchemeHttps && u.Scheme != Uri.UriSchemeHttp) return false;
            return u.Host.Equals("textures.minecraft.net", StringComparison.OrdinalIgnoreCase);
        }

        // UUID sin guiones y en minúsculas; "" si no tiene el formato esperado.
        public static string NormalizeUuid(string? uuid)
        {
            var id = (uuid ?? "").Replace("-", "").Trim().ToLowerInvariant();
            if (id.Length != 32) return "";
            foreach (var c in id) if (!Uri.IsHexDigit(c)) return "";
            return id;
        }

        private static string Str(JsonElement e, string name) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";

        private static string DescribeError(HttpResponseMessage resp, string body)
        {
            var code = (int)resp.StatusCode;
            if (code == 401) return "La sesión de Microsoft venció o es inválida. Volvé a iniciar sesión desde Cuentas.";
            if (code == 429) return "Mojang limitó las solicitudes. Esperá unos minutos y probá de nuevo.";
            string detail = "";
            try
            {
                using var doc = JsonDocument.Parse(body);
                detail = Str(doc.RootElement, "errorMessage");
                if (detail.Length == 0) detail = Str(doc.RootElement, "error");
            }
            catch { }
            return "Mojang respondió con error " + code + (detail.Length > 0 ? ": " + detail : ".");
        }
    }
}
