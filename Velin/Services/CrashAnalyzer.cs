using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Velin.Services
{
    // Una causa probable de un crash. Id sirve para buscar el texto traducido ("crash_cause_" + Id) y Detail es un complemento
    // ya formateado y sin idioma (por ejemplo " (Java 21+)" o " (Fabric API (Sodium))"), o vacío.
    public sealed record CrashFinding(string Id, string Detail);

    // Mira el log del lanzamiento y el crash report (si hay) y reconoce los problemas más comunes de Minecraft con mods.
    // No adivina: si no reconoce nada, no devuelve nada.
    public static class CrashAnalyzer
    {
        private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        private const int MaxFindings = 4;

        public static List<CrashFinding> Analyze(string? log, string? crashReport)
        {
            // La primera línea del log es el comando completo, que puede traer flags como -XX:+HeapDumpOnOutOfMemoryError: no son un error.
            var text = Regex.Replace((log ?? "") + "\n" + (crashReport ?? ""), @"\w*OnOutOfMemoryError", "", Opts);
            var found = new List<CrashFinding>();
            void Add(string id, string detail = "")
            {
                if (found.Count < MaxFindings && !found.Any(f => f.Id == id)) found.Add(new CrashFinding(id, detail));
            }
            if (string.IsNullOrWhiteSpace(text)) return found;

            // Java ni siquiera pudo arrancar
            var unrecognized = Regex.Match(text, @"Unrecognized (?:VM )?option:?\s*'?([^'\r\n]+)'?", Opts);
            bool heapReserve = Regex.IsMatch(text, @"Could not reserve enough space for|Invalid (?:maximum|initial) heap size|The specified size exceeds the maximum representable size", Opts);
            if (heapReserve) Add("heap");
            if (unrecognized.Success) Add("jvm_args", Paren(unrecognized.Groups[1].Value.Trim()));
            else if (!heapReserve && text.Contains("Could not create the Java Virtual Machine", StringComparison.OrdinalIgnoreCase)) Add("jvm_args");

            // Versión de Java demasiado vieja para el juego o los mods
            var classVersion = Regex.Match(text, @"UnsupportedClassVersionError[^\r\n]*?class file version (\d+)\.\d+|class file version (\d+)\.\d+[^\r\n]*?(?:only recognizes|up to)", Opts);
            if (classVersion.Success)
            {
                var raw = classVersion.Groups[1].Success ? classVersion.Groups[1].Value : classVersion.Groups[2].Value;
                var java = JavaForClassVersion(int.Parse(raw));
                Add("java_version", java > 0 ? Paren($"Java {java}+") : "");
            }

            if (text.Contains("OutOfMemoryError", StringComparison.OrdinalIgnoreCase)) Add("oom");

            // Mods: dependencias que faltan (Fabric y Forge)
            var deps = new List<string>();
            foreach (Match m in Regex.Matches(text, @"Mod '([^']+)' \(([^)]+)\)[^\r\n]*?requires[^\r\n]*?mod '([^']+)' \(([^)]+)\)[^\r\n]*?(?:which is missing|but only the wrong version)", Opts))
                deps.Add($"{m.Groups[3].Value} ← {m.Groups[1].Value}");
            foreach (Match m in Regex.Matches(text, @"Mod ID: '([^']+)', Requested by: '([^']+)'", Opts))
                deps.Add($"{m.Groups[1].Value} ← {m.Groups[2].Value}");
            if (deps.Count > 0) Add("missing_dep", Paren(string.Join(", ", deps.Distinct().Take(4))));

            if (Regex.IsMatch(text, @"duplicate mod", Opts)) Add("duplicate");

            if (Regex.IsMatch(text, @"GLFW error 6554[23]|GLFW_API_UNAVAILABLE|WGL: The driver does not appear to support OpenGL|Pixel format not accepted|Failed to create the GLFW window|Couldn't set pixel format|Failed to find a suitable pixel format", Opts))
                Add("gpu");

            if (Regex.IsMatch(text, @"UnsatisfiedLinkError|Failed to locate library|Failed to load library|Could not load lwjgl", Opts)) Add("native");

            if (Regex.IsMatch(text, @"No space left on device|not enough space on the disk|No hay suficiente espacio en (?:el )?disco", Opts)) Add("disk");

            // Un mod que no encaja con esta versión, con el loader o con otro mod
            if (Regex.IsMatch(text, @"Mixin apply failed|MixinApplyError|MixinTransformerError|InvalidMixinException|NoClassDefFoundError|ClassNotFoundException|NoSuchMethodError|NoSuchFieldError|AbstractMethodError|IncompatibleClassChangeError", Opts))
                Add("mod", SuspectDetail(text));

            return found;
        }

        // Versión de Java que corresponde a una versión de archivo de clase (52 = Java 8, 61 = Java 17, 65 = Java 21...). 0 si no se sabe.
        public static int JavaForClassVersion(int classMajor) => classMajor >= 52 && classMajor <= 80 ? classMajor - 44 : 0;

        // Qué mod (o clase) parece estar involucrado, si el log lo dice.
        private static string SuspectDetail(string text)
        {
            var mod = Regex.Match(text, @"Mixin apply for mod ([\w\-.]+) failed", Opts);
            if (mod.Success) return Paren(mod.Groups[1].Value);
            mod = Regex.Match(text, @"from mod ([\w\-.]+)", Opts);
            if (mod.Success) return Paren(mod.Groups[1].Value);
            var cls = Regex.Match(text, @"NoClassDefFoundError: ([\w/.$]+)", Opts);
            if (cls.Success) return Paren(cls.Groups[1].Value.Replace('/', '.'));
            return "";
        }

        private static string Paren(string value) => value.Length == 0 ? "" : $" ({value})";

        // Lee el final de un archivo de texto (como mucho maxBytes), aunque otro proceso lo esté escribiendo. "" si no existe o falla.
        public static string ReadTailText(string? path, int maxBytes)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "";
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length > maxBytes) fs.Seek(-maxBytes, SeekOrigin.End);
                using var reader = new StreamReader(fs, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            catch { return ""; }
        }
    }
}
