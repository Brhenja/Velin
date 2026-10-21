namespace Velin.Services
{
    // Datos de la aplicación en un solo lugar (versión, autor, repositorio).
    public static class AppInfo
    {
        public const string Name = "Velin";
        public const string Author = "Brhenja";
        public const string RepoUrl = "https://github.com/Brhenja/Velin";

        // Se toma de <Version> del .csproj.
        public static string Version
        {
            get
            {
                var v = typeof(AppInfo).Assembly.GetName().Version;
                return v == null ? "1.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
            }
        }

        // Identificación que se manda a Modrinth, CurseForge, Microsoft, etc.
        public static string UserAgent => $"Velin/{Version} ({RepoUrl})";
    }
}
