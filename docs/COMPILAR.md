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
```

Si PowerShell bloquea el script: `powershell -ExecutionPolicy Bypass -File .\build.ps1`.

El script:
1. Lee la versión de `Velin\Velin.csproj` (`<Version>`).
2. Corre las pruebas (`tests\ServiceTests`) y **aborta si alguna falla**.
3. Publica self-contained para win-x64 en `publish\` (incluye .NET: el usuario no instala nada).
4. Crea `dist\Velin-<versión>-portable-win-x64.zip`.
5. Compila `installer\velin_installer.iss` con Inno Setup y deja `dist\VelinSetup-<versión>.exe`.
6. Muestra el SHA256 de cada archivo.

## Paso a paso (sin el script)
```powershell
# Pruebas (devuelve código 1 si alguna falla)
dotnet run --project tests\ServiceTests -c Release

# Publicar
dotnet publish Velin\Velin.csproj -c Release -r win-x64 --self-contained true -p:DebugType=none -p:DebugSymbols=false -o publish

# Instalador
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" /DAppVersion=1.0.0 installer\velin_installer.iss
```

## Cambiar de versión
Editá `<Version>` en `Velin\Velin.csproj`: de ahí salen la versión que muestra el programa, el `User-Agent` y el nombre
de los archivos de `dist\`.

## Estructura
```
Velin.sln
build.ps1
Velin/              proyecto principal (Models, Services, UI, Forms, Assets)
tests/ServiceTests/ pruebas automáticas de modelos y servicios (no incluye las pantallas)
installer/          velin_installer.iss (Inno Setup)
docs/               guías para desarrolladores
```

## Notas
- Las pruebas crean sus datos de prueba dentro de la carpeta de salida de `tests\ServiceTests` (su propio `data\`), no en la de Velin instalado.
- El instalador no está firmado digitalmente: Windows SmartScreen puede mostrar un aviso al ejecutarlo.
- Para activar el login de Microsoft en un fork: [LOGIN_MICROSOFT_FORKS.md](LOGIN_MICROSOFT_FORKS.md).
