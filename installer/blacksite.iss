; BLACKSITE installer for Inno Setup 6.3+.
; Per-user install (no administrator rights). Build with scripts\build-release.ps1, which publishes the app first
; and passes /DMyAppVersion=... and /DSourceDir=<publish folder>.
; Uninstall keeps the user's settings in %AppData%\BlackSite.

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\dist\publish"
#endif
#define MyAppName "BLACKSITE"
#define MyAppExeName "BlackSite.exe"

[Setup]
AppId={{B7D1A2C4-5E3F-4A86-9C10-2D8E6F4B7A35}
AppName={#MyAppName} System Control Panel
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=BLACKSITE
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist
OutputBaseFilename=BlackSite-{#MyAppVersion}-Setup-win-x64
SetupIconFile=..\src\BlackSite.App\Assets\blacksite.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
AppMutex=Local\BlackSite.SystemControlPanel
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch BLACKSITE"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; The optional "launch at sign-in" shortcut created by the app itself.
Type: files; Name: "{userstartup}\BlackSite.lnk"
