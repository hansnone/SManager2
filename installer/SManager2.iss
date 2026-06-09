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
ChangesEnvironment=yes
; Sin privilegios de administrador: instalación solo para el usuario actual.
; Los datos (config, logs, estado IPC) permanecen en %LOCALAPPDATA%\SManager2.

#ifdef SetupIconFile
SetupIconFile={#SetupIconFile}
#endif

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked
Name: "autostart"; Description: "Iniciar SManager 2.0 al iniciar Windows (minimizado)"; GroupDescription: "Opciones adicionales:"; Flags: unchecked

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SManager2"; ValueData: """{app}\SManager.Gui.WinUI.exe"" -minimized"; Tasks: autostart

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
const
  ClaveEntornoUsuario = 'Environment';

// Evita duplicar la carpeta herramientas\ en PATH al reinstalar o actualizar.
// ChangesEnvironment=yes en [Setup] notifica a Windows del cambio de PATH al finalizar.
function NecesitaAnadirAlPath(const Directorio: String): Boolean;
var
  RutasActuales: String;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, ClaveEntornoUsuario, 'Path', RutasActuales) then
  begin
    Result := True;
    Exit;
  end;
  Result := Pos(';' + Uppercase(Directorio) + ';', ';' + Uppercase(RutasActuales) + ';') = 0;
end;

procedure AnadirAlPathUsuario(const Directorio: String);
var
  RutasActuales: String;
begin
  if not NecesitaAnadirAlPath(Directorio) then
    Exit;

  if RegQueryStringValue(HKEY_CURRENT_USER, ClaveEntornoUsuario, 'Path', RutasActuales) then
    RegWriteExpandStringValue(HKEY_CURRENT_USER, ClaveEntornoUsuario, 'Path', RutasActuales + ';' + Directorio)
  else
    RegWriteExpandStringValue(HKEY_CURRENT_USER, ClaveEntornoUsuario, 'Path', Directorio);
end;

procedure QuitarDelPathUsuario(const Directorio: String);
var
  RutasActuales, RutasNormalizadas: String;
begin
  if not RegQueryStringValue(HKEY_CURRENT_USER, ClaveEntornoUsuario, 'Path', RutasActuales) then
    Exit;

  RutasNormalizadas := ';' + RutasActuales + ';';
  StringChangeEx(RutasNormalizadas, ';' + Directorio + ';', ';', True);

  if Length(RutasNormalizadas) > 1 then
    Delete(RutasNormalizadas, 1, 1);
  if Length(RutasNormalizadas) > 1 then
    Delete(RutasNormalizadas, Length(RutasNormalizadas), 1);

  RegWriteExpandStringValue(HKEY_CURRENT_USER, ClaveEntornoUsuario, 'Path', RutasNormalizadas);
end;

// Detiene demonios activos antes de desinstalar (apagado ordenado + cierre forzado de respaldo).
procedure DetenerDemoniosSManager;
var
  RutaSmanager, CarpetaPerfiles, NombrePerfil: String;
  CodigoSalida: Integer;
  Busqueda: TFindRec;
begin
  // Durante upgrade la GUI puede bloquear el .exe; cerrarla antes de copiar ficheros.
  Exec('taskkill.exe', '/IM SManager.Gui.WinUI.exe /F', '', SW_HIDE, ewWaitUntilTerminated, CodigoSalida);

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

procedure CurStepChanged(CurStep: TSetupStep);
var
  CarpetaHerramientas: String;
begin
  // Actualización encima de instalación anterior: detener procesos antes de sobrescribir.
  if CurStep = ssInstall then
    DetenerDemoniosSManager;

  // Tras copiar ficheros: exponer smanager.exe en PATH (solo HKCU, sin admin).
  if CurStep = ssPostInstall then
  begin
    CarpetaHerramientas := ExpandConstant('{app}\herramientas');
    AnadirAlPathUsuario(CarpetaHerramientas);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    DetenerDemoniosSManager;
    RegDeleteValue(HKEY_CURRENT_USER, 'Software\Microsoft\Windows\CurrentVersion\Run', 'SManager2');
    QuitarDelPathUsuario(ExpandConstant('{app}\herramientas'));
  end;
end;

[UninstallDelete]
; Solo archivos del programa. Los datos del usuario en %LOCALAPPDATA%\SManager2 no se borran.
