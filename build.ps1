<#
.SYNOPSIS
  Arma Velin para publicar: pruebas -> publish (self-contained) -> zip portable -> instalador.
.EXAMPLE
  .\build.ps1                    # todo
  .\build.ps1 -SkipTests         # sin correr las pruebas
  .\build.ps1 -SkipInstaller     # solo publish + zip portable (no hace falta Inno Setup)
#>
param(
    [switch]$SkipTests,
    [switch]$SkipInstaller
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
Write-Host "== Publicando (win-x64, self-contained) ==" -ForegroundColor Cyan
if (Test-Path publish) { Remove-Item publish -Recurse -Force }
dotnet publish Velin\Velin.csproj -c Release -r win-x64 --self-contained true -p:DebugType=none -p:DebugSymbols=false -o publish
if ($LASTEXITCODE -ne 0) { Fail "dotnet publish fallo (codigo $LASTEXITCODE)." }
if (-not (Test-Path 'publish\Velin.exe')) { Fail "No se genero publish\Velin.exe." }

# --- Zip portable ---
New-Item -ItemType Directory -Force -Path dist | Out-Null
$zip = Join-Path $PSScriptRoot "dist\Velin-$version-portable-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Write-Host "== Zip portable ==" -ForegroundColor Cyan
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $PSScriptRoot 'publish'), $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
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

# --- Resumen ---
Write-Host ""
Write-Host "== Listo. Archivos en dist\ ==" -ForegroundColor Green
foreach ($a in $artifacts) {
    if (-not (Test-Path $a)) { Fail "Falta el archivo esperado: $a" }
    $h = Get-FileHash -Algorithm SHA256 -LiteralPath $a
    "{0}`n  {1:N1} MB   SHA256 {2}" -f (Split-Path $a -Leaf), ((Get-Item $a).Length / 1MB), $h.Hash
}
