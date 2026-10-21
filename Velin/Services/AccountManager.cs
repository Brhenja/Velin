using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Velin.Models;

#pragma warning disable CA1416 // DPAPI es sólo de Windows (Velin es sólo para Windows)
namespace Velin.Services
{
    // Guarda las cuentas en data\accounts.json y arma la sesión para lanzar el juego.
    public static class AccountManager
    {
        private static string FilePath => Path.Combine(PathService.DataDir, "accounts.json");
        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        public static List<Account> LoadAll()
        {
            PathService.EnsureAll();
            try
            {
                if (!File.Exists(FilePath)) return new List<Account>();
                return JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(FilePath)) ?? new List<Account>();
            }
            catch { return new List<Account>(); }
        }

        public static void SaveAll(List<Account> accounts)
        {
            PathService.EnsureAll();
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(accounts, JsonOpts));
            File.Move(tmp, FilePath, true);
        }

        public static Account AddOffline(string username)
        {
            username = username.Trim();
            var acc = new Account { Type = "offline", Username = username, Uuid = GameLauncher.OfflineUuid(username) };
            var all = LoadAll(); all.Add(acc); SaveAll(all);
            return acc;
        }

        // Agrega (o actualiza, si ya existía esa cuenta de Microsoft) y guarda los tokens cifrados.
        public static Account AddOrUpdateMicrosoft(MicrosoftAuthResult r)
        {
            var all = LoadAll();
            var acc = all.FirstOrDefault(a => a.IsMicrosoft && string.Equals(a.Uuid, r.Uuid, StringComparison.OrdinalIgnoreCase));
            if (acc == null) { acc = new Account { Type = "microsoft" }; all.Add(acc); }
            ApplyResult(acc, r);
            SaveAll(all);
            return acc;
        }

        public static void Remove(string id)
        {
            var all = LoadAll();
            all.RemoveAll(a => a.Id == id);
            SaveAll(all);
        }

        public static Account? Get(string id) =>
            string.IsNullOrEmpty(id) ? null : LoadAll().FirstOrDefault(a => a.Id == id);

        // Cuenta que usa una instancia: la suya, si no la predeterminada, si no ninguna (offline con el Username de la instancia).
        public static Account? Resolve(Instance instance, AppSettings settings)
        {
            return Get(instance.AccountId) ?? Get(settings.DefaultAccountId);
        }

        // Devuelve los datos de sesión. Para cuentas Microsoft renueva el token de Minecraft si venció.
        public static async Task<LaunchSession> GetSessionAsync(Account? account, Instance instance, string clientId, CancellationToken ct = default)
        {
            if (account == null)
                return new LaunchSession { Username = instance.Username, Uuid = GameLauncher.OfflineUuid(instance.Username) };

            if (!account.IsMicrosoft)
                return new LaunchSession { Username = account.Username, Uuid = account.Uuid };

            if (account.McExpiresSoon() || string.IsNullOrEmpty(account.McTokenProtected))
            {
                var refresh = Unprotect(account.RefreshTokenProtected);
                if (string.IsNullOrEmpty(refresh))
                    throw new MicrosoftAuthException("Falta la sesión guardada de " + account.Username + ". Volvé a iniciar sesión desde Cuentas.");

                var result = await MicrosoftAuth.RefreshAsync(clientId, refresh, ct);
                var all = LoadAll();
                var stored = all.FirstOrDefault(a => a.Id == account.Id);
                if (stored != null) { ApplyResult(stored, result); SaveAll(all); account = stored; }
                else ApplyResult(account, result);
            }

            return new LaunchSession
            {
                Username = account.Username,
                Uuid = account.Uuid,
                AccessToken = Unprotect(account.McTokenProtected),
                UserType = "msa"
            };
        }

        private static bool McExpiresSoon(this Account a) => a.McTokenExpiresUtc <= DateTime.UtcNow.AddMinutes(3);

        private static void ApplyResult(Account acc, MicrosoftAuthResult r)
        {
            acc.Username = r.Username;
            acc.Uuid = r.Uuid;
            acc.RefreshTokenProtected = Protect(r.MsRefreshToken);
            acc.McTokenProtected = Protect(r.McAccessToken);
            acc.McTokenExpiresUtc = r.McExpiresUtc;
        }

        // DPAPI (CurrentUser): los tokens sólo se pueden descifrar con tu usuario de Windows.
        private static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(data);
        }

        private static string Unprotect(string protectedB64)
        {
            if (string.IsNullOrEmpty(protectedB64)) return "";
            try
            {
                var data = ProtectedData.Unprotect(Convert.FromBase64String(protectedB64), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch { return ""; }
        }
    }
}
