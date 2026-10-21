using System;
using System.IO;

namespace Velin.Services
{
    // Todas las rutas de datos cuelgan de la carpeta donde vive el .exe
    // (carpeta "data" al lado del ejecutable), tal como se pidió.
    public static class PathService
    {
        public static string BaseDir => AppContext.BaseDirectory;
        public static string DataDir => Path.Combine(BaseDir, "data");
        public static string InstancesDir => Path.Combine(DataDir, "instances");
        public static string JavaDir => Path.Combine(DataDir, "java");
        public static string LibrariesDir => Path.Combine(DataDir, "libraries");
        public static string AssetsDir => Path.Combine(DataDir, "assets");
        public static string CacheDir => Path.Combine(DataDir, "cache");
        public static string VaultDir => Path.Combine(DataDir, "vault");
        public static string UserThemesDir => Path.Combine(DataDir, "themes");
        // Backups de mundos: fuera de la carpeta de la instancia, así sobreviven si se la elimina.
        public static string BackupsDir => Path.Combine(DataDir, "backups");
        public static string BuiltInThemesDir => Path.Combine(BaseDir, "Assets", "Themes");

        public static void EnsureAll()
        {
            foreach (var d in new[] { DataDir, InstancesDir, JavaDir, LibrariesDir, AssetsDir, CacheDir, VaultDir, UserThemesDir, BackupsDir })
                Directory.CreateDirectory(d);
        }
    }
}
