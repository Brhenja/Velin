using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Velin.Services
{
    public class JavaInstall
    {
        public string ExePath { get; set; } = "";     // ...\\bin\\javaw.exe (o java.exe)
        public string Version { get; set; } = "";     // "21.0.4" / "1.8.0_402"
        public int Major { get; set; }
        public override string ToString() => $"Java {Version}  —  {ExePath}";
    }

    // Busca instalaciones de Java en el equipo (para elegir una en "Opciones de lanzamiento").
    public static class JavaScanner
    {
        public static List<JavaInstall> Scan()
        {
            var roots = new List<string>();
            foreach (var pf in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
            {
                if (string.IsNullOrEmpty(pf)) continue;
                foreach (var vendor in new[] { "Java", "Eclipse Adoptium", "Eclipse Foundation", "Microsoft", "Zulu", "Amazon Corretto", "BellSoft", "Semeru", "AdoptOpenJDK" })
                    roots.Add(Path.Combine(pf, vendor));
            }
            roots.Add(PathService.JavaDir); // los Java que descarga Velin
            var home = Environment.GetEnvironmentVariable("JAVA_HOME");
            var list = Scan(roots);
            if (!string.IsNullOrWhiteSpace(home))
            {
                var fromHome = FromHome(home);
                if (fromHome != null && !list.Any(j => string.Equals(j.ExePath, fromHome.ExePath, StringComparison.OrdinalIgnoreCase))) list.Add(fromHome);
            }
            return list.OrderByDescending(j => j.Major).ThenBy(j => j.ExePath, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // Busca hasta 3 niveles de carpetas dentro de cada raíz: raíz\\jdk-21\\bin\\javaw.exe, raíz\\21\\jdk-21\\bin\\javaw.exe...
        public static List<JavaInstall> Scan(IEnumerable<string> roots)
        {
            var found = new Dictionary<string, JavaInstall>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in roots)
            {
                try
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (var dir in EnumerateDirs(root, 3))
                    {
                        var install = FromHome(dir);
                        if (install != null) found[install.ExePath] = install;
                    }
                }
                catch { /* carpeta sin permisos: se ignora */ }
            }
            return found.Values.ToList();
        }

        private static IEnumerable<string> EnumerateDirs(string root, int depth)
        {
            yield return root;
            if (depth == 0) yield break;
            IEnumerable<string> children;
            try { children = Directory.GetDirectories(root); } catch { yield break; }
            foreach (var child in children)
                foreach (var d in EnumerateDirs(child, depth - 1))
                    yield return d;
        }

        // Una carpeta "home" de Java tiene bin\\javaw.exe (o java.exe) y normalmente un archivo "release".
        public static JavaInstall? FromHome(string home)
        {
            var bin = Path.Combine(home, "bin");
            var exe = Path.Combine(bin, "javaw.exe");
            if (!File.Exists(exe)) exe = Path.Combine(bin, "java.exe");
            if (!File.Exists(exe)) exe = Path.Combine(bin, "javaw");
            if (!File.Exists(exe)) exe = Path.Combine(bin, "java");
            if (!File.Exists(exe)) return null;

            var version = "";
            try
            {
                var release = Path.Combine(home, "release");
                if (File.Exists(release))
                {
                    var m = Regex.Match(File.ReadAllText(release), "JAVA_VERSION=\"([^\"]+)\"");
                    if (m.Success) version = m.Groups[1].Value;
                }
            }
            catch { }
            if (version.Length == 0)
            {
                var m = Regex.Match(Path.GetFileName(home.TrimEnd('\\', '/')), @"\d+(\.\d+)*(_\d+)?");
                version = m.Success ? m.Value : "?";
            }
            return new JavaInstall { ExePath = exe, Version = version, Major = ParseMajor(version) };
        }

        // "1.8.0_402" -> 8, "21.0.4" -> 21, "17" -> 17
        public static int ParseMajor(string version)
        {
            var m = Regex.Match(version, @"^(\d+)(?:\.(\d+))?");
            if (!m.Success) return 0;
            int first = int.Parse(m.Groups[1].Value);
            if (first == 1 && m.Groups[2].Success) return int.Parse(m.Groups[2].Value);
            return first;
        }
    }
}
