; Velin - instalador (Inno Setup 6.3 o superior)
; Se compila con build.ps1 (que publica el programa en ..\publish y pasa /DAppVersion=<version>).
; A mano: ISCC.exe /DAppVersion=1.0.0 installer\velin_installer.iss

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppName "Velin"
#define AppPublisher "Brhenja"
#define AppURL "https://github.com/Brhenja/Velin"

[Setup]
AppId={{7B7E7B10-6B9B-4B3D-9C0E-1A9D9E2E6F00}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
AppCopyright=Copyright (c) 2026 Brhenja
VersionInfoVersion={#AppVersion}
DefaultDirName={localappdata}\Velin
DefaultGroupName=Velin
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=VelinSetup-{#AppVersion}
SetupIconFile=..\Velin\Assets\velin.ico
UninstallDisplayIcon={app}\Velin.exe
CloseApplications=yes
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
spanish.DeleteDataPrompt=¿Querés borrar también tus datos de Velin (instancias, mundos, mods, cuentas y configuración)?%n%nCarpeta: %1%n%nSi elegís "No", se conservan y los vas a tener de nuevo si volvés a instalar Velin.
english.DeleteDataPrompt=Do you also want to delete your Velin data (instances, worlds, mods, accounts and settings)?%n%nFolder: %1%n%nIf you choose "No", it is kept and will be there again if you reinstall Velin.

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; data\ es de cada usuario (instancias, mundos, cuentas): nunca se pisa ni se incluye.
Source: "..\publish\*"; DestDir: "{app}"; Excludes: "data\*"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Velin"; Filename: "{app}\Velin.exe"
Name: "{autodesktop}\Velin"; Filename: "{app}\Velin.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Velin.exe"; Description: "{cm:LaunchProgram,Velin}"; Flags: nowait postinstall skipifsilent

[Code]
// Al desinstalar se conserva la carpeta data\ (instancias y mundos). Se pregunta si se quiere borrar;
// por defecto NO, y en una desinstalacion silenciosa tampoco se borra nada.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{app}\data');
    if DirExists(DataDir) and (not UninstallSilent) then
    begin
      if MsgBox(FmtMessage(CustomMessage('DeleteDataPrompt'), [DataDir]), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
    end;
    // Solo borra la carpeta de instalacion si quedo vacia (si se conservo data\, no se toca).
    RemoveDir(ExpandConstant('{app}'));
  end;
end;
