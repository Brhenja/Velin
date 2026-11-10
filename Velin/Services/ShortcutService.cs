#pragma warning disable CA1416 // WScript.Shell es sólo de Windows (Velin es sólo para Windows)
using System;
using System.IO;
using System.Linq;

namespace Velin.Services
{
    // Accesos directos de Windows (.lnk) que abren Velin y juegan una instancia: Velin.exe --jugar "Nombre".
    public static class ShortcutService
    {
        public const string PlayArgument = "--jugar";

        // Argumentos de línea de comandos: --jugar "Nombre de la instancia" (también --play). null si no hay.
        public static string? ParsePlayArgument(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i].Equals(PlayArgument, StringComparison.OrdinalIgnoreCase) || args[i].Equals("--play", StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrWhiteSpace(args[i + 1]) ? null : args[i + 1].Trim();
            return null;
        }

        public static string BuildArguments(string instanceName) => $"{PlayArgument} \"{instanceName.Replace("\"", "")}\"";

        // Nombre de archivo seguro para el acceso directo.
        public static string SafeFileName(string name)
        {
            var bad = Path.GetInvalidFileNameChars();
            var s = new string(name.Where(c => !bad.Contains(c)).ToArray()).Trim();
            return s.Length == 0 ? "Velin" : s;
        }

        // Crea "<carpeta>\<instancia>.lnk". Usa el objeto COM WScript.Shell que trae Windows (sin dependencias).
        public static string Create(string instanceName, string folder, string exePath, string? iconPath = null)
        {
            Directory.CreateDirectory(folder);
            var lnkPath = Path.Combine(folder, SafeFileName(instanceName) + ".lnk");

            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new PlatformNotSupportedException("Este sistema no permite crear accesos directos de Windows.");
            dynamic shell = Activator.CreateInstance(shellType)!;
            try
            {
                dynamic link = shell.CreateShortcut(lnkPath);
                link.TargetPath = exePath;
                link.Arguments = BuildArguments(instanceName);
                link.WorkingDirectory = Path.GetDirectoryName(exePath) ?? "";
                link.IconLocation = (iconPath ?? exePath) + ",0";
                link.Description = "Velin: " + instanceName;
                link.Save();
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
            }
            return lnkPath;
        }
    }
}
