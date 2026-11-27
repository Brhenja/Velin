namespace Velin.Models
{
    public class AppSettings
    {
        public string Language { get; set; } = "es";
        public string DefaultUsername { get; set; } = "Player";
        public int DefaultRamMB { get; set; } = 2048;
        public string ThemeName { get; set; } = "Velin (medieval)";
        // Fuente de texto de la interfaz. Vacío = la predeterminada de Velin (la medieval, o la normal de Windows según el idioma).
        public string UiFont { get; set; } = "";
        // Estilo de la ventana principal: "new" (íconos, botones +/−/engranaje sobre la lista) o "legacy" (disposición clásica). Se aplica al reiniciar.
        public string UiStyle { get; set; } = "new";
        // Abrir las carpetas (instancia, mods, Vault) con el Explorador de Windows en vez del gestor de archivos propio de Velin.
        public bool UseWindowsFileManager { get; set; } = false;
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
        // Orden de la lista de mods instalados: az, recent, oldest, enabled, disabled.
        public string ModsSort { get; set; } = "az";
        // Cuentas: Client ID de la app de Azure (login Microsoft) y cuenta usada por defecto.
        // API key de CurseForge del usuario (se carga en Configuración; cada usuario usa la suya).
        public string CurseForgeApiKey { get; set; } = "";
        public string MicrosoftClientId { get; set; } = "";
        public string DefaultAccountId { get; set; } = "";
        // Si está activo, el launcher no se oculta mientras corre Minecraft y se abre la consola en vivo.
        public bool KeepLauncherOpen { get; set; } = false;
        // Filtro de grupo en la lista de instancias ("" = todas).
        public string GroupFilter { get; set; } = "";

        // --- Novedades 1.1.0 ---
        // Java opcionales (GraalVM 21 / IBM Semeru OpenJ9): se descargan y se activan en Configuración;
        // recién activados aparecen para elegir en las opciones de cada instancia.
        public bool GraalVmEnabled { get; set; } = false;
        public bool SemeruEnabled { get; set; } = false;
        // Flags de JVM optimizados (G1 / OpenJ9 según el Java). Si se apaga, se usan solo -Xms/-Xmx.
        public bool UseOptimizedFlags { get; set; } = true;
        // Argumentos de JVM extra que se agregan a TODAS las instancias.
        public string GlobalJvmArgs { get; set; } = "";
        // Fuente que aparece primero en los buscadores de contenido: "modrinth" o "curseforge".
        public string DefaultContentSource { get; set; } = "modrinth";
        // --- Novedades 1.2.0 ---
        // Aviso de versión nueva (GitHub Releases) al abrir Velin, como máximo cada 12 horas.
        public bool CheckUpdatesOnStart { get; set; } = true;
        public DateTime LastUpdateCheckUtc { get; set; } = DateTime.MinValue;
        public string SkippedUpdateVersion { get; set; } = "";
        // Pedir confirmación antes de eliminar una instancia.
        public bool ConfirmDelete { get; set; } = true;
    }
}
