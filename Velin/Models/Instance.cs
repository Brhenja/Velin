using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Velin.Models
{
    public enum ModLoaderType
    {
        Vanilla,
        Fabric,
        Forge,
        Quilt,
        NeoForge
    }

    public class Instance
    {
        public string Name { get; set; } = "";
        public string McVersion { get; set; } = "";
        public ModLoaderType Loader { get; set; } = ModLoaderType.Vanilla;
        public string? LoaderVersion { get; set; }
        // Nombre de un .jar propio que esta en la carpeta de la instancia ("" = el cliente oficial de Mojang).
        public string CustomJar { get; set; } = "";
        public int RamMB { get; set; } = 2048;
        public string Username { get; set; } = "Player";

        // Cuenta con la que se juega esta instancia ("" = la cuenta por defecto; si no hay, offline con Username).
        public string AccountId { get; set; } = "";
        // Grupo/categoría para ordenar la lista ("" = sin grupo).
        public string Group { get; set; } = "";

        // Estadísticas de uso.
        public DateTime? LastPlayedUtc { get; set; }
        public long TotalPlaySeconds { get; set; }

        // Opciones de lanzamiento por instancia. Todas opcionales: vacío/0/false = comportamiento de siempre.
        public string JavaPath { get; set; } = "";        // Java propio (si no, se usa el que baja el launcher)
        public string ExtraGameArgs { get; set; } = "";   // argumentos extra del juego
        public int WindowWidth { get; set; } = 0;
        public int WindowHeight { get; set; } = 0;
        public bool Fullscreen { get; set; } = false;
        public string JoinServer { get; set; } = "";      // entrar directo a un servidor (host[:puerto])
        public string JoinWorld { get; set; } = "";       // entrar directo a un mundo (1.20+)

        [JsonIgnore]
        public string InstanceDir => Path.Combine(Services.PathService.InstancesDir, Name);
        [JsonIgnore]
        public string GameDir => Path.Combine(InstanceDir, "minecraft");
        [JsonIgnore]
        public string ModsDir => Path.Combine(GameDir, "mods");
        [JsonIgnore]
        public string VersionsDir => Path.Combine(InstanceDir, "versions");
        [JsonIgnore]
        public string NativesDir => Path.Combine(InstanceDir, "natives");
        [JsonIgnore]
        public string ConfigFile => Path.Combine(InstanceDir, "instance.json");

        public void EnsureFolders()
        {
            Directory.CreateDirectory(GameDir);
            Directory.CreateDirectory(ModsDir);
            Directory.CreateDirectory(VersionsDir);
            Directory.CreateDirectory(NativesDir);
            Directory.CreateDirectory(Path.Combine(GameDir, "saves"));
        }

        public void Save()
        {
            EnsureFolders();
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json);
        }

        public static Instance? Load(string dir)
        {
            var file = Path.Combine(dir, "instance.json");
            if (!File.Exists(file)) return null;
            var json = File.ReadAllText(file);
            return JsonSerializer.Deserialize<Instance>(json);
        }

        // Texto de dos líneas para la lista principal: nombre + versión · loader · tiempo jugado.
        [JsonIgnore]
        public string ListCard
        {
            get
            {
                var parts = new List<string> { McVersion.Length == 0 ? "?" : McVersion, Loader.ToString() };
                if (CustomJar.Length > 0) parts.Add("jar propio");
                var play = FormatPlayTime(TotalPlaySeconds);
                if (play.Length > 0) parts.Add(play);
                if (!string.IsNullOrEmpty(Group)) parts.Add("[" + Group + "]");
                return Name + "\n" + string.Join(" · ", parts);
            }
        }

        public static string FormatPlayTime(long seconds)
        {
            if (seconds < 60) return "";
            var h = seconds / 3600;
            var m = (seconds % 3600) / 60;
            return h > 0 ? $"{h} h {m} min" : $"{m} min";
        }

        public override string ToString() => Name;
    }
}
