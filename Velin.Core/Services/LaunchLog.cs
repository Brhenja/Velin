using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Velin.Services
{
    // Log de depuración de un lanzamiento: el comando completo (con el accessToken oculto)
    // y toda la salida del proceso de Minecraft. Se sobrescribe en cada lanzamiento.
    internal sealed class LaunchLog : IDisposable
    {
        private readonly StreamWriter _writer;
        private readonly object _lock = new();

        public string Path { get; }

        public LaunchLog(string path)
        {
            Path = path;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            _writer = new StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = true };
        }

        public void Line(string text)
        {
            lock (_lock)
            {
                try { _writer.WriteLine(text); } catch { /* el log nunca debe romper el lanzamiento */ }
            }
        }

        // Últimas líneas del log (se puede leer aunque el juego todavía lo esté escribiendo).
        public static string ReadTail(string path, int lines)
        {
            try
            {
                if (!File.Exists(path)) return "";
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs, Encoding.UTF8);
                var all = new List<string>();
                string? line;
                while ((line = reader.ReadLine()) != null) all.Add(line);
                return string.Join(Environment.NewLine, all.Skip(Math.Max(0, all.Count - lines)));
            }
            catch { return ""; }
        }

        // Arma el comando como texto, reemplazando el valor que sigue a --accessToken por ***.
        public static string FormatCommand(string exe, IEnumerable<string> args)
        {
            var parts = new List<string> { Quote(exe) };
            string? previous = null;
            foreach (var a in args)
            {
                var shown = string.Equals(previous, "--accessToken", StringComparison.OrdinalIgnoreCase) ? "***" : a;
                parts.Add(Quote(shown));
                previous = a;
            }
            return string.Join(" ", parts);
        }

        private static string Quote(string s) =>
            s.Length == 0 || s.Any(char.IsWhiteSpace) ? "\"" + s + "\"" : s;

        public void Dispose()
        {
            lock (_lock)
            {
                try { _writer.Dispose(); } catch { }
            }
        }
    }
}
