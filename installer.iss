[Setup]
AppName=WSL USB Bridge
AppVersion={#APP_VERSION}
AppPublisher=bherbruck
DefaultDirName={localappdata}\WSL USB Bridge
DefaultGroupName=WSL USB Bridge
UninstallDisplayIcon={app}\UsbBridge.exe
OutputDir=output
OutputBaseFilename=WslUsbBridge-Setup-x64
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\WSL USB Bridge"; Filename: "{app}\UsbBridge.exe"
Name: "{autodesktop}\WSL USB Bridge"; Filename: "{app}\UsbBridge.exe"; Tasks: desktopicon
Name: "{userstartup}\WSL USB Bridge"; Filename: "{app}\UsbBridge.exe"; Tasks: autostart

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "autostart"; Description: "Start automatically with Windows"; GroupDescription: "Startup:"

[Run]
Filename: "{app}\UsbBridge.exe"; Description: "Launch WSL USB Bridge"; Flags: nowait postinstall skipifsilent
