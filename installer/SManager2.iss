; Script Inno Setup — instalador único per-user para SManager 2.0
; Compilar con: tools\Generar-Instalador.ps1 (recomendado) o ISCC con defines.

#ifndef MyAppVersion
  #define MyAppVersion "2.0.0"
#endif

#ifndef StagingDir
  #define StagingDir "..\dist\staging"
#endif

#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

#ifdef MySetupIcon
  #define SetupIconFile MySetupIcon
#endif

[Setup]
AppId={{A7B3C4D5-E6F7-4890-ABCD-012345678901}
AppName=SManager 2.0
AppVersion={#MyAppVersion}
AppVerName=SManager 2.0 {#MyAppVersion}
AppPublisher=SManager
DefaultDirName={localappdata}\Programs\SManager2
DefaultGroupName=SManager 2.0
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=SManager2-Setup-{#MyAppVersion}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
UninstallDisplayIcon={app}\SManager.Gui.WinUI.exe
SetupLogging=yes
; Sin privilegios de administrador: instalación solo para el usuario actual.
; Los datos (config, logs, estado IPC) permanecen en %LOCALAPPDATA%\SManager2.

#ifdef SetupIconFile
SetupIconFile={#SetupIconFile}
#endif

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked

[Files]
; Todo el publish self-contained (GUI + herramientas\ con CLI y Host).
Source: "{#StagingDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\SManager 2.0"; Filename: "{app}\SManager.Gui.WinUI.exe"; WorkingDir: "{app}"
Name: "{group}\Desinstalar SManager 2.0"; Filename: "{uninstallexe}"
Name: "{autodesktop}\SManager 2.0"; Filename: "{app}\SManager.Gui.WinUI.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\SManager.Gui.WinUI.exe"; Description: "Abrir SManager 2.0"; Flags: nowait postinstall skipifsilent

[Code]
// Detiene demonios activos antes de desinstalar (apagado ordenado + cierre forzado de respaldo).
procedure DetenerDemoniosSManager;
var
  RutaSmanager, CarpetaPerfiles, NombrePerfil: String;
  CodigoSalida: Integer;
  Busqueda: TFindRec;
begin
  RutaSmanager := ExpandConstant('{app}\herramientas\smanager.exe');
  if not FileExists(RutaSmanager) then
    Exit;

  CarpetaPerfiles := ExpandConstant('{localappdata}\SManager2\Perfiles');
  if DirExists(CarpetaPerfiles) then
  begin
    if FindFirst(CarpetaPerfiles + '\*', Busqueda) then
    begin
      try
        repeat
          if ((Busqueda.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0)
             and (Busqueda.Name <> '.') and (Busqueda.Name <> '..') then
          begin
            NombrePerfil := Busqueda.Name;
            Exec(RutaSmanager, 'stop -perfil "' + NombrePerfil + '"',
              ExpandConstant('{app}\herramientas'), SW_HIDE, ewWaitUntilTerminated, CodigoSalida);
          end;
        until not FindNext(Busqueda);
      finally
        FindClose(Busqueda);
      end;
    end;
  end;

  // Perfil por defecto por si no hubo carpeta Perfiles\ aún.
  Exec(RutaSmanager, 'stop -perfil General',
    ExpandConstant('{app}\herramientas'), SW_HIDE, ewWaitUntilTerminated, CodigoSalida);

  // Respaldo: procesos huérfanos que no respondieron a APAGAR.
  Exec('taskkill.exe', '/IM SManager.Host.exe /F', '', SW_HIDE, ewWaitUntilTerminated, CodigoSalida);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    DetenerDemoniosSManager;
end;

[UninstallDelete]
; Solo archivos del programa. Los datos del usuario en %LOCALAPPDATA%\SManager2 no se borran.
