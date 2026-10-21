namespace Velin.Models
{
    public class AppSettings
    {
        public string Language { get; set; } = "es";
        public string DefaultUsername { get; set; } = "Player";
        public int DefaultRamMB { get; set; } = 2048;
        public string ThemeName { get; set; } = "Velin (medieval)";
        public bool MusicEnabled { get; set; } = true; // activada por defecto; se puede apagar en Configuración
        // Marca que ya se aplicó el cambio "música apagada por defecto" a una configuración vieja.
        public bool MusicOffDefaultApplied { get; set; } = false;
        // Marca que ya se pasó UNA vez la música a "activada por defecto" en una configuración vieja
        // (una versión de desarrollo anterior la había apagado). Después de eso se respeta lo que elijas con el interruptor.
        public bool MusicOnDefaultApplied { get; set; } = false;
        // Proxy (opcional) para las descargas y para el juego. Vacío = sin proxy. Se aplica al reiniciar Velin.
        public string ProxyHost { get; set; } = "";
        public int ProxyPort { get; set; } = 0;
        public bool ClickSoundEnabled { get; set; } = true;
        // Copia de seguridad automática de los mundos (antes de actualizar mods y antes de
        // eliminar una instancia). Desactivada por defecto: se activa en Configuración.
        public bool BackupWorldsEnabled { get; set; } = false;
        // Pantalla de inicio (splash). Activada por defecto.
        public bool ShowSplash { get; set; } = true;
        // Orden de la lista de instancias: "name", "lastPlayed" o "playTime".
        public string InstanceSort { get; set; } = "name";
        // Cuentas: Client ID de la app de Azure (login Microsoft) y cuenta usada por defecto.
        // API key de CurseForge del usuario (se carga en Configuración; cada usuario usa la suya).
        public string CurseForgeApiKey { get; set; } = "";
        public string MicrosoftClientId { get; set; } = "";
        public string DefaultAccountId { get; set; } = "";
        // Si está activo, el launcher no se oculta mientras corre Minecraft y se abre la consola en vivo.
        public bool KeepLauncherOpen { get; set; } = false;
        // Filtro de grupo en la lista de instancias ("" = todas).
        public string GroupFilter { get; set; } = "";
    }
}
