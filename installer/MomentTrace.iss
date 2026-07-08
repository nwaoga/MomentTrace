#define MyAppName "MomentTrace"
#define MyAppPublisher "MomentTrace"
#ifndef AppExeName
#define AppExeName "MomentTrace.Desktop.exe"
#endif

#define MyAppExeName AppExeName

#ifndef AppVersion
#define AppVersion "0.1.0"
#endif

#ifndef SourceDir
#define SourceDir "..\publish\win-x64"
#endif

[Setup]
AppId={{B8A71A1C-B3AF-45F4-AC53-3A2C2E756F3F}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=MomentTrace-{#AppVersion}-win-x64-setup
SetupIconFile=..\MomentTrace.App\Assets\MomentTrace.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then
  begin
    Exec(ExpandConstant('{cmd}'), '/C taskkill /IM MomentTrace.Desktop.exe /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec(ExpandConstant('{cmd}'), '/C taskkill /IM MomentTrace.App.exe /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'MomentTrace');
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Nwaoga Timeline');
  end;
end;
