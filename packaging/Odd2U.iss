[Setup]
AppId=Odd2U.Family
AppName=Odd2U
AppVersion=1.0.0
DefaultDirName={localappdata}\Programs\Odd2U
DefaultGroupName=Odd2U
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir=..\dist
OutputBaseFilename=Odd2U-Setup-1.0.0-Win11-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Odd2U.exe
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Tasks]
Name: desktopicon; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "..\dist\Odd2U-Portable\*"; DestDir: "{app}"; Excludes: "portable.flag,Data\*,*Checks.exe"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Odd2U"; Filename: "{app}\Odd2U.exe"
Name: "{group}\Uninstall Odd2U"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Odd2U"; Filename: "{app}\Odd2U.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Odd2U.exe"; Description: "Open Odd"; Flags: nowait postinstall skipifsilent unchecked
