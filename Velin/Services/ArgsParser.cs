using System.Collections.Generic;
using System.Text;

namespace Velin.Services
{
    public static class ArgsParser
    {
        // Separa una línea de argumentos respetando comillas dobles ("a b" cuenta como uno solo).
        public static List<string> Split(string? line)
        {
            var args = new List<string>();
            if (string.IsNullOrWhiteSpace(line)) return args;

            var current = new StringBuilder();
            bool inQuotes = false, hasToken = false;
            foreach (var ch in line)
            {
                if (ch == '"') { inQuotes = !inQuotes; hasToken = true; continue; }
                if (char.IsWhiteSpace(ch) && !inQuotes)
                {
                    if (hasToken) { args.Add(current.ToString()); current.Clear(); hasToken = false; }
                    continue;
                }
                current.Append(ch);
                hasToken = true;
            }
            if (hasToken) args.Add(current.ToString());
            return args;
        }
    }
}
