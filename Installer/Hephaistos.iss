#define MyAppName "Héphaïstos"
#define MyAppVersion "1.1 Beta"
#define MyAppExeName "Hephaistos.exe"
#define MyAppPublisher "Héphaïstos"
#define MyAppId "Hephaistos.Desktop"
#define MyPortableDir "dist\Hephaistos-Beta-1.1-Windows-x64"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} Beta 1.1
AppPublisher={#MyAppPublisher}
VersionInfoVersion=1.1.0.0
VersionInfoProductName={#MyAppName}
VersionInfoDescription=Installateur de {#MyAppName}
VersionInfoCompany={#MyAppPublisher}
SourceDir=..
DefaultDirName={localappdata}\Programs\Hephaistos
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
SetupArchitecture=x64
WizardStyle=modern
SetupIconFile=Assets\hephaistos.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
OutputDir=dist\installer
OutputBaseFilename=Hephaistos-Beta-1.1-Windows-x64-Installer
Compression=lzma2/max
SolidCompression=yes
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousTasks=yes
AllowNoIcons=yes

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"; GroupDescription: "Raccourcis :"; Flags: checkedonce

[Files]
Source: "{#MyPortableDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{userdesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Lancer {#MyAppName}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent
