using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Velin.Services
{
    public class MicrosoftAuthException : Exception
    {
        public MicrosoftAuthException(string message) : base(message) { }
    }

    public class DeviceCodeInfo
    {
        public string DeviceCode { get; set; } = "";
        public string UserCode { get; set; } = "";
        public string VerificationUri { get; set; } = "";
        public int ExpiresInSeconds { get; set; } = 900;
        public int IntervalSeconds { get; set; } = 5;
    }

    public class MicrosoftAuthResult
    {
        public string MsRefreshToken { get; set; } = "";
        public string McAccessToken { get; set; } = "";
        public DateTime McExpiresUtc { get; set; }
        public string Username { get; set; } = "";
        public string Uuid { get; set; } = "";
    }

    // Login de Microsoft para Minecraft con "device code flow" (el usuario escribe un código en
    // microsoft.com/link; no hace falta ventana de login embebida ni client secret):
    //   código de dispositivo -> token Microsoft -> Xbox Live -> XSTS -> token Minecraft -> perfil.
    // Requiere un Client ID de una app de Azure APROBADA por Mojang (ver README / pantalla Cuentas).
    public static class MicrosoftAuth
    {
        private static readonly HttpClient Http = CreateClient();
        private const string Scope = "XboxLive.signin offline_access";
        private const string Tenant = "consumers";

        private static HttpClient CreateClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.UserAgent);
            return c;
        }

        // El Client ID que se usa: el que trae Velin adentro (MicrosoftAuthConfig) o, sólo para pruebas del
        // desarrollador, el que se escriba a mano en settings.json ("MicrosoftClientId").
        public static string ResolveClientId(string? settingsOverride) =>
            !string.IsNullOrWhiteSpace(settingsOverride) ? settingsOverride.Trim() : MicrosoftAuthConfig.ClientId;

        public static bool IsConfigured(string? settingsOverride) => ResolveClientId(settingsOverride).Length > 0;

        // Login "normal": se abre el navegador con la página de Microsoft; al terminar, Microsoft redirige a un
        // puerto local de esta PC y Velin recibe el código (OAuth2 con PKCE, sin secretos ni códigos que tipear).
        public static async Task<MicrosoftAuthResult> LoginWithBrowserAsync(string clientId, Action<string> openUrl, CancellationToken ct = default)
        {
            RequireClientId(clientId);
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                var redirect = $"http://localhost:{port}";
                var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
                var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
                var state = Base64Url(RandomNumberGenerator.GetBytes(16));

                var url = $"https://login.microsoftonline.com/{Tenant}/oauth2/v2.0/authorize"
                    + $"?client_id={Uri.EscapeDataString(clientId.Trim())}&response_type=code"
                    + $"&redirect_uri={Uri.EscapeDataString(redirect)}&response_mode=query"
                    + $"&scope={Uri.EscapeDataString(Scope)}&state={state}"
                    + $"&code_challenge={challenge}&code_challenge_method=S256&prompt=select_account";
                openUrl(url);

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromMinutes(5));
                string code;
                try { code = await WaitForAuthCodeAsync(listener, state, timeout.Token); }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                { throw new MicrosoftAuthException("Se agotó el tiempo de espera (5 minutos). Probá de nuevo."); }

                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId.Trim(),
                    ["scope"] = Scope,
                    ["code"] = code,
                    ["redirect_uri"] = redirect,
                    ["grant_type"] = "authorization_code",
                    ["code_verifier"] = verifier
                });
                using var resp = await Http.PostAsync($"https://login.microsoftonline.com/{Tenant}/oauth2/v2.0/token", body, ct);
                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = ParseJson(json);
                var root = doc.RootElement;
                if (!resp.IsSuccessStatusCode || !root.TryGetProperty("access_token", out var at))
                    throw new MicrosoftAuthException(DescribeOAuthError(root, "Microsoft rechazó el inicio de sesión."));

                var refresh = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "";
                return await FinishAsync(at.GetString() ?? "", refresh, ct);
            }
            finally
            {
                listener.Stop();
            }
        }

        // Atiende el navegador hasta que llega la redirección con el código (ignora favicon y cosas raras).
        private static async Task<string> WaitForAuthCodeAsync(TcpListener listener, string state, CancellationToken ct)
        {
            while (true)
            {
                using var client = await listener.AcceptTcpClientAsync(ct);
                var stream = client.GetStream();
                var buf = new byte[8192];
                int total = 0;
                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                readCts.CancelAfter(5000);
                try
                {
                    while (total < buf.Length)
                    {
                        int n = await stream.ReadAsync(buf.AsMemory(total), readCts.Token);
                        if (n == 0) break;
                        total += n;
                        if (Encoding.ASCII.GetString(buf, 0, total).Contains("\r\n\r\n")) break;
                    }
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { continue; }

                var firstLine = Encoding.ASCII.GetString(buf, 0, total).Split('\n')[0].Trim(); // GET /?code=..&state=.. HTTP/1.1
                var parts = firstLine.Split(' ');
                if (parts.Length < 2 || !parts[1].StartsWith("/?"))
                {
                    await RespondAsync(stream, 404, "No encontrado.");
                    continue;
                }

                var query = System.Web.HttpUtility.ParseQueryString(parts[1].Substring(2));
                var error = query["error"];
                if (!string.IsNullOrEmpty(error))
                {
                    await RespondAsync(stream, 200, "Se canceló el inicio de sesión. Podés cerrar esta pestaña y volver a Velin.");
                    throw new MicrosoftAuthException(error == "access_denied"
                        ? "Cancelaste el inicio de sesión."
                        : "Microsoft devolvió un error: " + (query["error_description"] ?? error));
                }

                var code = query["code"];
                if (string.IsNullOrEmpty(code) || query["state"] != state)
                {
                    await RespondAsync(stream, 400, "Respuesta inválida.");
                    continue;
                }

                await RespondAsync(stream, 200, "¡Listo! Ya podés cerrar esta pestaña y volver a Velin.");
                return code;
            }
        }

        private static async Task RespondAsync(NetworkStream stream, int status, string message)
        {
            var html = "<!doctype html><html><head><meta charset=\"utf-8\"><title>Velin</title></head>"
                + "<body style=\"margin:0;height:100vh;display:flex;align-items:center;justify-content:center;"
                + "background:#2b1d12;color:#f0e0c0;font-family:Segoe UI,sans-serif\">"
                + "<div style=\"text-align:center\"><h1>Velin</h1><p style=\"font-size:18px\">" + message + "</p></div></body></html>";
            var body = Encoding.UTF8.GetBytes(html);
            var head = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {status} {(status == 200 ? "OK" : "Error")}\r\nContent-Type: text/html; charset=utf-8\r\n" +
                $"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            try
            {
                await stream.WriteAsync(head);
                await stream.WriteAsync(body);
                await stream.FlushAsync();
            }
            catch { /* si el navegador ya cerró la conexión, no importa */ }
        }

        private static string Base64Url(byte[] data) =>
            Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        // 1) Alternativa: pide el código que el usuario tiene que escribir en la web de Microsoft.
        public static async Task<DeviceCodeInfo> StartDeviceFlowAsync(string clientId, CancellationToken ct = default)
        {
            RequireClientId(clientId);
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId.Trim(),
                ["scope"] = Scope
            });
            using var resp = await Http.PostAsync($"https://login.microsoftonline.com/{Tenant}/oauth2/v2.0/devicecode", body, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = ParseJson(json);
            var root = doc.RootElement;

            if (!resp.IsSuccessStatusCode || !root.TryGetProperty("device_code", out _))
                throw new MicrosoftAuthException(DescribeOAuthError(root, "No se pudo iniciar el login con Microsoft."));

            return new DeviceCodeInfo
            {
                DeviceCode = root.GetProperty("device_code").GetString() ?? "",
                UserCode = root.GetProperty("user_code").GetString() ?? "",
                VerificationUri = root.TryGetProperty("verification_uri", out var vu) ? vu.GetString() ?? "https://www.microsoft.com/link" : "https://www.microsoft.com/link",
                ExpiresInSeconds = root.TryGetProperty("expires_in", out var ex) ? ex.GetInt32() : 900,
                IntervalSeconds = root.TryGetProperty("interval", out var iv) ? Math.Max(1, iv.GetInt32()) : 5
            };
        }

        // 2) Espera a que el usuario ingrese el código y completa toda la cadena de tokens.
        public static async Task<MicrosoftAuthResult> CompleteDeviceFlowAsync(string clientId, DeviceCodeInfo info, CancellationToken ct = default)
        {
            RequireClientId(clientId);
            var deadline = DateTime.UtcNow.AddSeconds(info.ExpiresInSeconds);
            int interval = info.IntervalSeconds;

            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(interval), ct);

                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["client_id"] = clientId.Trim(),
                    ["device_code"] = info.DeviceCode
                });
                using var resp = await Http.PostAsync($"https://login.microsoftonline.com/{Tenant}/oauth2/v2.0/token", body, ct);
                var json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = ParseJson(json);
                var root = doc.RootElement;

                if (resp.IsSuccessStatusCode && root.TryGetProperty("access_token", out var at))
                {
                    var refresh = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "";
                    return await FinishAsync(at.GetString() ?? "", refresh, ct);
                }

                var error = root.TryGetProperty("error", out var e) ? e.GetString() : null;
                switch (error)
                {
                    case "authorization_pending": continue;
                    case "slow_down": interval += 5; continue;
                    case "authorization_declined": throw new MicrosoftAuthException("Cancelaste el inicio de sesión en la página de Microsoft.");
                    case "expired_token":
                    case "bad_verification_code":
                        throw new MicrosoftAuthException("El código venció. Probá de nuevo.");
                    default:
                        throw new MicrosoftAuthException(DescribeOAuthError(root, "Microsoft rechazó el inicio de sesión."));
                }
            }
            throw new MicrosoftAuthException("El código venció. Probá de nuevo.");
        }

        // Renueva la sesión con el refresh token guardado (sin que el usuario haga nada).
        public static async Task<MicrosoftAuthResult> RefreshAsync(string clientId, string refreshToken, CancellationToken ct = default)
        {
            RequireClientId(clientId);
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientId.Trim(),
                ["refresh_token"] = refreshToken,
                ["scope"] = Scope
            });
            using var resp = await Http.PostAsync($"https://login.microsoftonline.com/{Tenant}/oauth2/v2.0/token", body, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = ParseJson(json);
            var root = doc.RootElement;

            if (!resp.IsSuccessStatusCode || !root.TryGetProperty("access_token", out var at))
                throw new MicrosoftAuthException("La sesión de Microsoft venció. Volvé a iniciar sesión desde Cuentas.");

            var newRefresh = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? refreshToken : refreshToken;
            return await FinishAsync(at.GetString() ?? "", newRefresh, ct);
        }

        // Token Microsoft -> Xbox Live -> XSTS -> Minecraft -> perfil.
        private static async Task<MicrosoftAuthResult> FinishAsync(string msAccessToken, string msRefreshToken, CancellationToken ct)
        {
            // Xbox Live
            var xblJson = await PostJsonAsync("https://user.auth.xboxlive.com/user/authenticate", new
            {
                Properties = new { AuthMethod = "RPS", SiteName = "user.auth.xboxlive.com", RpsTicket = "d=" + msAccessToken },
                RelyingParty = "http://auth.xboxlive.com",
                TokenType = "JWT"
            }, null, ct);
            string xblToken, userHash;
            if (!xblJson.ok) throw new MicrosoftAuthException("Xbox Live rechazó el inicio de sesión (" + xblJson.status + ").");
            using (var d = ParseJson(xblJson.body))
            {
                xblToken = d.RootElement.GetProperty("Token").GetString() ?? "";
                userHash = d.RootElement.GetProperty("DisplayClaims").GetProperty("xui")[0].GetProperty("uhs").GetString() ?? "";
            }

            // XSTS (autorización para los servicios de Minecraft)
            var xstsJson = await PostJsonAsync("https://xsts.auth.xboxlive.com/xsts/authorize", new
            {
                Properties = new { SandboxId = "RETAIL", UserTokens = new[] { xblToken } },
                RelyingParty = "rp://api.minecraftservices.com/",
                TokenType = "JWT"
            }, null, ct);
            string xstsToken;
            using (var d = ParseJson(xstsJson.body))
            {
                if (!xstsJson.ok)
                    throw new MicrosoftAuthException(DescribeXstsError(d.RootElement));
                xstsToken = d.RootElement.GetProperty("Token").GetString() ?? "";
            }

            // Token de Minecraft
            var mcJson = await PostJsonAsync("https://api.minecraftservices.com/authentication/login_with_xbox", new
            {
                identityToken = $"XBL3.0 x={userHash};{xstsToken}"
            }, null, ct);
            string mcToken; int expiresIn;
            using (var d = ParseJson(mcJson.body))
            {
                if (!mcJson.ok)
                {
                    if (mcJson.body.Contains("Invalid app registration", StringComparison.OrdinalIgnoreCase))
                        throw new MicrosoftAuthException(
                            "Mojang todavía no aprobó el Client ID de esta app (\"Invalid app registration\").\n" +
                            "Hay que pedir la aprobación en https://aka.ms/mce-reviewappid y esperar la respuesta.");
                    throw new MicrosoftAuthException("Minecraft Services rechazó el login (" + mcJson.status + ").");
                }
                mcToken = d.RootElement.GetProperty("access_token").GetString() ?? "";
                expiresIn = d.RootElement.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 86400;
            }

            // Perfil (nombre + UUID). 404 = la cuenta no tiene Minecraft Java.
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.minecraftservices.com/minecraft/profile");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mcToken);
            using var resp = await Http.SendAsync(req, ct);
            var profileBody = await resp.Content.ReadAsStringAsync(ct);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new MicrosoftAuthException("Esta cuenta de Microsoft no tiene Minecraft Java Edition (o todavía no creó su perfil).");
            if (!resp.IsSuccessStatusCode)
                throw new MicrosoftAuthException("No se pudo leer el perfil de Minecraft (" + (int)resp.StatusCode + ").");

            using var profile = ParseJson(profileBody);
            return new MicrosoftAuthResult
            {
                MsRefreshToken = msRefreshToken,
                McAccessToken = mcToken,
                McExpiresUtc = DateTime.UtcNow.AddSeconds(expiresIn),
                Username = profile.RootElement.GetProperty("name").GetString() ?? "",
                Uuid = profile.RootElement.GetProperty("id").GetString() ?? ""
            };
        }

        // --- Utilidades -------------------------------------------------------------------

        private static void RequireClientId(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new MicrosoftAuthException("El login de Microsoft todavía no está habilitado en esta versión de Velin.");
        }

        private static JsonDocument ParseJson(string text)
        {
            try { return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text); }
            catch (JsonException) { return JsonDocument.Parse("{}"); }
        }

        private static async Task<(bool ok, int status, string body)> PostJsonAsync(string url, object payload, string? bearer, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (bearer != null) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            using var resp = await Http.SendAsync(req, ct);
            return (resp.IsSuccessStatusCode, (int)resp.StatusCode, await resp.Content.ReadAsStringAsync(ct));
        }

        private static string DescribeOAuthError(JsonElement root, string fallback)
        {
            if (root.ValueKind == JsonValueKind.Object)
            {
                var code = root.TryGetProperty("error", out var e) ? e.GetString() : null;
                var desc = root.TryGetProperty("error_description", out var d) ? d.GetString() : null;
                if (code == "invalid_client" || (desc != null && desc.Contains("AADSTS700016")))
                    return "El Client ID no es válido o la app de Azure no está configurada como cliente público para cuentas personales.";
                if (desc != null && desc.Contains("AADSTS70002"))
                    return "En Azure hay que activar \"Permitir flujos de cliente público\" (Autenticación).";
                if (!string.IsNullOrEmpty(desc)) return fallback + "\n" + desc.Split('\n')[0];
            }
            return fallback;
        }

        private static string DescribeXstsError(JsonElement root)
        {
            long code = 0;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("XErr", out var x) && x.ValueKind == JsonValueKind.Number)
                code = x.GetInt64();
            return code switch
            {
                2148916233 => "Esta cuenta de Microsoft no tiene perfil de Xbox. Creá uno en xbox.com e intentá de nuevo.",
                2148916235 => "Xbox Live no está disponible en el país de esta cuenta.",
                2148916236 or 2148916237 => "Esta cuenta necesita una verificación de edad/identidad adicional en Xbox.",
                2148916238 => "Es una cuenta de menor de edad: un adulto tiene que agregarla a un grupo familiar de Microsoft.",
                _ => "Xbox Live rechazó la autorización" + (code != 0 ? $" (código {code})." : ".")
            };
        }
    }
}
