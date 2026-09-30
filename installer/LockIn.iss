#define MyAppName "LOCK-IN"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "LOCK-IN"
#define MyAppExeName "LOCK-IN.exe"

[Setup]
AppId={{A46B580A-3A3B-4B66-9BA9-D3F16E9575C0}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\LOCK-IN
DefaultGroupName=LOCK-IN
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts
OutputBaseFilename=LOCK-IN-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\LOCK-IN"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\LOCK-IN"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch LOCK-IN"; Flags: nowait postinstall skipifsilent
