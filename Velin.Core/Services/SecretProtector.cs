using System;
using System.Security.Cryptography;
using System.Text;

namespace Velin.Services
{
    // Cómo se cifran en disco los tokens de las cuentas. En Windows se usa DPAPI (sólo tu usuario de Windows puede leerlos).
    // Otro sistema operativo tiene que poner acá su propia implementación (por ejemplo el llavero del sistema) antes de usar cuentas.
    public interface ISecretProtector
    {
        // Texto -> texto cifrado en base64 ("" si no hay nada que cifrar).
        string Protect(string plain);
        // Lo inverso. "" si el texto está vacío o no se puede descifrar (por ejemplo, lo cifró otro usuario).
        string Unprotect(string protectedB64);
    }

    public static class SecretProtector
    {
        public static ISecretProtector Current { get; set; } = new DpapiSecretProtector();
    }

    internal sealed class DpapiSecretProtector : ISecretProtector
    {
        public string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("DPAPI sólo existe en Windows: hace falta otro SecretProtector.");
            var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(data);
        }

        public string Unprotect(string protectedB64)
        {
            if (string.IsNullOrEmpty(protectedB64) || !OperatingSystem.IsWindows()) return "";
            try
            {
                var data = ProtectedData.Unprotect(Convert.FromBase64String(protectedB64), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch { return ""; }
        }
    }
}
