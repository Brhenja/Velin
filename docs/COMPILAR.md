# Cómo compilar Velin

Velin es un proyecto **C# / .NET 8 / WinForms** y solo se compila en **Windows x64**.

## Requisitos
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PowerShell (viene con Windows)
- Opcional, solo para el instalador: [Inno Setup 6.3 o superior](https://jrsoftware.org/isdl.php)

## Todo junto: `build.ps1`
Desde la raíz del repositorio (donde está `Velin.sln`):

```powershell
.\build.ps1                  # pruebas + publish + zip portable + instalador
.\build.ps1 -SkipTests       # sin correr las pruebas
.\build.ps1 -SkipInstaller   # sin Inno Setup (solo publish + zip portable)
.\build.ps1 -NoSingleFile     # publish en el modo viejo (muchas .dll sueltas) en vez de un solo Velin.exe
```

Si PowerShell bloquea el script: `powershell -ExecutionPolicy Bypass -File .\build.ps1`.

El script:
1. Lee la versión de `Velin\Velin.csproj` (`<Version>`).
2. Corre las pruebas (`tests\ServiceTests`) y **aborta si alguna falla**.
3. Publica self-contained para win-x64 en `publish\` **en un solo `Velin.exe`** (incluye .NET: el usuario no instala nada).
4. Copia la carpeta `Assets\` completa a `publish\Assets\` y **aborta si falta algún archivo imprescindible** (fuentes,
   ícono, sonido de click, idiomas base y tema). Esto es a propósito: el publish en un solo archivo dejaba afuera algunos.
5. Crea `dist\Velin-<versión>-portable-win-x64.zip` (una sola carpeta `Velin\`).
6. Compila `installer\velin_installer.iss` con Inno Setup y deja `dist\VelinSetup-<versión>.exe`. El instalador borra las
   `.dll` sueltas de versiones anteriores.
7. Genera `dist\SHA256SUMS.txt` y muestra el SHA256 de cada archivo.

## Paso a paso (sin el script)
```powershell
# Pruebas (devuelve código 1 si alguna falla)
dotnet run --project tests\ServiceTests -c Release

# Publicar
dotnet publish Velin\Velin.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=false -p:DebugType=none -p:DebugSymbols=false -o publish
# IMPORTANTE: copiar a mano la carpeta de recursos junto al .exe
Copy-Item Velin\Assets publish\Assets -Recurse -Force

# Instalador
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" /DAppVersion=1.2.0 installer\velin_installer.iss
```

## Cambiar de versión
Editá `<Version>` en `Velin\Velin.csproj`: de ahí salen la versión que muestra el programa, el `User-Agent` y el nombre
de los archivos de `dist\`.

## GitHub Actions
En `.github/workflows/`:
- **`ci.yml`:** en cada push a `main` y en cada pull request corre las pruebas y compila. No hace falta configurar nada.
- **`release.yml`:** se ejecuta **a mano** (pestaña *Actions* → *Release build* → *Run workflow*). Arma el instalador, el zip
  portable y `SHA256SUMS.txt` en un servidor de GitHub y los deja como artefactos descargables. **No crea la release ni
  sube nada a Releases.**

## Publicar una release (a mano)
1. Subí la versión: `<Version>` en `Velin\Velin.csproj` (el instalador y los nombres de `dist\` la toman de ahí) y la
   sección de ese número en `CHANGELOG.md`.
2. Compilá con `.\build.ps1` y probá el instalador. Se generan en `dist\`: `VelinSetup-<versión>.exe`,
   `Velin-<versión>-portable-win-x64.zip` y `SHA256SUMS.txt`.
3. Subí el código con git (`git add`, `git commit`, `git push`).
4. En GitHub: *Releases → Draft a new release*, creá el tag `v<versión>` sobre `main`, pegá las notas (están en
   `docs/RELEASE-<versión>.md`) y adjuntá **los tres archivos** de `dist\`.

> El aviso de actualización de Velin toma la última release de `Brhenja/Velin`, verifica el instalador contra el hash que
> publica GitHub o contra `SHA256SUMS.txt`, y solo descarga desde `github.com/Brhenja/Velin/releases/download`. Por eso
> conviene **adjuntar siempre `SHA256SUMS.txt`** y no renombrar los archivos.

## Firma digital
El instalador no está firmado: el plan gratuito de [SignPath](https://signpath.org) para proyectos de código abierto
pide cierta relevancia del proyecto y la solicitud no fue aprobada. Se puede volver a pedir más adelante, o firmar con un
certificado de pago. Mientras tanto Windows SmartScreen puede mostrar un aviso al ejecutar el instalador.

## Agregar un idioma
Los idiomas son `Velin\Assets\Lang\<código>.json` (en un build instalado, también se leen de `data\lang\`). Copiá
`es.json` o `en.json`, traducí los valores conservando `{0}`, `{1}` y `\n`, y poné `"lang.name"` con el nombre del idioma
en ese idioma. Las pruebas (`tests\ServiceTests`) verifican que cada archivo tenga las mismas claves y los mismos marcadores
que `es.json`. Si el idioma necesita una fuente que las medievales no tienen, ver `LanguageCatalog` (`LatinSafe` y
`CjkFontNames`).

## Desarrollo rápido
`build.ps1` hace todo (ReadyToRun, un solo `.exe`, instalador) y es lento. Para el día a día alcanza con:
```powershell
dotnet build Velin\Velin.csproj -c Debug                    # solo ver si compila (incremental)
dotnet run --project Velin\Velin.csproj -c Debug            # probar el programa sin instalar
dotnet run --project tests\ServiceTests -c Release          # las pruebas
```
La carpeta `Assets\` se copia sola junto al ejecutable de Debug. Dejá `.\build.ps1` para probar el instalador y para la release.

## Estructura
```
Velin.sln
build.ps1
Velin.Core/         núcleo: Models y Services (lógica sin pantallas; no usa WinForms)
Velin/              interfaz de Windows: Forms, UI, Platform (sonido y otras piezas de Windows), Assets (fuentes, temas, idiomas y sonidos)
tests/ServiceTests/ pruebas automáticas del núcleo (no incluye las pantallas)
installer/          velin_installer.iss (Inno Setup)
.github/workflows/  ci.yml y release.yml
docs/               guías para desarrolladores
```

## Qué va en cada proyecto
- **`Velin.Core`** (`net8.0`): todo lo que no es una pantalla: instancias, descargas, mods, cuentas, lanzamiento del juego, importadores, etc. Los namespaces se mantienen (`Velin.Models`, `Velin.Services`). **No puede usar WinForms, `System.Drawing` ni NAudio**: una prueba lo comprueba. Lo que `internal` en el núcleo lo ven `Velin` y `ServiceTests` (`InternalsVisibleTo`).
- **`Velin`** (`net8.0-windows`, WinForms): las ventanas (`Forms`), el tema y los dibujos (`UI`) y `Platform\`, con lo que sólo tiene sentido en Windows (`SoundManager` con NAudio, `MemoryTrimmer`, `SystemLanguage`). Sigue usando el namespace `Velin.Services` en esos archivos para no tocar el resto del código.
- **Costuras con el sistema operativo** (para poder hacer otra interfaz, por ejemplo la de Linux): `SecretProtector.Current` define cómo se cifran los tokens de las cuentas (DPAPI en Windows; otro sistema tiene que poner su propia implementación), y `SystemInfo`, `ShortcutService` y el cifrado se comportan de forma segura fuera de Windows.
- Si agregás una clase nueva: si no necesita una pantalla ni nada de Windows va en `Velin.Core`; si no, en `Velin`.

## Notas
- Las pruebas crean sus datos de prueba dentro de la carpeta de salida de `tests\ServiceTests` (su propio `data\`), no en la de Velin instalado.
- El instalador no está firmado digitalmente (ver "Firma digital"): Windows SmartScreen puede mostrar un aviso al ejecutarlo.
- Velin necesita la carpeta `Assets\` junto al `.exe`: sin ella no hay fuentes, temas, idiomas ni sonidos.
- Para activar el login de Microsoft en un fork: [LOGIN_MICROSOFT_FORKS.md](LOGIN_MICROSOFT_FORKS.md).
