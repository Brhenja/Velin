<#
.SYNOPSIS
  Arma Velin para publicar: pruebas -> publish (self-contained, en UN solo .exe) -> zip portable (con carpeta) -> instalador.
.EXAMPLE
  .\build.ps1                    # todo
  .\build.ps1 -SkipTests         # sin correr las pruebas
  .\build.ps1 -SkipInstaller     # solo publish + zip portable (no hace falta Inno Setup)
  .\build.ps1 -NoSingleFile      # publish clasico (cientos de .dll sueltas), por si el .exe unico diera problemas
#>
param(
    [switch]$SkipTests,
    [switch]$SkipInstaller,
    [switch]$NoSingleFile
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Fail($msg) { Write-Host "ERROR: $msg" -ForegroundColor Red; exit 1 }

# --- Version (sale del .csproj) ---
$csproj = Join-Path $PSScriptRoot 'Velin\Velin.csproj'
[xml]$xml = Get-Content -LiteralPath $csproj -Raw
$version = @($xml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ }) | Select-Object -First 1
if (-not $version -or $version -notmatch '^\d+\.\d+\.\d+$') { Fail "No pude leer <Version> (X.Y.Z) de Velin\Velin.csproj." }
Write-Host "== Velin $version ==" -ForegroundColor Cyan

# --- Pruebas ---
if ($SkipTests) {
    Write-Host "Pruebas omitidas (-SkipTests)." -ForegroundColor Yellow
} else {
    Write-Host "== Pruebas ==" -ForegroundColor Cyan
    dotnet run --project tests\ServiceTests -c Release
    if ($LASTEXITCODE -ne 0) { Fail "Las pruebas fallaron (codigo $LASTEXITCODE). No se genera nada." }
}

# --- Publish self-contained ---
# Por defecto todo el runtime de .NET va adentro de un unico Velin.exe (la carpeta queda con Velin.exe + Assets\,
# en vez de cientos de .dll sueltas). Las librerias nativas se extraen una sola vez a %TEMP%\.net al primer arranque.
Write-Host "== Publicando (win-x64, self-contained) ==" -ForegroundColor Cyan
if (Test-Path publish) { Remove-Item publish -Recurse -Force }
$publishArgs = @('publish', 'Velin\Velin.csproj', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
                 '-p:DebugType=none', '-p:DebugSymbols=false', '-o', 'publish')
if (-not $NoSingleFile) {
    $publishArgs += @('-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:EnableCompressionInSingleFile=false')
}
dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { Fail "dotnet publish fallo (codigo $LASTEXITCODE)." }
if (-not (Test-Path 'publish\Velin.exe')) { Fail "No se genero publish\Velin.exe." }

# --- Assets ---
# El publish en un solo .exe dejaba afuera algunos archivos de Assets\ (fuentes, icono, musica). Se copia la carpeta entera
# a mano, sin depender de lo que decida el SDK, y se verifica que esten los imprescindibles.
$assetsSrc = Join-Path $PSScriptRoot 'Velin\Assets'
$assetsDst = Join-Path $PSScriptRoot 'publish\Assets'
New-Item -ItemType Directory -Force -Path $assetsDst | Out-Null
Copy-Item -Path (Join-Path $assetsSrc '*') -Destination $assetsDst -Recurse -Force
foreach ($f in @('MedievalSharp.ttf', 'Cinzel.ttf', 'velin.ico', 'velin_bgmusic.wav', 'velin_theme.wav', 'click.wav', 'flash_mascot.gif', 'Lang\es.json', 'Lang\en.json', 'Themes\velin.json')) {
    if (-not (Test-Path (Join-Path $assetsDst $f))) { Fail "Falta publish\Assets\$f" }
}
Write-Host ("Assets copiados a publish\: {0} archivos" -f @(Get-ChildItem $assetsDst -Recurse -File).Count) -ForegroundColor DarkGray
$dllCount = @(Get-ChildItem publish -Filter *.dll -File).Count
Write-Host "Archivos .dll sueltas en publish\: $dllCount" -ForegroundColor DarkGray

# --- Zip portable ---
# El zip trae UNA carpeta "Velin" con todo adentro, para que al extraerlo en el escritorio no se llene de archivos sueltos.
New-Item -ItemType Directory -Force -Path dist | Out-Null
$zip = Join-Path $PSScriptRoot "dist\Velin-$version-portable-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Write-Host "== Zip portable ==" -ForegroundColor Cyan
$stageRoot = Join-Path $PSScriptRoot 'dist\_stage'
$stage = Join-Path $stageRoot 'Velin'
if (Test-Path $stageRoot) { Remove-Item $stageRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot 'publish\*') -Destination $stage -Recurse -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $true)
Remove-Item $stageRoot -Recurse -Force
$artifacts = @($zip)

# --- Instalador (Inno Setup) ---
if ($SkipInstaller) {
    Write-Host "Instalador omitido (-SkipInstaller)." -ForegroundColor Yellow
} else {
    Write-Host "== Instalador ==" -ForegroundColor Cyan
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LocalAppData\Programs\Inno Setup 6\ISCC.exe"
    )
    $iscc = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $iscc) { Fail "No encontre ISCC.exe. Instala Inno Setup 6 (https://jrsoftware.org/isdl.php) o corre .\build.ps1 -SkipInstaller." }
    & $iscc "/DAppVersion=$version" 'installer\velin_installer.iss'
    if ($LASTEXITCODE -ne 0) { Fail "Inno Setup fallo (codigo $LASTEXITCODE)." }
    $artifacts += (Join-Path $PSScriptRoot "dist\VelinSetup-$version.exe")
}

# --- Resumen + SHA256SUMS.txt (el aviso de actualizacion de Velin lo usa para verificar la descarga) ---
Write-Host ""
Write-Host "== Listo. Archivos en dist\ ==" -ForegroundColor Green
$sums = @()
foreach ($a in $artifacts) {
    if (-not (Test-Path $a)) { Fail "Falta el archivo esperado: $a" }
    $h = Get-FileHash -Algorithm SHA256 -LiteralPath $a
    "{0}`n  {1:N1} MB   SHA256 {2}" -f (Split-Path $a -Leaf), ((Get-Item $a).Length / 1MB), $h.Hash
    $sums += ("{0}  {1}" -f $h.Hash.ToLowerInvariant(), (Split-Path $a -Leaf))
}
Set-Content -LiteralPath (Join-Path $PSScriptRoot 'dist\SHA256SUMS.txt') -Value $sums -Encoding ascii
