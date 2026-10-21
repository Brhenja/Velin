using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Velin.Models;
using Velin.Services;

int fails = 0, total = 0;
void Check(bool ok, string name) { total++; if (!ok) fails++; Console.WriteLine((ok ? "  OK   " : "  FALLA ") + name); }

// Las pruebas asumen una carpeta data\ vacia: se borra la de la carpeta de salida de ESTAS pruebas (restos de corridas anteriores).
if (PathService.DataDir.Contains("ServiceTests", StringComparison.OrdinalIgnoreCase) && Directory.Exists(PathService.DataDir))
    Directory.Delete(PathService.DataDir, true);

Console.WriteLine("== ArgsParser / LaunchLog ==");
Check(ArgsParser.Split("-Xmx2G \"-Dfoo=a b\" x").SequenceEqual(new[] { "-Xmx2G", "-Dfoo=a b", "x" }), "Split respeta comillas");
Check(ArgsParser.Split("   ").Count == 0 && ArgsParser.Split(null).Count == 0, "Split de vacío");
var cmd = LaunchLog.FormatCommand(@"C:\Program Files\java.exe", new[] { "-cp", "a b", "--accessToken", "SECRETO123", "--x", "1" });
Check(!cmd.Contains("SECRETO123") && cmd.Contains("***") && cmd.Contains("\"a b\"") && cmd.StartsWith("\"C:\\Program Files"), "el accessToken se oculta en el log");

Console.WriteLine("== Reglas de librerías (Windows x64) ==");
MojangRule R(string action, string? os, string? arch = null) => new() { Action = action, Os = os == null && arch == null ? null : new MojangOsRule { Name = os, Arch = arch } };
Check(MinecraftDownloader.RuleAllowsWindows(null), "sin reglas = permitida");
Check(MinecraftDownloader.RuleAllowsWindows(new() { R("allow", "windows") }), "natives-windows (x64) permitida");
Check(!MinecraftDownloader.RuleAllowsWindows(new() { R("allow", "windows", "x86") }), "natives-windows-x86 YA NO se carga");
Check(!MinecraftDownloader.RuleAllowsWindows(new() { R("allow", "windows", "arm64") }), "natives-windows-arm64 YA NO se carga");
Check(!MinecraftDownloader.RuleAllowsWindows(new() { R("allow", "osx") }), "macOS descartada");
Check(MinecraftDownloader.RuleAllowsWindows(new() { R("allow", null), R("disallow", "osx") }), "allow general + disallow osx = permitida");
Check(!MinecraftDownloader.RuleAllowsWindows(new() { R("allow", null), R("disallow", "windows") }), "disallow windows = descartada");

Console.WriteLine("== Opciones de lanzamiento ==");
var addArgs = typeof(GameLauncher).GetMethod("AddLaunchOptionArgs", BindingFlags.NonPublic | BindingFlags.Static)!;
List<string> Args(Instance i, List<string>? warns = null)
{
    var list = new List<string>();
    addArgs.Invoke(null, new object[] { list, i, (Action<string>)(w => warns?.Add(w)) });
    return list;
}
Check(Args(new Instance { McVersion = "1.21.1" }).Count == 0, "sin opciones no agrega ningún argumento");
Check(string.Join(" ", Args(new Instance { McVersion = "1.21.1", JoinServer = "mc.ejemplo.com:25566" })) == "--quickPlayMultiplayer mc.ejemplo.com:25566", "1.21.1: servidor con quickPlay");
Check(string.Join(" ", Args(new Instance { McVersion = "1.12.2", JoinServer = "mc.ejemplo.com:25566" })) == "--server mc.ejemplo.com --port 25566", "1.12.2: --server/--port");
Check(string.Join(" ", Args(new Instance { McVersion = "1.12.2", JoinServer = "mc.ejemplo.com" })) == "--server mc.ejemplo.com --port 25565", "1.12.2: puerto por defecto");
Check(string.Join(" ", Args(new Instance { McVersion = "1.21.1", JoinWorld = "Mi Mundo" })) == "--quickPlaySingleplayer Mi Mundo", "1.21.1: mundo con quickPlay");
var warns = new List<string>();
Check(Args(new Instance { McVersion = "1.12.2", JoinWorld = "x" }, warns).Count == 0 && warns.Count == 1, "1.12.2: mundo directo se ignora y avisa");
Check(string.Join(" ", Args(new Instance { McVersion = "1.20.1", WindowWidth = 1280, WindowHeight = 720 })) == "--width 1280 --height 720", "ventana 1280x720");
Check(string.Join(" ", Args(new Instance { McVersion = "1.20.1", Fullscreen = true, WindowWidth = 1280, WindowHeight = 720 })) == "--fullscreen", "pantalla completa gana sobre el tamaño");
Check(string.Join("|", Args(new Instance { McVersion = "1.20.1", ExtraGameArgs = "--foo \"a b\"" })) == "--foo|a b", "argumentos extra");
var qp = typeof(GameLauncher).GetMethod("SupportsQuickPlay", BindingFlags.NonPublic | BindingFlags.Static)!;
bool Q(string v) => (bool)qp.Invoke(null, new object[] { v })!;
Check(Q("1.20") && Q("1.21.1") && Q("26.3") && Q("23w14a") && !Q("1.19.4") && !Q("1.12.2") && !Q("b1.7.3") && !Q("1.0") && !Q("22w45a"), "SupportsQuickPlay por versión");

Console.WriteLine("== FileDownloader (servidor local) ==");
var port = 18765;
var listener = new HttpListener();
listener.Prefixes.Add($"http://127.0.0.1:{port}/");
listener.Start();
var payload = Encoding.UTF8.GetBytes(new string('x', 5000));
int flakyHits = 0;
_ = Task.Run(async () =>
{
    while (listener.IsListening)
    {
        HttpListenerContext ctx;
        try { ctx = await listener.GetContextAsync(); } catch { break; }
        var path = ctx.Request.Url!.AbsolutePath;
        if (path == "/flaky" && ++flakyHits <= 2) { ctx.Response.StatusCode = 500; ctx.Response.Close(); continue; }
        if (path == "/missing") { ctx.Response.StatusCode = 404; ctx.Response.Close(); continue; }
        ctx.Response.OutputStream.Write(payload, 0, payload.Length);
        ctx.Response.Close();
    }
});
var tmp = Path.Combine(Path.GetTempPath(), "dl-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tmp);
var sha = Convert.ToHexString(SHA1.HashData(payload));
var f1 = Path.Combine(tmp, "sub", "ok.bin");
await FileDownloader.DownloadAsync($"http://127.0.0.1:{port}/ok", f1, sha, payload.Length);
Check(File.Exists(f1) && File.ReadAllBytes(f1).Length == 5000, "descarga correcta con hash y tamaño");
Check(!Directory.GetFiles(tmp, "*.part", SearchOption.AllDirectories).Any(), "no deja archivos .part");
var f2 = Path.Combine(tmp, "flaky.bin");
await FileDownloader.DownloadAsync($"http://127.0.0.1:{port}/flaky", f2, sha, payload.Length);
Check(File.Exists(f2) && flakyHits == 3, $"reintenta tras 2 errores 500 (intentos: {flakyHits})");
var f3 = Path.Combine(tmp, "badhash.bin");
bool threw = false;
try { await FileDownloader.DownloadAsync($"http://127.0.0.1:{port}/ok", f3, new string('0', 40), payload.Length, 2); } catch (IOException) { threw = true; }
Check(threw && !File.Exists(f3) && !Directory.GetFiles(tmp, "*.part").Any(), "hash incorrecto: falla, no deja el archivo ni .part");
var f4 = Path.Combine(tmp, "badsize.bin");
threw = false;
try { await FileDownloader.DownloadAsync($"http://127.0.0.1:{port}/ok", f4, null, 123, 1); } catch (IOException) { threw = true; }
Check(threw && !File.Exists(f4), "tamaño incorrecto: falla");
threw = false;
try { await FileDownloader.DownloadAsync($"http://127.0.0.1:{port}/missing", Path.Combine(tmp, "m.bin"), null, 0, 2); } catch (IOException) { threw = true; }
Check(threw, "404 persistente: falla con error claro");
// descargas concurrentes al MISMO destino no se pisan
var f5 = Path.Combine(tmp, "same.bin");
await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => FileDownloader.DownloadAsync($"http://127.0.0.1:{port}/ok", f5, sha, payload.Length)));
Check(File.Exists(f5) && new FileInfo(f5).Length == 5000, "6 descargas simultáneas al mismo destino");
Check(FileDownloader.IsValidExisting(f5, 5000) && !FileDownloader.IsValidExisting(f5, 4999) && !FileDownloader.IsValidExisting(f5 + "x", 0), "IsValidExisting");
listener.Stop();

Console.WriteLine("== Instancias: duplicar / renombrar / exportar / importar ==");
PathService.EnsureAll();
var inst = new Instance { Name = "Base", McVersion = "1.20.1", Loader = ModLoaderType.Fabric, LoaderVersion = "0.15.0", RamMB = 3072, Username = "Brhenja", TotalPlaySeconds = 7200, LastPlayedUtc = DateTime.UtcNow };
inst.Save();
Directory.CreateDirectory(Path.Combine(inst.GameDir, "saves", "Mundo1", "region"));
File.WriteAllText(Path.Combine(inst.GameDir, "saves", "Mundo1", "level.dat"), "datos");
File.WriteAllText(Path.Combine(inst.GameDir, "saves", "Mundo1", "region", "r.0.0.mca"), "chunk");
File.WriteAllText(Path.Combine(inst.ModsDir, "mod.jar"), "jar");
File.WriteAllText(Path.Combine(inst.NativesDir, "lwjgl.dll"), "dll");
File.WriteAllText(Path.Combine(inst.InstanceDir, "velin-launch.log"), "log");

Check(InstanceManager.IsValidName("Mi Instancia") && !InstanceManager.IsValidName("a/b") && !InstanceManager.IsValidName(" x") && !InstanceManager.IsValidName("x.") && !InstanceManager.IsValidName(""), "IsValidName");
Check(InstanceManager.UniqueName("Base") == "Base (2)" && InstanceManager.UniqueName("Nuevo") == "Nuevo", "UniqueName");
Check(InstanceManager.SanitizeName("a/b") == "a_b" && InstanceManager.SanitizeName("  x. ") == "x" && InstanceManager.SanitizeName("") == "Instancia" && InstanceManager.SanitizeName(new string((char)97, 100)).Length == 60, "SanitizeName (en Windows también limpia : * ? etc.)");

var dup = await InstanceManager.DuplicateAsync(inst, "Copia");
Check(dup.Name == "Copia" && dup.McVersion == "1.20.1" && dup.Loader == ModLoaderType.Fabric && dup.RamMB == 3072, "duplicar conserva la configuración");
Check(dup.TotalPlaySeconds == 0 && dup.LastPlayedUtc == null, "duplicar reinicia las estadísticas");
Check(File.Exists(Path.Combine(dup.GameDir, "saves", "Mundo1", "region", "r.0.0.mca")) && File.Exists(Path.Combine(dup.ModsDir, "mod.jar")), "duplicar copia mundos y mods");
Check(!File.Exists(Path.Combine(dup.NativesDir, "lwjgl.dll")) && !File.Exists(Path.Combine(dup.InstanceDir, "velin-launch.log")), "duplicar omite nativos y log");
Check(Instance.Load(dup.InstanceDir)?.Name == "Copia", "el instance.json de la copia tiene el nombre nuevo");
bool dupThrew = false; try { await InstanceManager.DuplicateAsync(inst, "Copia"); } catch (IOException) { dupThrew = true; }
Check(dupThrew, "duplicar con nombre existente falla");
Check(Directory.Exists(inst.InstanceDir) && File.Exists(Path.Combine(inst.ModsDir, "mod.jar")), "el original queda intacto");

InstanceManager.Rename(dup, "Renombrada");
Check(!Directory.Exists(Path.Combine(PathService.InstancesDir, "Copia")) && Instance.Load(Path.Combine(PathService.InstancesDir, "Renombrada"))?.Name == "Renombrada", "renombrar mueve la carpeta y actualiza instance.json");
InstanceManager.Rename(dup, "RENOMBRADA");
Check(Directory.GetDirectories(PathService.InstancesDir).Select(Path.GetFileName).Contains("RENOMBRADA"), "renombrar sólo cambiando mayúsculas");

var zip = Path.Combine(tmp, "export.zip");
await InstanceManager.ExportAsync(inst, zip);
using (var z = ZipFile.OpenRead(zip))
{
    var names = z.Entries.Select(e => e.FullName).ToList();
    Check(names.Contains("instance.json") && names.Contains("mods/mod.jar") == false && names.Contains("minecraft/mods/mod.jar"), "export contiene instance.json y mods");
    Check(!names.Any(n => n.StartsWith("natives")) && !names.Contains("velin-launch.log"), "export omite nativos y log");
}
var imp = await InstanceManager.ImportExportedAsync(zip);
Check(imp.Name == "Base (2)" && File.Exists(Path.Combine(imp.GameDir, "saves", "Mundo1", "level.dat")) && imp.RamMB == 3072, "importar: nombre único y contenido completo");
Check(Instance.Load(imp.InstanceDir)?.Name == "Base (2)", "importar: instance.json con el nombre final");
// zip malicioso
var evil = Path.Combine(tmp, "evil.zip");
using (var z = ZipFile.Open(evil, ZipArchiveMode.Create))
{
    var e1 = z.CreateEntry("instance.json");
    using (var w = new StreamWriter(e1.Open())) w.Write(JsonSerializer.Serialize(new Instance { Name = "Evil", McVersion = "1.20.1" }));
    var e2 = z.CreateEntry("../../escapado.txt");
    using (var w = new StreamWriter(e2.Open())) w.Write("x");
}
var evilInst = await InstanceManager.ImportExportedAsync(evil);
Check(evilInst.Name == "Evil" && !File.Exists(Path.Combine(PathService.InstancesDir, "..", "escapado.txt")) && !File.Exists(Path.Combine(PathService.DataDir, "..", "escapado.txt")), "importar ignora rutas que escapan de la carpeta");
var notVelin = Path.Combine(tmp, "random.zip");
using (var z = ZipFile.Open(notVelin, ZipArchiveMode.Create)) { var e = z.CreateEntry("a.txt"); using var w = new StreamWriter(e.Open()); w.Write("hola"); }
bool invalid = false; try { await InstanceManager.ImportExportedAsync(notVelin); } catch (InvalidDataException) { invalid = true; }
Check(invalid, "importar un zip cualquiera da error claro");

Console.WriteLine("== Backup de mundos ==");
var emptyInst = new Instance { Name = "SinMundos", McVersion = "1.20.1" }; emptyInst.Save();
Check(!BackupService.HasWorlds(emptyInst) && await BackupService.BackupWorldsAsync(emptyInst, "manual") == null, "sin mundos: no crea backup");
Check(BackupService.HasWorlds(inst), "HasWorlds detecta mundos");
var b1 = await BackupService.BackupWorldsAsync(inst, "manual");
Check(b1 != null && File.Exists(b1), "crea el backup");
using (var z = ZipFile.OpenRead(b1!)) Check(z.Entries.Any(e => e.FullName.Replace('\\', '/') == "saves/Mundo1/level.dat"), "el zip contiene el mundo");
Check(b1!.StartsWith(PathService.BackupsDir) && !b1.StartsWith(inst.InstanceDir), "el backup queda fuera de la carpeta de la instancia");
for (int i = 0; i < 8; i++) { await BackupService.BackupWorldsAsync(inst, "mods"); await Task.Delay(15); }
Check(Directory.GetFiles(BackupService.BackupDirFor(inst), "saves-*.zip").Length == BackupService.KeepPerInstance, $"conserva sólo los {BackupService.KeepPerInstance} más recientes");
InstanceManager.Delete(inst);
Check(Directory.GetFiles(BackupService.BackupDirFor(inst), "saves-*.zip").Length == BackupService.KeepPerInstance, "los backups sobreviven a eliminar la instancia");

Console.WriteLine("== Importador de modpacks (.mrpack) ==");
var mr = Path.Combine(tmp, "pack.mrpack");
using (var z = ZipFile.Open(mr, ZipArchiveMode.Create))
{
    void Add(string name, string text) { var e = z.CreateEntry(name); using var w = new StreamWriter(e.Open()); w.Write(text); }
    Add("modrinth.index.json", """
    {"formatVersion":1,"game":"minecraft","versionId":"1.0","name":"Mi Pack: Genial?",
     "files":[
       {"path":"mods/a.jar","hashes":{"sha1":"00"},"downloads":["https://evil.example.com/a.jar"],"fileSize":1},
       {"path":"../../fuera.jar","hashes":{"sha1":"00"},"downloads":["https://cdn.modrinth.com/x.jar"],"fileSize":1},
       {"path":"mods/servidor.jar","env":{"client":"unsupported","server":"required"},"downloads":["https://cdn.modrinth.com/s.jar"],"fileSize":1}
     ],
     "dependencies":{"minecraft":"1.20.1","fabric-loader":"0.15.11"}}
    """);
    Add("overrides/config/ajustes.txt", "cfg");
    Add("client-overrides/options.txt", "opts");
    Add("overrides/../../escape.txt", "no");
}
var mrWarnings = new List<string>();
var pack = await ModpackImporter.ImportMrpackAsync(mr, null, null, w => mrWarnings.Add(w));
Check(pack.McVersion == "1.20.1" && pack.Loader == ModLoaderType.Fabric && pack.LoaderVersion == "0.15.11", "lee versión de Minecraft y loader");
Check(!pack.Name.Any(c => Path.GetInvalidFileNameChars().Contains(c)), $"nombre de instancia saneado ({pack.Name})");
Check(File.Exists(Path.Combine(pack.GameDir, "config", "ajustes.txt")) && File.Exists(Path.Combine(pack.GameDir, "options.txt")), "copia overrides y client-overrides");
Check(!File.Exists(Path.Combine(PathService.DataDir, "escape.txt")) && !File.Exists(Path.Combine(PathService.InstancesDir, "escape.txt")), "los overrides no pueden escapar de la carpeta del juego");
Check(mrWarnings.Count == 1 && mrWarnings[0].Contains("2 archivo(s)") && !mrWarnings[0].Contains("servidor.jar"), "descargas no permitidas/rutas inválidas se reportan; los de servidor se omiten");
Check(!File.Exists(Path.Combine(PathService.InstancesDir, "..", "fuera.jar")), "ruta con ../ en un archivo del pack no escribe afuera");
var bad = Path.Combine(tmp, "bad.mrpack");
using (var z = ZipFile.Open(bad, ZipArchiveMode.Create)) { var e = z.CreateEntry("otra.txt"); using var w = new StreamWriter(e.Open()); w.Write("x"); }
invalid = false; try { await ModpackImporter.ImportMrpackAsync(bad); } catch (InvalidDataException) { invalid = true; }
Check(invalid, ".mrpack inválido da error claro");

Console.WriteLine("== Estadísticas / ListCard ==");
Check(Instance.FormatPlayTime(30) == "" && Instance.FormatPlayTime(150) == "2 min" && Instance.FormatPlayTime(7500) == "2 h 5 min", "FormatPlayTime");
Check(new Instance { Name = "X", McVersion = "1.21.1", TotalPlaySeconds = 3600 }.ListCard == "X\n1.21.1 · Vanilla · 1 h 0 min", "ListCard de dos líneas");
var json = JsonSerializer.Serialize(new Instance { Name = "J", McVersion = "1.0" });
Check(!json.Contains("ListCard"), "ListCard no se guarda en el JSON");
var oldJson = """{"Name":"Vieja","McVersion":"1.12.2","Loader":0,"RamMB":2048,"Username":"P","UseTotal":true,"CustomJvmArgs":"-Xfoo"}""";
var old = JsonSerializer.Deserialize<Instance>(oldJson)!;
Check(old.Name == "Vieja" && old.JavaPath == "" && old.WindowWidth == 0 && !old.Fullscreen, "un instance.json viejo (con campos de TOTAL) se sigue leyendo");

Console.WriteLine("== Fase 1: orden de versiones, backups, cambio de versión, cuentas ==");
var nv = new[] { "21.1.99", "21.1.219", "21.1.100", "21.1.5" }.OrderByDescending(v => v, VersionComparer.Instance).ToList();
Check(nv[0] == "21.1.219" && nv[1] == "21.1.100" && nv[2] == "21.1.99" && nv[3] == "21.1.5", "NeoForge: 21.1.219 es más nueva que 21.1.99 (orden numérico, no alfabético)");
Check(VersionComparer.Instance.Compare("47.10.0", "47.2.0") > 0 && VersionComparer.Instance.Compare("1.20.1", "1.20.1") == 0, "VersionComparer: 47.10.0 > 47.2.0 y versiones iguales");

var ri = new Instance { Name = "RestoreTest", McVersion = "1.21.1" };
ri.Save();
var worldDir = Path.Combine(ri.GameDir, "saves", "Mundo1");
Directory.CreateDirectory(worldDir);
File.WriteAllText(Path.Combine(worldDir, "level.dat"), "v1");
var rb = await BackupService.BackupWorldsAsync(ri, "manual");
File.WriteAllText(Path.Combine(worldDir, "level.dat"), "v2");
var listed = BackupService.ListBackups(ri);
Check(listed.Count == 1 && listed[0].Reason == "manual" && listed[0].SizeBytes > 0, "ListBackups lee fecha, tamaño y motivo");
await BackupService.RestoreAsync(ri, rb!);
Check(File.ReadAllText(Path.Combine(worldDir, "level.dat")) == "v1", "restaurar devuelve el contenido del backup");
Check(BackupService.ListBackups(ri).Count == 2 && BackupService.ListBackups(ri).Any(b => b.Reason == "prerestore"), "antes de restaurar se guarda una copia de lo actual");
var ph1Evil = Path.Combine(tmp, "ph1Evil-backup.zip");
using (var z = ZipFile.Open(ph1Evil, ZipArchiveMode.Create)) { var e = z.CreateEntry("../../escape-restore.txt"); using var w = new StreamWriter(e.Open()); w.Write("x"); }
bool ph1EvilBlocked = false; try { await BackupService.RestoreAsync(ri, ph1Evil); } catch (InvalidDataException) { ph1EvilBlocked = true; }
Check(ph1EvilBlocked && File.ReadAllText(Path.Combine(worldDir, "level.dat")) == "v1", "un backup con rutas ../ se rechaza y los mundos quedan intactos");

var ci = new Instance { Name = "CvTest", McVersion = "1.20.1", Loader = ModLoaderType.NeoForge, LoaderVersion = "21.1.99" };
ci.Save();
Directory.CreateDirectory(Path.Combine(ci.VersionsDir, "neoforge-21.1.99"));
File.WriteAllText(Path.Combine(ci.VersionsDir, "1.20.1.jar"), "x");
File.WriteAllText(Path.Combine(ci.InstanceDir, ".velin-loader-neoforge-1.20.1-21.1.99"), "ok");
Directory.CreateDirectory(Path.Combine(ci.InstanceDir, "libraries", "a"));
File.WriteAllText(Path.Combine(ci.ModsDir, "mi-mod.jar"), "m");
InstanceManager.ChangeVersion(ci, "1.20.1", ModLoaderType.NeoForge, "21.1.219");
Check(!Directory.Exists(Path.Combine(ci.VersionsDir, "neoforge-21.1.99")) && File.Exists(Path.Combine(ci.VersionsDir, "1.20.1.jar")), "cambiar sólo el loader borra su perfil viejo y conserva el .jar del cliente");
InstanceManager.ChangeVersion(ci, "1.21.1", ModLoaderType.Fabric, "0.16.0");
var cl = Instance.Load(ci.InstanceDir)!;
Check(cl.McVersion == "1.21.1" && cl.Loader == ModLoaderType.Fabric && cl.LoaderVersion == "0.16.0", "ChangeVersion guarda versión, loader y versión del loader");
Check(!File.Exists(Path.Combine(ci.VersionsDir, "1.20.1.jar")) && !Directory.Exists(Path.Combine(ci.InstanceDir, "libraries")) && !File.Exists(Path.Combine(ci.InstanceDir, ".velin-loader-neoforge-1.20.1-21.1.99")), "al cambiar de Minecraft se limpian cliente, librerías y marcas del loader");
Check(File.Exists(Path.Combine(ci.ModsDir, "mi-mod.jar")), "los mods y mundos no se tocan al cambiar de versión");

var off = AccountManager.AddOffline("Brhenja");
Check(off.Uuid == GameLauncher.OfflineUuid("Brhenja") && !off.IsMicrosoft && AccountManager.Get(off.Id)?.Username == "Brhenja", "cuenta offline: UUID y persistencia");
var sess = await AccountManager.GetSessionAsync(off, ci, "");
Check(sess.Username == "Brhenja" && sess.AccessToken == "0" && sess.UserType == "legacy", "sesión offline");
var noAcc = await AccountManager.GetSessionAsync(null, new Instance { Username = "Solo" }, "");
Check(noAcc.Username == "Solo" && noAcc.Uuid == GameLauncher.OfflineUuid("Solo"), "sin cuenta: offline con el nombre de la instancia");
Check(AccountManager.Resolve(new Instance { AccountId = off.Id }, new AppSettings())?.Id == off.Id && AccountManager.Resolve(new Instance(), new AppSettings { DefaultAccountId = off.Id })?.Id == off.Id && AccountManager.Resolve(new Instance(), new AppSettings()) == null, "Resolve: cuenta de la instancia > por defecto > ninguna");
if (OperatingSystem.IsWindows())
{
    var ms = AccountManager.AddOrUpdateMicrosoft(new MicrosoftAuthResult { MsRefreshToken = "REFRESCO", McAccessToken = "TOKEN-MC", McExpiresUtc = DateTime.UtcNow.AddHours(5), Username = "Premium", Uuid = "abcdef0123456789abcdef0123456789" });
    var rawJson = File.ReadAllText(Path.Combine(PathService.DataDir, "accounts.json"));
    Check(!rawJson.Contains("TOKEN-MC") && !rawJson.Contains("REFRESCO"), "los tokens de Microsoft se guardan cifrados (no en texto plano)");
    var msSess = await AccountManager.GetSessionAsync(ms, ci, "");
    Check(msSess.AccessToken == "TOKEN-MC" && msSess.UserType == "msa" && msSess.Uuid == ms.Uuid, "sesión Microsoft con token vigente (sin red)");
}
foreach (var a in AccountManager.LoadAll()) AccountManager.Remove(a.Id);

Console.WriteLine("== Fase 2: login por navegador, mods, contenido, mundos ==");
var ph2Wait = typeof(MicrosoftAuth).GetMethod("WaitForAuthCodeAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
var ph2Tl = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); ph2Tl.Start();
int ph2Port = ((IPEndPoint)ph2Tl.LocalEndpoint).Port;
var ph2CodeTask = (Task<string>)ph2Wait.Invoke(null, new object[] { ph2Tl, "STATE1", CancellationToken.None })!;
async Task<string> Browser(int portN, string path)
{
    using var c = new System.Net.Sockets.TcpClient();
    await c.ConnectAsync(IPAddress.Loopback, portN);
    var st = c.GetStream();
    await st.WriteAsync(Encoding.ASCII.GetBytes($"GET {path} HTTP/1.1\r\nHost: localhost\r\n\r\n"));
    var buf = new byte[4096]; int n = await st.ReadAsync(buf);
    return Encoding.UTF8.GetString(buf, 0, n);
}
var ph2R1 = await Browser(ph2Port, "/favicon.ico");
var ph2R2 = await Browser(ph2Port, "/?code=XXX&state=OTRO");
var ph2R3 = await Browser(ph2Port, "/?code=ABC%2B123&state=STATE1");
var ph2Code = await ph2CodeTask; ph2Tl.Stop();
Check(ph2R1.StartsWith("HTTP/1.1 404") && ph2R2.StartsWith("HTTP/1.1 400") && ph2R3.StartsWith("HTTP/1.1 200") && ph2Code == "ABC+123", "login por navegador: ignora favicon y state incorrecto, y recibe el código");
var ph2Tl2 = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); ph2Tl2.Start();
var ph2Task2 = (Task<string>)ph2Wait.Invoke(null, new object[] { ph2Tl2, "S", CancellationToken.None })!;
await Browser(((IPEndPoint)ph2Tl2.LocalEndpoint).Port, "/?error=access_denied&state=S");
bool ph2Denied = false; try { await ph2Task2; } catch (MicrosoftAuthException) { ph2Denied = true; }
ph2Tl2.Stop();
Check(ph2Denied, "login por navegador: si el usuario cancela en Microsoft, da un error claro");
Check(MicrosoftAuth.ResolveClientId("  abc  ") == "abc" && MicrosoftAuth.ResolveClientId("") == MicrosoftAuthConfig.ClientId && MicrosoftAuth.IsConfigured("x"), "ResolveClientId: el de settings manda; si no, el que trae Velin");

var ph2Mi = new Instance { Name = "ModsToggleTest", McVersion = "1.21.1" }; ph2Mi.Save();
var ph2Jar = Path.Combine(ph2Mi.ModsDir, "mod.jar"); File.WriteAllText(ph2Jar, "x");
var ph2Off = ModManager.ToggleModFile(ph2Jar);
Check(ph2Off.EndsWith(".jar.disabled") && File.Exists(ph2Off) && !File.Exists(ph2Jar) && ModManager.IsDisabled(ph2Mi, "mod.jar"), "desactivar un mod renombra a .jar.disabled");
var ph2On = ModManager.ToggleModFile(ph2Off);
Check(ph2On == ph2Jar && File.Exists(ph2Jar) && !ModManager.IsDisabled(ph2Mi, "mod.jar"), "reactivar un mod lo deja como .jar");

Check(ContentService.FolderFor(ph2Mi, ContentKind.DataPack, "Mi Mundo") == Path.Combine(ph2Mi.GameDir, "saves", "Mi Mundo", "datapacks")
    && ContentService.FolderFor(ph2Mi, ContentKind.Shader).EndsWith("shaderpacks") && ContentService.ModrinthType(ContentKind.ResourcePack) == "resourcepack", "rutas y tipos de resource packs / shaders / datapacks");
Check(ContentService.SafeFileName("../../a.zip") == "a.zip", "SafeFileName no deja escapar de la carpeta");
var ph2Packs = ContentService.FolderFor(ph2Mi, ContentKind.ResourcePack);
Directory.CreateDirectory(Path.Combine(ph2Packs, "carpeta-pack")); File.WriteAllText(Path.Combine(ph2Packs, "pack.zip"), "z");
var ph2Listed = ContentService.ListInstalled(ph2Packs);
Check(ph2Listed.Count == 2, "ListInstalled ve archivos y carpetas");
ContentService.Remove(Path.Combine(ph2Packs, "pack.zip")); ContentService.Remove(Path.Combine(ph2Packs, "carpeta-pack"));
Check(ContentService.ListInstalled(ph2Packs).Count == 0, "Remove borra archivos y carpetas");

var ph2Wi = new Instance { Name = "WorldsTest", McVersion = "1.21.1" }; ph2Wi.Save();
var ph2W1 = Path.Combine(ph2Wi.GameDir, "saves", "Mundo A");
Directory.CreateDirectory(Path.Combine(ph2W1, "region"));
File.WriteAllText(Path.Combine(ph2W1, "level.dat"), "d"); File.WriteAllText(Path.Combine(ph2W1, "region", "r.0.0.mca"), "abcd");
Directory.CreateDirectory(Path.Combine(ph2Wi.GameDir, "saves", "no-es-un-mundo"));
var ph2List = WorldService.ListWorlds(ph2Wi);
Check(ph2List.Count == 1 && ph2List[0].Name == "Mundo A" && WorldService.DirectorySize(ph2List[0].Path) == 5, "ListWorlds sólo cuenta carpetas con level.dat; calcula el tamaño");
var ph2Dup = await WorldService.DuplicateAsync(ph2Wi, ph2List[0]);
Check(ph2Dup == "Mundo A (copia)" && File.Exists(Path.Combine(ph2Wi.GameDir, "saves", ph2Dup, "region", "r.0.0.mca")), "duplicar un mundo copia todo");
var ph2Bk = await WorldService.BackupWorldAsync(ph2Wi, ph2List[0]);
Check(File.Exists(ph2Bk) && Path.GetFileName(ph2Bk).StartsWith("world-Mundo A-") && BackupService.ListBackups(ph2Wi).Count == 0, "backup de un solo mundo (no se mezcla con los backups de todos los mundos)");
var ph2Zip = Path.Combine(tmp, "mundo.zip");
await WorldService.ExportAsync(ph2List[0], ph2Zip);
var ph2Imp = await WorldService.ImportAsync(ph2Wi, ph2Zip);
Check(ph2Imp == "Mundo A (2)" && File.Exists(Path.Combine(ph2Wi.GameDir, "saves", ph2Imp, "level.dat")), "importar un .zip de mundo (con carpeta adentro) con nombre sin chocar");
var ph2Evil = Path.Combine(tmp, "mundo-malo.zip");
using (var z = ZipFile.Open(ph2Evil, ZipArchiveMode.Create)) { var e = z.CreateEntry("../../fuera.txt"); using var w = new StreamWriter(e.Open()); w.Write("x"); }
bool ph2Blocked = false; try { await WorldService.ImportAsync(ph2Wi, ph2Evil); } catch (InvalidDataException) { ph2Blocked = true; }
Check(ph2Blocked && !File.Exists(Path.Combine(ph2Wi.InstanceDir, "fuera.txt")), "importar un .zip con rutas ../ se rechaza");
var ph2NotWorld = Path.Combine(tmp, "no-mundo.zip");
using (var z = ZipFile.Open(ph2NotWorld, ZipArchiveMode.Create)) { var e = z.CreateEntry("hola.txt"); using var w = new StreamWriter(e.Open()); w.Write("x"); }
bool ph2NotW = false; try { await WorldService.ImportAsync(ph2Wi, ph2NotWorld); } catch (InvalidDataException) { ph2NotW = true; }
Check(ph2NotW, "importar un .zip sin level.dat da un error claro");
WorldService.Delete(ph2List[0]);
Check(!Directory.Exists(ph2W1), "eliminar un mundo");

Console.WriteLine("== Fase 3: NBT, servidores, ping, Java ==");
var ph3Root = new NbtCompound();
var ph3Inner = new NbtCompound();
ph3Inner.Set("name", NbtTag.String("Mi server ñandú"));
ph3Inner.Set("ip", NbtTag.String("play.ejemplo.net:25570"));
ph3Inner.Set("hidden", NbtTag.Byte(1));
ph3Inner.Set("icono", new NbtTag(NbtType.ByteArray, new byte[] { 1, 2, 3 }));
ph3Inner.Set("n", new NbtTag(NbtType.Long, 1234567890123L));
ph3Inner.Set("f", new NbtTag(NbtType.Double, 2.5));
ph3Inner.Set("ints", new NbtTag(NbtType.IntArray, new[] { 1, -2, 3 }));
var ph3List = new NbtList { ElementType = NbtType.Compound }; ph3List.Items.Add(NbtTag.Compound(ph3Inner));
ph3Root.Set("servers", NbtTag.List(ph3List));
ph3Root.Set("otro", NbtTag.String("se conserva"));
var ph3Ms = new MemoryStream(); Nbt.WriteRoot(ph3Ms, "", ph3Root);
ph3Ms.Position = 0;
var ph3Back = Nbt.ReadRoot(ph3Ms, out var ph3Name);
var ph3Srv = ((NbtList)ph3Back.Get("servers")!.Value!).Items[0].Value as NbtCompound;
Check(ph3Name == "" && ph3Srv != null && ph3Srv.GetString("name") == "Mi server ñandú" && (sbyte)ph3Srv.Get("hidden")!.Value! == 1
    && ((byte[])ph3Srv.Get("icono")!.Value!).Length == 3 && (long)ph3Srv.Get("n")!.Value! == 1234567890123L && (double)ph3Srv.Get("f")!.Value! == 2.5
    && ((int[])ph3Srv.Get("ints")!.Value!)[1] == -2 && ph3Back.GetString("otro") == "se conserva", "NBT: escribir y leer de vuelta todos los tipos");
bool ph3Bad = false; try { Nbt.ReadRoot(new MemoryStream(new byte[] { 10, 0, 0, 8, 0 }), out _); } catch (InvalidDataException) { ph3Bad = true; }
Check(ph3Bad, "NBT cortado/corrupto da un error claro (no cuelga)");

var ph3Inst = new Instance { Name = "ServersTest", McVersion = "1.21.1" }; ph3Inst.Save();
Check(ServerListService.Load(ph3Inst).Count == 0, "sin servers.dat: lista vacía");
ServerListService.Save(ph3Inst, new List<ServerEntry> { new ServerEntry { Name = "A", Ip = "a.net" }, new ServerEntry { Name = "B", Ip = "b.net:1234" } });
var ph3Loaded = ServerListService.Load(ph3Inst);
Check(ph3Loaded.Count == 2 && ph3Loaded[1].Ip == "b.net:1234", "guardar y leer servers.dat");
ph3Loaded[0].Raw.Set("hidden", NbtTag.Byte(1)); ph3Loaded[0].Name = "A2";
ServerListService.Save(ph3Inst, ph3Loaded);
var ph3Again = ServerListService.Load(ph3Inst);
Check(ph3Again[0].Name == "A2" && ph3Again[0].Raw.Get("hidden") != null && File.Exists(ServerListService.FilePath(ph3Inst) + ".velin-bak"), "editar conserva los datos extra y deja copia .velin-bak");

Check(ServerPinger.ParseAddress("play.x.net") == ("play.x.net", 25565) && ServerPinger.ParseAddress("play.x.net:25570") == ("play.x.net", 25570), "ParseAddress: puerto por defecto y explícito");
var ph3Srv2 = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0); ph3Srv2.Start();
int ph3SrvPort = ((IPEndPoint)ph3Srv2.LocalEndpoint).Port;
var ph3ServerTask = Task.Run(async () =>
{
    using var cl = await ph3Srv2.AcceptTcpClientAsync();
    var st = cl.GetStream();
    int ReadVar() { int r = 0, sh = 0; while (true) { int b = st.ReadByte(); r |= (b & 0x7F) << sh; if ((b & 0x80) == 0) return r; sh += 7; } }
    int hsLen = ReadVar(); var hs = new byte[hsLen]; st.ReadExactly(hs);
    ReadVar(); st.ReadByte();                      // paquete "status request" (largo 1 + id 0)
    var json = Encoding.UTF8.GetBytes("{\"version\":{\"name\":\"1.21.1\",\"protocol\":767},\"players\":{\"max\":20,\"online\":3},\"description\":{\"text\":\"§aHola \",\"extra\":[{\"text\":\"mundo\"}]}}");
    var body = new MemoryStream(); body.WriteByte(0); body.WriteByte((byte)json.Length); body.WriteByte((byte)(json.Length >> 7)); body.Write(json);
    var pkt = body.ToArray(); var framed = new MemoryStream();
    int len = pkt.Length; while (true) { if ((len & ~0x7F) == 0) { framed.WriteByte((byte)len); break; } framed.WriteByte((byte)((len & 0x7F) | 0x80)); len >>= 7; }
    framed.Write(pkt); st.Write(framed.ToArray());
    int pingLen = ReadVar(); var ping = new byte[pingLen]; st.ReadExactly(ping);
    var pong = new MemoryStream(); pong.WriteByte((byte)pingLen); pong.Write(ping); st.Write(pong.ToArray());
});
var ph3Status = await ServerPinger.PingAsync("127.0.0.1:" + ph3SrvPort, 5000);
await ph3ServerTask; ph3Srv2.Stop();
Check(ph3Status.Online && ph3Status.Players == 3 && ph3Status.MaxPlayers == 20 && ph3Status.Version == "1.21.1" && ph3Status.Motd == "Hola mundo", "ping de servidor: jugadores, versión y MOTD (sin códigos de color)");
var ph3Dead = await ServerPinger.PingAsync("127.0.0.1:1", 1500);
Check(!ph3Dead.Online && ph3Dead.Error.Length > 0, "ping a un servidor caído: offline con motivo");

var ph3JavaRoot = Path.Combine(tmp, "javas");
Directory.CreateDirectory(Path.Combine(ph3JavaRoot, "jdk-21", "bin")); File.WriteAllText(Path.Combine(ph3JavaRoot, "jdk-21", "bin", "javaw.exe"), "x");
File.WriteAllText(Path.Combine(ph3JavaRoot, "jdk-21", "release"), "JAVA_VERSION=\"21.0.4\"\n");
Directory.CreateDirectory(Path.Combine(ph3JavaRoot, "vendor", "jre8", "bin")); File.WriteAllText(Path.Combine(ph3JavaRoot, "vendor", "jre8", "bin", "java.exe"), "x");
File.WriteAllText(Path.Combine(ph3JavaRoot, "vendor", "jre8", "release"), "JAVA_VERSION=\"1.8.0_402\"\n");
var ph3Javas = JavaScanner.Scan(new[] { ph3JavaRoot, Path.Combine(tmp, "no-existe") }).OrderByDescending(j => j.Major).ToList();
Check(ph3Javas.Count == 2 && ph3Javas[0].Major == 21 && ph3Javas[0].Version == "21.0.4" && ph3Javas[1].Major == 8, "JavaScanner encuentra Java 21 y Java 8 (1.8.0_402)");
Check(JavaScanner.ParseMajor("17") == 17 && JavaScanner.ParseMajor("1.8.0_402") == 8 && JavaScanner.ParseMajor("21.0.12.1") == 21, "ParseMajor");

Console.WriteLine("== CurseForge ==");
var cfZip = Path.Combine(tmp, "cf-pack.zip");
using (var z = ZipFile.Open(cfZip, ZipArchiveMode.Create)) { var e = z.CreateEntry("manifest.json"); using var w = new StreamWriter(e.Open()); w.Write("{\"manifestType\":\"minecraftModpack\",\"minecraft\":{\"version\":\"1.20.1\"}}"); }
var notCfZip = Path.Combine(tmp, "no-cf.zip");
using (var z = ZipFile.Open(notCfZip, ZipArchiveMode.Create)) { var e = z.CreateEntry("manifest.json"); using var w = new StreamWriter(e.Open()); w.Write("{\"hola\":1}"); }
Check(CurseForgeService.IsCurseForgeZip(cfZip) && !CurseForgeService.IsCurseForgeZip(notCfZip) && !CurseForgeService.IsCurseForgeZip(Path.Combine(tmp, "no-existe.zip")), "IsCurseForgeZip reconoce sólo manifest.json de tipo minecraftModpack");
Check(CurseForgeService.ParseLoaderId("forge-47.2.0") == (ModLoaderType.Forge, "47.2.0") && CurseForgeService.ParseLoaderId("neoforge-21.1.99") == (ModLoaderType.NeoForge, "21.1.99")
    && CurseForgeService.ParseLoaderId("fabric-0.15.7") == (ModLoaderType.Fabric, "0.15.7") && CurseForgeService.ParseLoaderId("cosa-1").loader == ModLoaderType.Vanilla && CurseForgeService.ParseLoaderId(null).loader == ModLoaderType.Vanilla, "ParseLoaderId (forge/neoforge/fabric/quilt)");
Check(CurseForgeService.LoaderId(ModLoaderType.Forge) == 1 && CurseForgeService.LoaderId(ModLoaderType.Fabric) == 4 && CurseForgeService.LoaderId(ModLoaderType.Quilt) == 5 && CurseForgeService.LoaderId(ModLoaderType.NeoForge) == 6 && CurseForgeService.LoaderId(ModLoaderType.Vanilla) == 0, "ids de loader de CurseForge");
var cfBest = CurseForgeService.PickBest(new[]
{
    new CfFile { Id = 1, ReleaseType = 1, FileDate = new DateTime(2024, 1, 1) },
    new CfFile { Id = 2, ReleaseType = 2, FileDate = new DateTime(2025, 1, 1) },
    new CfFile { Id = 3, ReleaseType = 1, FileDate = new DateTime(2024, 6, 1) }
});
Check(cfBest?.Id == 3, "PickBest: prefiere release y, entre iguales, el más nuevo (aunque haya una beta más reciente)");
Check(CurseForgeApi.ResolveKey("  KEY  ") == "KEY" && CurseForgeApi.ResolveKey("") == "" && CurseForgeApi.ResolveKey(null) == "", "ResolveKey: usa la key del usuario (sin espacios) y vacío si no hay");

Console.WriteLine("== NeoForge 1.20.1, fuentes unificadas, dependencias sin duplicar ==");
Check(LoaderVersionsApi.NormalizeNeoForgeVersion("1.20.1", "47.1.106") == "1.20.1-47.1.106"
    && LoaderVersionsApi.NormalizeNeoForgeVersion("1.20.1", "1.20.1-47.1.106") == "1.20.1-47.1.106"
    && LoaderVersionsApi.NormalizeNeoForgeVersion("1.21.1", "21.1.99") == "21.1.99", "NeoForge 1.20.1: la versión se normaliza a 1.20.1-47.1.x (y las demás quedan igual)");
Check(LoaderVersionsApi.IsLegacyNeoForge("1.20.1", "47.1.106") && LoaderVersionsApi.IsLegacyNeoForge("1.20.1", "1.20.1-47.1.106") && !LoaderVersionsApi.IsLegacyNeoForge("1.21.1", "21.1.99") && !LoaderVersionsApi.IsLegacyNeoForge("1.20.4", "20.4.237"), "IsLegacyNeoForge: sólo 1.20.1 usa el esquema viejo");
var ordLegacy = new[] { "1.20.1-47.1.9", "1.20.1-47.1.106", "1.20.1-47.1.20" }.OrderByDescending(v => v, VersionComparer.Instance).ToList();
Check(ordLegacy[0] == "1.20.1-47.1.106" && ordLegacy[2] == "1.20.1-47.1.9", "versiones de NeoForge 1.20.1 ordenadas numéricamente");

var v10Inst = new Instance { Name = "DepsTest", McVersion = "1.21.1", Loader = ModLoaderType.Fabric }; v10Inst.Save();
var v10Idx = new InstalledModsIndex();
v10Idx.Mods.Add(new InstalledMod { Source = "modrinth", ProjectId = "P1", ProjectTitle = "Fabric API", VersionId = "v", Filename = "fabric-api.jar" });
ModManager.SaveIndex(v10Inst, v10Idx);
Check(ModManager.IsAlreadyInstalled(v10Inst, ContentSource.Modrinth, "P1", "otro nombre")
    && ModManager.IsAlreadyInstalled(v10Inst, ContentSource.CurseForge, "306612", "Fabric API")
    && ModManager.IsAlreadyInstalled(v10Inst, ContentSource.CurseForge, "1", "fabric-api")
    && !ModManager.IsAlreadyInstalled(v10Inst, ContentSource.CurseForge, "9", "Sodium")
    && !ModManager.IsAlreadyInstalled(v10Inst, ContentSource.Modrinth, "P2", "Sodium"), "dependencias: no se reinstala lo que ya está (misma fuente o el mismo mod de la otra fuente)");
Check(ModManager.NormalizeName("Cloth Config API (Fabric)") == "clothconfigapifabric" && ModManager.NormalizeName(null) == "", "NormalizeName");
var v10Old = System.Text.Json.JsonSerializer.Deserialize<InstalledModsIndex>("{\"Mods\":[{\"ProjectId\":\"a\",\"ProjectTitle\":\"A\",\"VersionId\":\"v\",\"Filename\":\"a.jar\",\"Sha1\":\"\"}]}");
Check(v10Old!.Mods[0].Source == "modrinth", "índices de mods viejos (sin 'Source') cuentan como Modrinth");

var v10Cf = ContentSources.FromCurseForge(new CfFile
{
    Id = 7, ModId = 3, DisplayName = "Mod 1.0", FileName = "mod-1.0.jar", DownloadUrl = null, FileLength = 5,
    Hashes = { new CfHash { Algo = 2, Value = "md5" }, new CfHash { Algo = 1, Value = "abc" } },
    Dependencies = { new CfDependency { ModId = 11, RelationType = 3 }, new CfDependency { ModId = 12, RelationType = 2 } }
}, null);
Check(v10Cf.Id == "7" && v10Cf.ProjectId == "3" && v10Cf.Sha1 == "abc" && v10Cf.Url == null && v10Cf.Dependencies.Count == 1 && v10Cf.Dependencies[0].ProjectId == "11", "CurseForge -> versión común: SHA1, y sólo dependencias OBLIGATORIAS");
var v10Mr = ContentSources.FromModrinth(new ModrinthVersion
{
    Id = "v1", ProjectId = "p", VersionNumber = "2.0",
    Files = { new ModrinthFile { Filename = "a.jar", Primary = false, Url = "u1" }, new ModrinthFile { Filename = "b.jar", Primary = true, Url = "u2", Hashes = new ModrinthHashes { Sha1 = "h" } } },
    Dependencies = { new ModrinthDependency { ProjectId = "d1", DependencyType = "required" }, new ModrinthDependency { ProjectId = "d2", DependencyType = "optional" } }
}, ContentType.Mod);
Check(v10Mr != null && v10Mr.FileName == "b.jar" && v10Mr.Sha1 == "h" && v10Mr.Name == "2.0" && v10Mr.Dependencies.Count == 1 && v10Mr.Dependencies[0].ProjectId == "d1", "Modrinth -> versión común: archivo principal y sólo dependencias obligatorias");
var v10Pack = ContentSources.FromModrinth(new ModrinthVersion { Files = { new ModrinthFile { Filename = "x.zip", Primary = true }, new ModrinthFile { Filename = "p.mrpack" } } }, ContentType.ModPack);
Check(v10Pack != null && v10Pack.FileName == "p.mrpack" && ContentSources.FromModrinth(new ModrinthVersion(), ContentType.Mod) == null, "modpacks de Modrinth: se elige el .mrpack; una versión sin archivos se descarta");
Check(!ContentSources.Supports(ContentSource.CurseForge, ContentType.DataPack) && ContentSources.Supports(ContentSource.Modrinth, ContentType.DataPack) && ContentSources.Supports(ContentSource.CurseForge, ContentType.Mod)
    && ContentSources.ParseSource("curseforge") == ContentSource.CurseForge && ContentSources.ParseSource(null) == ContentSource.Modrinth, "Supports y ParseSource");

Console.WriteLine("== Versión final: .jar propio, AppInfo, restaurar backup ==");
void FinMakeJar(string path, string? versionJson)
{
    if (File.Exists(path)) File.Delete(path);
    using (var z = ZipFile.Open(path, ZipArchiveMode.Create))
    {
        var e1 = z.CreateEntry("net/minecraft/Main.class");
        using (var w1 = new StreamWriter(e1.Open())) w1.Write("clase");
        if (versionJson != null)
        {
            var e2 = z.CreateEntry("version.json");
            using var w2 = new StreamWriter(e2.Open());
            w2.Write(versionJson);
        }
    }
}
var finDir = Path.Combine(tmp, "fin-jars");
Directory.CreateDirectory(finDir);
var finJarNew = Path.Combine(finDir, "nuevo.jar");
FinMakeJar(finJarNew, "{\"id\":\"1.20.1\",\"name\":\"1.20.1\"}");
var finJarOld = Path.Combine(finDir, "viejo.jar");
FinMakeJar(finJarOld, null);
var finJarBlank = Path.Combine(finDir, "vacio.jar");
FinMakeJar(finJarBlank, "{\"id\":\"  \"}");
var finJarBad = Path.Combine(finDir, "roto.jar");
File.WriteAllText(finJarBad, "esto no es un zip");
Check(CustomJar.TryReadVersion(finJarNew) == "1.20.1", "CustomJar.TryReadVersion lee el id de version.json");
Check(CustomJar.TryReadVersion(finJarOld) == null, "CustomJar.TryReadVersion sin version.json devuelve null");
Check(CustomJar.TryReadVersion(finJarBlank) == null && CustomJar.TryReadVersion(finJarBad) == null && CustomJar.TryReadVersion(Path.Combine(finDir, "no-existe.jar")) == null, "TryReadVersion: id vacío, jar roto o inexistente devuelven null (sin excepción)");
Check(Path.GetFileName(CustomJar.FindJarIn(finDir)) == "nuevo.jar", "FindJarIn elige el primer .jar por nombre");
var finEmptyDir = Path.Combine(tmp, "fin-empty");
Directory.CreateDirectory(finEmptyDir);
Check(CustomJar.FindJarIn(finEmptyDir) == null, "FindJarIn en una carpeta sin .jar devuelve null");

var finClient = Path.Combine(tmp, "fin-inst", "versions", "1.20.1.jar");
Check(CustomJar.PrepareClientJar(finJarNew, finClient) && File.Exists(finClient) && new FileInfo(finClient).Length == new FileInfo(finJarNew).Length, "PrepareClientJar copia el .jar la primera vez");
Check(!CustomJar.PrepareClientJar(finJarNew, finClient), "PrepareClientJar no vuelve a copiar si es igual");
FinMakeJar(finJarNew, "{\"id\":\"1.20.1\",\"extra\":\"cambio para alterar el tamaño\"}");
Check(CustomJar.PrepareClientJar(finJarNew, finClient) && new FileInfo(finClient).Length == new FileInfo(finJarNew).Length, "PrepareClientJar recopia si el .jar cambió");
bool finMissing = false;
try { CustomJar.PrepareClientJar(Path.Combine(finDir, "no-existe.jar"), finClient); } catch (FileNotFoundException) { finMissing = true; }
Check(finMissing, "PrepareClientJar lanza FileNotFoundException si falta el .jar propio");

// Carpetas con un .jar dentro de data\instances: Velin las adopta como instancias
var finA = Path.Combine(PathService.InstancesDir, "FinJarConVersion");
Directory.CreateDirectory(finA);
FinMakeJar(Path.Combine(finA, "cliente.jar"), "{\"id\":\"1.20.1\"}");
var finB = Path.Combine(PathService.InstancesDir, "FinJarSinVersion");
Directory.CreateDirectory(finB);
FinMakeJar(Path.Combine(finB, "cliente-viejo.jar"), null);
var finC = Path.Combine(PathService.InstancesDir, "FinCarpetaVacia");
Directory.CreateDirectory(finC);
var finAll = InstanceManager.LoadAll();
var finInstA = finAll.FirstOrDefault(i => i.Name == "FinJarConVersion");
var finInstB = finAll.FirstOrDefault(i => i.Name == "FinJarSinVersion");
Check(finInstA != null && finInstA.CustomJar == "cliente.jar" && finInstA.McVersion == "1.20.1" && File.Exists(Path.Combine(finA, "instance.json")), "LoadAll adopta una carpeta con .jar (versión leída de version.json) y guarda instance.json");
Check(finInstB != null && finInstB.CustomJar == "cliente-viejo.jar" && finInstB.McVersion == "", "LoadAll adopta un .jar sin version.json con versión vacía (se pregunta al jugar)");
Check(!finAll.Any(i => i.Name == "FinCarpetaVacia"), "una carpeta sin .jar ni instance.json no se toma como instancia");
Check(finInstA != null && finInstA.ListCard == "FinJarConVersion\n1.20.1 · Vanilla · jar propio", "ListCard muestra \"jar propio\"");
Check(finInstB != null && finInstB.ListCard == "FinJarSinVersion\n? · Vanilla · jar propio", "ListCard de un jar propio sin versión muestra \"?\"");
Check(InstanceManager.LoadAll().Count(i => i.Name == "FinJarConVersion") == 1 && Instance.Load(finA)?.CustomJar == "cliente.jar", "volver a cargar no duplica ni rehace la instancia adoptada");

// AppInfo
Check(AppInfo.UserAgent.Contains(AppInfo.RepoUrl) && AppInfo.UserAgent.StartsWith("Velin/") && AppInfo.RepoUrl == "https://github.com/Brhenja/Velin" && AppInfo.Author == "Brhenja", "AppInfo.UserAgent incluye la URL del repositorio");
Check(System.Text.RegularExpressions.Regex.IsMatch(AppInfo.Version, @"^\d+\.\d+\.\d+$"), "AppInfo.Version tiene formato X.Y.Z");

// Regresión: restaurar un backup deja saves\<mundo> (no saves\saves\<mundo>)
var finRi = new Instance { Name = "FinRestore", McVersion = "1.21.1" };
finRi.Save();
var finSaves = Path.Combine(finRi.GameDir, "saves");
var finWorld = Path.Combine(finSaves, "Mundo1");
Directory.CreateDirectory(Path.Combine(finWorld, "region"));
File.WriteAllText(Path.Combine(finWorld, "level.dat"), "original");
File.WriteAllText(Path.Combine(finWorld, "region", "r.0.0.mca"), "chunks");
var finBk = await BackupService.BackupWorldsAsync(finRi, "manual");
File.WriteAllText(Path.Combine(finWorld, "level.dat"), "modificado");
Directory.CreateDirectory(Path.Combine(finSaves, "MundoNuevo"));
await BackupService.RestoreAsync(finRi, finBk!);
Check(File.ReadAllText(Path.Combine(finSaves, "Mundo1", "level.dat")) == "original" && File.Exists(Path.Combine(finSaves, "Mundo1", "region", "r.0.0.mca")), "restaurar backup: el mundo queda en saves\\Mundo1\\level.dat");
Check(!Directory.Exists(Path.Combine(finSaves, "saves")) && !Directory.Exists(Path.Combine(finSaves, "MundoNuevo")), "restaurar backup: no queda saves\\saves y se reemplaza lo anterior");
Check(!Directory.GetDirectories(finRi.GameDir).Any(d => Path.GetFileName(d).Contains(".restoring-") || Path.GetFileName(d).Contains(".old-")), "restaurar backup: no deja carpetas temporales");

Console.WriteLine($"\nRESULTADO: {total - fails}/{total} pruebas OK");
Directory.Delete(tmp, true);
if (PathService.DataDir.Contains("ServiceTests", StringComparison.OrdinalIgnoreCase) && Directory.Exists(PathService.DataDir))
    try { Directory.Delete(PathService.DataDir, true); } catch { }
return fails == 0 ? 0 : 1;
