using System;
using System.Text.Json.Serialization;

namespace Velin.Models
{
    // Cuenta guardada en data\accounts.json. Las cuentas offline sólo tienen nombre y UUID.
    // En las de Microsoft, los tokens se guardan cifrados con DPAPI (sólo este usuario de Windows
    // puede descifrarlos) — nunca en texto plano.
    public class Account
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Type { get; set; } = "offline";      // "offline" o "microsoft"
        public string Username { get; set; } = "";
        public string Uuid { get; set; } = "";

        public string RefreshTokenProtected { get; set; } = "";
        public string McTokenProtected { get; set; } = "";
        public DateTime McTokenExpiresUtc { get; set; } = DateTime.MinValue;

        [JsonIgnore]
        public bool IsMicrosoft => string.Equals(Type, "microsoft", StringComparison.OrdinalIgnoreCase);

        public override string ToString() => Username + (IsMicrosoft ? " (Microsoft)" : " (offline)");
    }

    // Lo que necesita el juego para arrancar con una cuenta.
    public class LaunchSession
    {
        public string Username { get; set; } = "";
        public string Uuid { get; set; } = "";
        public string AccessToken { get; set; } = "0";
        public string UserType { get; set; } = "legacy";   // "msa" para cuentas Microsoft
    }
}
