using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Velin.Models;

namespace Velin.Services
{
    // Resultado de instalar (o actualizar) un mod junto con lo que hizo falta para que funcione.
    public class ModInstallResult
    {
        public List<InstalledMod> Installed { get; } = new();   // el mod pedido + sus dependencias nuevas
        public List<string> Warnings { get; } = new();          // dependencias que no se pudieron instalar
    }

    public static class ModManager
    {
        private static string IndexFile(Instance i) => Path.Combine(i.InstanceDir, "mods.json");

        public static InstalledModsIndex LoadIndex(Instance instance)
        {
            var file = IndexFile(instance);
            if (!File.Exists(file)) return new InstalledModsIndex();
            try { return JsonSerializer.Deserialize<InstalledModsIndex>(File.ReadAllText(file)) ?? new InstalledModsIndex(); }
            catch { return new InstalledModsIndex(); }
        }

        public static void SaveIndex(Instance instance, InstalledModsIndex index)
        {
            Directory.CreateDirectory(instance.InstanceDir);
            File.WriteAllText(IndexFile(instance), JsonSerializer.Serialize(index, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static string LoaderCategoryName(ModLoaderType t) => t switch
        {
            ModLoaderType.Fabric => "fabric",
            ModLoaderType.Forge => "forge",
            ModLoaderType.Quilt => "quilt",
            ModLoaderType.NeoForge => "neoforge",
            _ => "fabric"
        };

        // Nombre "normalizado" (sólo letras y números en minúscula) para reconocer el mismo mod aunque venga
        // de otra fuente o se escriba distinto ("Fabric API" == "fabric-api").
        internal static string NormalizeName(string? name) =>
            new string((name ?? "").Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        // ¿Ya está este mod en la instancia? Cuenta como "ya instalado": el mismo proyecto de la misma fuente, o el mismo
        // mod bajado de la OTRA fuente (mismo nombre). Así una dependencia no se duplica aunque la hayas bajado de Modrinth
        // y ahora la pida un mod de CurseForge (dos copias del mismo mod hacen crashear el juego).
        internal static bool IsAlreadyInstalled(Instance instance, ContentSource source, string projectId, string? title)
        {
            var key = ContentSources.SourceKey(source);
            var norm = NormalizeName(title);
            return LoadIndex(instance).Mods.Any(m =>
                (string.Equals(m.Source, key, StringComparison.OrdinalIgnoreCase) && m.ProjectId == projectId)
                || (norm.Length > 0 && NormalizeName(m.ProjectTitle) == norm));
        }

        // Instala un mod (de Modrinth o CurseForge) y todas sus dependencias OBLIGATORIAS que todavía no estén.
        public static async Task<ModInstallResult> InstallModWithDependenciesAsync(
            Instance instance, ContentSource source, string projectId, string projectTitle, Action<string>? status = null)
        {
            var result = new ModInstallResult();

            // Primero reconocemos los .jar que pusiste a mano, para no instalar por duplicado
            // una dependencia que ya tenés (dos copias del mismo mod hacen crashear el juego).
            try { await IdentifyLocalModsAsync(instance, ContentSources.MrApi, status); } catch { /* sin internet o límite de la API: seguimos igual */ }

            var visited = new HashSet<string>();
            await InstallRecursiveAsync(instance, source, projectId, projectTitle, null, visited, result, status, 0);
            return result;
        }

        private static async Task InstallRecursiveAsync(Instance instance, ContentSource source, string projectId, string? title,
            string? pinnedVersionId, HashSet<string> visited, ModInstallResult result, Action<string>? status, int depth)
        {
            if (depth > 8 || !visited.Add(ContentSources.SourceKey(source) + ":" + projectId)) return;

            title ??= await ContentSources.GetProjectTitleAsync(source, projectId);

            // Una dependencia que ya está instalada no se vuelve a bajar.
            if (depth > 0 && IsAlreadyInstalled(instance, source, projectId, title)) return;

            status?.Invoke(depth == 0 ? $"Instalando {title}..." : $"Instalando dependencia: {title}...");

            ContentVersion version;
            if (!string.IsNullOrEmpty(pinnedVersionId) && source == ContentSource.Modrinth)
            {
                var mv = await ContentSources.MrApi.GetVersionByIdAsync(pinnedVersionId)
                    ?? throw new Exception($"No se encontró la versión pedida de {title}.");
                version = ContentSources.FromModrinth(mv, ContentType.Mod)
                    ?? throw new Exception("Esta versión del mod no tiene archivos para descargar.");
            }
            else
            {
                var versions = await ContentSources.GetVersionsAsync(source, ContentType.Mod, projectId, instance);
                if (versions.Count == 0)
                    throw new Exception($"No hay versiones de {title} compatibles con Minecraft {instance.McVersion} / {LoaderCategoryName(instance.Loader)}.");
                version = versions[0];
            }

            var fileName = ContentService.SafeFileName(version.FileName);
            Directory.CreateDirectory(instance.ModsDir);
            var destPath = Path.Combine(instance.ModsDir, fileName);

            // Una dependencia cuyo archivo ya está en la carpeta (activo o desactivado) no se baja de nuevo.
            if (depth > 0 && (File.Exists(destPath) || File.Exists(destPath + DisabledSuffix))) return;

            await ContentSources.DownloadAsync(version, destPath);

            var key = ContentSources.SourceKey(source);
            var index = LoadIndex(instance);
            // Si ya había otra versión de este mod con otro nombre de archivo, se borra la vieja
            // (antes quedaban las dos .jar juntas).
            bool keepDisabled = false;
            foreach (var old in index.Mods.Where(m => m.ProjectId == projectId && string.Equals(m.Source, key, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                var oldPath = Path.Combine(instance.ModsDir, old.Filename);
                if (File.Exists(oldPath + DisabledSuffix)) keepDisabled = true;
                if (!string.Equals(old.Filename, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    try { if (File.Exists(oldPath)) File.Delete(oldPath); } catch { }
                    try { if (File.Exists(oldPath + DisabledSuffix)) File.Delete(oldPath + DisabledSuffix); } catch { }
                }
            }
            // Si el mod estaba desactivado, la versión nueva también queda desactivada.
            if (keepDisabled)
            {
                try { ToggleModFile(destPath); } catch { }
            }
            index.Mods.RemoveAll(m => m.ProjectId == projectId && string.Equals(m.Source, key, StringComparison.OrdinalIgnoreCase));
            var entry = new InstalledMod
            {
                Source = key,
                ProjectId = projectId,
                ProjectTitle = title,
                VersionId = version.Id,
                Filename = fileName,
                Sha1 = version.Sha1 ?? ""
            };
            index.Mods.Add(entry);
            SaveIndex(instance, index);
            result.Installed.Add(entry);

            await InstallDependenciesAsync(instance, source, version, visited, result, status, depth);
        }

        private static async Task InstallDependenciesAsync(Instance instance, ContentSource source, ContentVersion version,
            HashSet<string> visited, ModInstallResult result, Action<string>? status, int depth)
        {
            foreach (var dep in version.Dependencies)
            {
                try
                {
                    var depProject = dep.ProjectId;
                    if (string.IsNullOrEmpty(depProject) && !string.IsNullOrEmpty(dep.VersionId) && source == ContentSource.Modrinth)
                        depProject = (await ContentSources.MrApi.GetVersionByIdAsync(dep.VersionId))?.ProjectId ?? "";
                    if (string.IsNullOrEmpty(depProject)) continue;

                    await InstallRecursiveAsync(instance, source, depProject, null, dep.VersionId, visited, result, status, depth + 1);
                }
                catch (Exception ex)
                {
                    result.Warnings.Add(ex.Message);
                }
            }
        }

        // Un mod desactivado es el mismo .jar con ".disabled" al final: el juego sólo carga los .jar,
        // así que simplemente lo ignora hasta que se reactive.
        public const string DisabledSuffix = ".disabled";

        public static bool IsDisabled(Instance instance, string filename) =>
            File.Exists(Path.Combine(instance.ModsDir, filename + DisabledSuffix));

        // Activa o desactiva un archivo de mod (por su ruta actual). Devuelve la ruta nueva.
        public static string ToggleModFile(string path)
        {
            string target = path.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(0, path.Length - DisabledSuffix.Length)
                : path + DisabledSuffix;
            if (File.Exists(target)) File.Delete(target);
            File.Move(path, target);
            return target;
        }

        public static void RemoveMod(Instance instance, InstalledMod mod)
        {
            var path = Path.Combine(instance.ModsDir, mod.Filename);
            if (File.Exists(path + DisabledSuffix)) File.Delete(path + DisabledSuffix);
            if (File.Exists(path)) File.Delete(path);
            var index = LoadIndex(instance);
            index.Mods.RemoveAll(m => m.ProjectId == mod.ProjectId && string.Equals(m.Source, mod.Source, StringComparison.OrdinalIgnoreCase));
            SaveIndex(instance, index);
        }

        // Para los .jar que están en la carpeta de mods pero no vinieron de Modrinth a través del launcher
        // (los que pusiste a mano): le pregunta a Modrinth si conoce ese archivo por su hash. Si lo conoce,
        // pasa a estar "vinculado" y entonces también se le pueden chequear actualizaciones.
        public static async Task<int> IdentifyLocalModsAsync(Instance instance, ModrinthApi api, Action<string>? status = null)
        {
            if (!Directory.Exists(instance.ModsDir)) return 0;

            var index = LoadIndex(instance);
            var tracked = new HashSet<string>(index.Mods.Select(m => m.Filename), StringComparer.OrdinalIgnoreCase);
            var unknown = new HashSet<string>(index.UnidentifiedSha1, StringComparer.OrdinalIgnoreCase);

            int identified = 0;
            bool changed = false;

            foreach (var path in Directory.GetFiles(instance.ModsDir, "*.jar"))
            {
                var name = Path.GetFileName(path);
                if (tracked.Contains(name)) continue;

                string sha1;
                try { sha1 = await FileDownloader.Sha1Async(path); }
                catch { continue; }
                if (unknown.Contains(sha1)) continue; // Modrinth ya dijo que no lo conoce

                ModrinthVersion? v;
                try { v = await api.GetVersionByFileHashAsync(sha1); }
                catch { continue; } // sin internet: no lo marcamos como desconocido, se reintenta otro día

                if (v == null)
                {
                    index.UnidentifiedSha1.Add(sha1);
                    unknown.Add(sha1);
                    changed = true;
                    continue;
                }

                // Si ya hay otro archivo vinculado a ese mismo proyecto, no lo pisamos.
                if (string.IsNullOrEmpty(v.ProjectId) || index.Mods.Any(m => m.ProjectId == v.ProjectId)) continue;

                status?.Invoke($"Reconocido: {name}");
                var title = await api.GetProjectTitleAsync(v.ProjectId);
                index.Mods.Add(new InstalledMod
                {
                    ProjectId = v.ProjectId,
                    ProjectTitle = title,
                    VersionId = v.Id,
                    Filename = name,
                    Sha1 = sha1
                });
                tracked.Add(name);
                identified++;
                changed = true;
            }

            if (changed) SaveIndex(instance, index);
            return identified;
        }

        public class UpdateInfo
        {
            public InstalledMod Current { get; set; } = null!;
            public ContentVersion Latest { get; set; } = null!;
        }

        // Busca versiones nuevas de los mods instalados, tanto de Modrinth como de CurseForge.
        public static async Task<List<UpdateInfo>> CheckUpdatesAsync(Instance instance)
        {
            var index = LoadIndex(instance);
            var updates = new List<UpdateInfo>();

            foreach (var mod in index.Mods)
            {
                try
                {
                    var source = ContentSources.ParseSource(mod.Source);
                    var versions = await ContentSources.GetVersionsAsync(source, ContentType.Mod, mod.ProjectId, instance);
                    var latest = versions.FirstOrDefault();
                    if (latest == null || latest.Id == mod.VersionId) continue;

                    // En CurseForge los ids de archivo crecen con el tiempo: sólo es "nueva" si el id es mayor
                    // (así no se "actualiza" hacia atrás si la versión instalada es una beta más reciente).
                    if (source == ContentSource.CurseForge
                        && long.TryParse(latest.Id, out var newId) && long.TryParse(mod.VersionId, out var curId) && newId <= curId)
                        continue;

                    updates.Add(new UpdateInfo { Current = mod, Latest = latest });
                }
                catch { /* mod eliminado, sin key de CurseForge o error de red: se salta */ }
            }
            return updates;
        }

        // Baja la versión nueva ANTES de borrar la vieja: si la descarga falla, no te quedás sin el mod.
        public static async Task<ModInstallResult> ApplyUpdateAsync(Instance instance, UpdateInfo update, Action<string>? status = null)
        {
            var result = new ModInstallResult();
            var source = update.Latest.Source;
            var fileName = ContentService.SafeFileName(update.Latest.FileName);

            Directory.CreateDirectory(instance.ModsDir);
            var newPath = Path.Combine(instance.ModsDir, fileName);
            await ContentSources.DownloadAsync(update.Latest, newPath);

            var oldPath = Path.Combine(instance.ModsDir, update.Current.Filename);
            bool wasDisabled = File.Exists(oldPath + DisabledSuffix);
            if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                try { if (File.Exists(oldPath)) File.Delete(oldPath); } catch { }
                try { if (File.Exists(oldPath + DisabledSuffix)) File.Delete(oldPath + DisabledSuffix); } catch { }
            }
            if (wasDisabled)
            {
                try { ToggleModFile(newPath); } catch { }
            }

            var key = ContentSources.SourceKey(source);
            var index = LoadIndex(instance);
            var entry = index.Mods.FirstOrDefault(m => m.ProjectId == update.Current.ProjectId && string.Equals(m.Source, key, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                entry = new InstalledMod { Source = key, ProjectId = update.Current.ProjectId, ProjectTitle = update.Current.ProjectTitle };
                index.Mods.Add(entry);
            }
            entry.VersionId = update.Latest.Id;
            entry.Filename = fileName;
            entry.Sha1 = update.Latest.Sha1 ?? "";
            SaveIndex(instance, index);
            result.Installed.Add(entry);

            // Una versión nueva puede traer dependencias nuevas (las que ya están no se repiten).
            var visited = new HashSet<string> { key + ":" + update.Current.ProjectId };
            await InstallDependenciesAsync(instance, source, update.Latest, visited, result, status, 0);
            return result;
        }
    }
}
