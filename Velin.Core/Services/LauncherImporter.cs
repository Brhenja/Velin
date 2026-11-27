using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Velin.Models;

namespace Velin.Services
{
    public enum LauncherKind { Prism, CurseForge, Minecraft }

    // Una instancia encontrada en otro launcher, lista para importar.
    public class ImportCandidate
    {
        public LauncherKind Launcher { get; set; }
        public string Name { get; set; } = "";
        public string McVersion { get; set; } = "";
        public ModLoaderType Loader { get; set; } = ModLoaderType.Vanilla;
        public string? LoaderVersion { get; set; }
        public int RamMB { get; set; }                 // 0 = usar el valor por defecto de Velin
        public string SourceDir { get; set; } = "";    // carpeta del juego de la que se copian los archivos
        public override string ToString() => Name;
    }

    public class ImportOptions
    {
        public bool Worlds { get; set; } = true;       // saves\
        public bool Mods { get; set; } = true;         // mods\, config\ y similares
        public bool Other { get; set; } = true;        // resource packs, shaders, opciones, servidores, capturas...
    }

    // Importa instancias de Prism Launcher / MultiMC, de la app de CurseForge y del launcher oficial de Minecraft.
    // Se copian los archivos (el original no se toca); Velin baja después Java, Minecraft y el loader al jugar por primera vez.
    public static class LauncherImporter
    {
        // --- Carpetas habituales ----------------------------------------------------------------

        public static string PrismInstancesDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PrismLauncher", "instances");
        public static string PolyMcInstancesDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PolyMC", "instances");
        public static string CurseForgeInstancesDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "curseforge", "minecraft", "Instances");
        public static string OfficialDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");

        // Busca en las carpetas habituales y en las extra que el usuario haya elegido.
        public static List<ImportCandidate> DetectAll(IEnumerable<string>? extraFolders = null)
        {
            var result = new List<ImportCandidate>();
            var folders = new List<string> { PrismInstancesDir, PolyMcInstancesDir, CurseForgeInstancesDir, OfficialDir };
            if (extraFolders != null) folders.AddRange(extraFolders);
            foreach (var f in folders.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
                result.AddRange(ScanFolder(f));
            return result;
        }

        // Una carpeta puede ser: la de instancias de Prism/MultiMC, la de CurseForge, la de una sola instancia, o una .minecraft oficial.
        public static List<ImportCandidate> ScanFolder(string folder)
        {
            var found = new List<ImportCandidate>();
            try
            {
                if (File.Exists(Path.Combine(folder, "launcher_profiles.json")))
                {
                    found.AddRange(ParseOfficialProfiles(File.ReadAllText(Path.Combine(folder, "launcher_profiles.json")), folder));
                    return found;
                }
                var dirs = new List<string> { folder };
                dirs.AddRange(Directory.EnumerateDirectories(folder));
                foreach (var dir in dirs)
                {
                    var c = ParsePrismInstance(dir) ?? ParseCurseForgeInstance(dir);
                    if (c != null) found.Add(c);
                }
            }
            catch { /* carpeta ilegible: se ignora */ }
            return found;
        }

        // --- Prism Launcher / MultiMC / PolyMC --------------------------------------------------

        public static ImportCandidate? ParsePrismInstance(string dir)
        {
            var cfg = Path.Combine(dir, "instance.cfg");
            var pack = Path.Combine(dir, "mmc-pack.json");
            if (!File.Exists(cfg) || !File.Exists(pack)) return null;
            try
            {
                var c = ParsePrism(File.ReadAllText(cfg), File.ReadAllText(pack), Path.GetFileName(dir.TrimEnd('\\', '/')));
                if (c == null) return null;
                var game = Directory.Exists(Path.Combine(dir, "minecraft")) ? Path.Combine(dir, "minecraft") : Path.Combine(dir, ".minecraft");
                if (!Directory.Exists(game)) return null;
                c.SourceDir = game;
                return c;
            }
            catch { return null; }
        }

        // instance.cfg (INI simple: clave=valor) + mmc-pack.json (components con uid y version).
        public static ImportCandidate? ParsePrism(string instanceCfg, string mmcPackJson, string fallbackName)
        {
            string name = fallbackName;
            int ram = 0;
            foreach (var line in instanceCfg.Split('\n'))
            {
                var i = line.IndexOf('=');
                if (i <= 0) continue;
                var key = line.Substring(0, i).Trim();
                var val = line.Substring(i + 1).Trim();
                if (key == "name" && val.Length > 0) name = val;
                else if (key == "MaxMemAlloc" && int.TryParse(val, out var mb) && mb >= 512) ram = mb;
            }

            using var doc = JsonDocument.Parse(mmcPackJson);
            if (!doc.RootElement.TryGetProperty("components", out var comps) || comps.ValueKind != JsonValueKind.Array) return null;

            string mc = "", loaderVersion = "";
            var loader = ModLoaderType.Vanilla;
            foreach (var comp in comps.EnumerateArray())
            {
                var uid = Str(comp, "uid");
                var version = Str(comp, "version");
                switch (uid)
                {
                    case "net.minecraft": mc = version; break;
                    case "net.fabricmc.fabric-loader": loader = ModLoaderType.Fabric; loaderVersion = version; break;
                    case "org.quiltmc.quilt-loader": loader = ModLoaderType.Quilt; loaderVersion = version; break;
                    case "net.minecraftforge": loader = ModLoaderType.Forge; loaderVersion = version; break;
                    case "net.neoforged": loader = ModLoaderType.NeoForge; loaderVersion = version; break;
                }
            }
            if (mc.Length == 0) return null;
            return new ImportCandidate
            {
                Launcher = LauncherKind.Prism, Name = name, McVersion = mc, Loader = loader,
                LoaderVersion = loader == ModLoaderType.Vanilla || loaderVersion.Length == 0 ? null : StripMcPrefix(loaderVersion, mc),
                RamMB = ram
            };
        }

        // --- CurseForge (app de escritorio) -----------------------------------------------------

        public static ImportCandidate? ParseCurseForgeInstance(string dir)
        {
            var file = Path.Combine(dir, "minecraftinstance.json");
            if (!File.Exists(file)) return null;
            try
            {
                var c = ParseCurseForge(File.ReadAllText(file), Path.GetFileName(dir.TrimEnd('\\', '/')));
                if (c == null) return null;
                c.SourceDir = dir;   // en CurseForge el juego vive directamente en la carpeta de la instancia
                return c;
            }
            catch { return null; }
        }

        // minecraftinstance.json: name, gameVersion y baseModLoader.name ("forge-47.2.0", "fabric-0.14.21-1.19.2", "neoforge-20.4.200"...).
        public static ImportCandidate? ParseCurseForge(string json, string fallbackName)
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            var mc = Str(r, "gameVersion");
            if (mc.Length == 0) return null;
            var name = Str(r, "name");
            var c = new ImportCandidate { Launcher = LauncherKind.CurseForge, Name = name.Length > 0 ? name : fallbackName, McVersion = mc };

            if (r.TryGetProperty("baseModLoader", out var bml) && bml.ValueKind == JsonValueKind.Object)
            {
                var m = Regex.Match(Str(bml, "name"), @"^(forge|fabric|quilt|neoforge)-(.+)$", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    c.Loader = m.Groups[1].Value.ToLowerInvariant() switch
                    {
                        "forge" => ModLoaderType.Forge,
                        "fabric" => ModLoaderType.Fabric,
                        "quilt" => ModLoaderType.Quilt,
                        _ => ModLoaderType.NeoForge
                    };
                    var v = m.Groups[2].Value;
                    if (v.EndsWith("-" + mc, StringComparison.Ordinal)) v = v.Substring(0, v.Length - mc.Length - 1);   // "0.14.21-1.19.2" -> "0.14.21"
                    c.LoaderVersion = StripMcPrefix(v, mc);
                }
            }
            return c;
        }

        // --- Launcher oficial de Minecraft ------------------------------------------------------

        // launcher_profiles.json: perfiles con lastVersionId ("1.20.1", "fabric-loader-0.15.7-1.20.1", "1.20.1-forge-47.2.0"...).
        // Los perfiles "latest-release/latest-snapshot" no tienen versión fija y se omiten.
        public static List<ImportCandidate> ParseOfficialProfiles(string json, string mcDir)
        {
            var list = new List<ImportCandidate>();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("profiles", out var profiles) || profiles.ValueKind != JsonValueKind.Object) return list;
            foreach (var p in profiles.EnumerateObject())
            {
                var versionId = Str(p.Value, "lastVersionId");
                var parsed = ParseVersionId(versionId);
                if (parsed == null) continue;
                var name = Str(p.Value, "name");
                var gameDir = Str(p.Value, "gameDir");
                list.Add(new ImportCandidate
                {
                    Launcher = LauncherKind.Minecraft,
                    Name = name.Length > 0 ? name : versionId,
                    McVersion = parsed.Value.Mc, Loader = parsed.Value.Loader, LoaderVersion = parsed.Value.LoaderVersion,
                    SourceDir = gameDir.Length > 0 && Directory.Exists(gameDir) ? gameDir : mcDir
                });
            }
            return list;
        }

        public static (string Mc, ModLoaderType Loader, string? LoaderVersion)? ParseVersionId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            id = id.Trim();
            Match m;
            if ((m = Regex.Match(id, @"^fabric-loader-(.+)-(\d[\w.]*)$")).Success) return (m.Groups[2].Value, ModLoaderType.Fabric, m.Groups[1].Value);
            if ((m = Regex.Match(id, @"^quilt-loader-(.+)-(\d[\w.]*)$")).Success) return (m.Groups[2].Value, ModLoaderType.Quilt, m.Groups[1].Value);
            if ((m = Regex.Match(id, @"^(\d[\w.]*)-forge-?(.+)$")).Success) return (m.Groups[1].Value, ModLoaderType.Forge, StripMcPrefix(m.Groups[2].Value, m.Groups[1].Value));
            if ((m = Regex.Match(id, @"^(?:neoforge-)(\d+)\.(\d+)\.[\w.+-]+$")).Success)
            {
                var major = int.Parse(m.Groups[1].Value);
                var minor = int.Parse(m.Groups[2].Value);
                var mc = major == 47 ? "1.20.1" : (minor == 0 ? $"1.{major}" : $"1.{major}.{minor}");   // 20.4.x -> 1.20.4; 21.0.x -> 1.21
                return (mc, ModLoaderType.NeoForge, id.Substring("neoforge-".Length));
            }
            if (Regex.IsMatch(id, @"^\d+\.\d+(\.\d+)?$") || Regex.IsMatch(id, @"^\d{2}w\d{2}[a-z]$")) return (id, ModLoaderType.Vanilla, null);
            return null;   // "latest-release", Optifine, versiones raras...
        }

        // --- Importar ----------------------------------------------------------------------------

        // Carpetas de nivel superior que NUNCA se copian (se regeneran solas o no sirven en Velin).
        private static readonly string[] AlwaysSkip = { "logs", "crash-reports", "natives", ".cache", ".mixin.out", "webcache2", "saves-backup" };
        private static readonly string[] ModFolders = { "mods", "config", "defaultconfigs", "kubejs", "scripts", "patchouli_books", "journeymap", "bluemap" };
        private static readonly string[] OfficialAllowed =
            { "saves", "mods", "config", "resourcepacks", "shaderpacks", "options.txt", "optionsof.txt", "optionsshaders.txt", "servers.dat", "screenshots" };

        // Decide si una entrada de nivel superior de la carpeta del juego se copia con las opciones elegidas.
        public static bool ShouldCopy(string topLevelName, bool official, ImportOptions options)
        {
            if (AlwaysSkip.Contains(topLevelName, StringComparer.OrdinalIgnoreCase)) return false;
            if (topLevelName.Equals("saves", StringComparison.OrdinalIgnoreCase)) return options.Worlds;
            if (ModFolders.Contains(topLevelName, StringComparer.OrdinalIgnoreCase)) return options.Mods;
            if (official && !OfficialAllowed.Contains(topLevelName, StringComparer.OrdinalIgnoreCase)) return false;
            if (topLevelName.StartsWith("minecraftinstance", StringComparison.OrdinalIgnoreCase) || topLevelName.EndsWith(".log", StringComparison.OrdinalIgnoreCase)) return false;
            return options.Other;
        }

        public static async Task<Instance> ImportAsync(ImportCandidate c, ImportOptions options, IProgress<string>? progress,
            IProgress<double>? percent, CancellationToken ct = default)
        {
            if (!Directory.Exists(c.SourceDir)) throw new DirectoryNotFoundException("No se encontró la carpeta del juego: " + c.SourceDir);
            PathService.EnsureAll();
            var settings = SettingsManager.Load();
            var instance = new Instance
            {
                Name = InstanceManager.UniqueName(InstanceManager.SanitizeName(c.Name)),
                McVersion = c.McVersion,
                Loader = c.Loader,
                LoaderVersion = c.LoaderVersion,
                RamMB = c.RamMB > 0 ? c.RamMB : settings.DefaultRamMB,
                Username = settings.DefaultUsername
            };
            instance.Save();

            try
            {
                var official = c.Launcher == LauncherKind.Minecraft;
                var items = Directory.EnumerateFileSystemEntries(c.SourceDir)
                    .Where(p => ShouldCopy(Path.GetFileName(p), official, options)).ToList();

                // Para el porcentaje se cuenta el total de bytes a copiar.
                long total = 0;
                foreach (var item in items) total += SizeOf(item);
                long copied = 0;

                progress?.Report("Copiando archivos de " + c.Name + "...");
                await Task.Run(() =>
                {
                    foreach (var item in items)
                    {
                        ct.ThrowIfCancellationRequested();
                        var dest = Path.Combine(instance.GameDir, Path.GetFileName(item));
                        if (Directory.Exists(item)) CopyDirectory(item, dest, ct, n => { copied += n; if (total > 0) percent?.Report(Math.Min(1.0, (double)copied / total)); });
                        else
                        {
                            Directory.CreateDirectory(instance.GameDir);
                            File.Copy(item, dest, true);
                            copied += new FileInfo(item).Length;
                        }
                    }
                }, ct);
                instance.EnsureFolders();
                percent?.Report(1.0);
            }
            catch
            {
                try { Directory.Delete(instance.InstanceDir, true); } catch { }
                throw;
            }
            return instance;
        }

        private static long SizeOf(string path)
        {
            try
            {
                if (File.Exists(path)) return new FileInfo(path).Length;
                return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });
            }
            catch { return 0; }
        }

        private static void CopyDirectory(string source, string dest, CancellationToken ct, Action<long> onBytes)
        {
            Directory.CreateDirectory(dest);
            foreach (var file in Directory.EnumerateFiles(source))
            {
                ct.ThrowIfCancellationRequested();
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), true);
                try { onBytes(new FileInfo(file).Length); } catch { }
            }
            foreach (var dir in Directory.EnumerateDirectories(source))
                CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)), ct, onBytes);
        }

        // Prism a veces guarda la versión de Forge como "1.20.1-47.2.0": Velin usa sólo "47.2.0".
        private static string StripMcPrefix(string version, string mc) =>
            version.StartsWith(mc + "-", StringComparison.Ordinal) ? version.Substring(mc.Length + 1) : version;

        private static string Str(JsonElement e, string name) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";
    }
}
