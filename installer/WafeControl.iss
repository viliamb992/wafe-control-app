; Inno Setup 7 script for the Windows app: installs the self-contained publish output to Program Files
; (or a per-user / custom folder), with Start menu entry and uninstaller.
;
; Built by .github/workflows/release.yml. Locally, after `dotnet publish ... -o publish`:
;   iscc /DAppVersion=1.2.0 /DFileVersion=1.2.0.0 /DArch=x64 /DSourceDir=..\publish installer\WafeControl.iss

#ifndef AppVersion
  #define AppVersion "0.0.0-dev"
#endif
#ifndef FileVersion
  #define FileVersion "0.0.0.0"
#endif
; x64 or arm64
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif

#define AppName "WAFE Control"
#define AppExe "WafeControl.WinUI.exe"
#define RepoUrl "https://github.com/viliamb992/wafe-control-app"
; Must match RegistryStartupRegistration.ValueName.
#define StartupValueName "WafeControl"

[Setup]
; Identifies the app across versions (upgrades, uninstall). Never change it.
AppId={{B27FABBD-CA2A-418D-88DC-96328CDD8B1D}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Viliam Birmon
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}/issues
AppUpdatesURL={#RepoUrl}/releases
VersionInfoVersion={#FileVersion}
DefaultDirName={autopf}\WAFE Control
DisableProgramGroupPage=yes
; Program Files for all users by default; the user can choose a per-user install (no admin) instead.
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog commandline
; The app writes its Start with Windows entry to HKCU; uninstall removes it (see [Registry]).
UsedUserAreasWarning=no
; Windows 11 only.
MinVersion=10.0.22000
; A 64-bit setup.exe (Inno Setup 7); on ARM64 it runs under Windows 11's x64 emulation.
SetupArchitecture=x64
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
OutputBaseFilename=WafeControl-{#AppVersion}-win-{#Arch}-setup
SetupIconFile=..\src\WafeControl.WinUI\Assets\app-icon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
; Close the running app (it lives in the tray) before replacing its files.
CloseApplications=force

[Languages]
Name: "cs"; MessagesFile: "compiler:Languages\Czech.isl"
Name: "sk"; MessagesFile: "compiler:Languages\Slovak.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
; Left by versions up to 1.0.1 (RecuperationSystem.*, "Wafe Recuperation Controller"); an upgrade keeps their folder.
Type: files; Name: "{app}\RecuperationSystem.*"
Type: files; Name: "{app}\cs\RecuperationSystem.*"
Type: files; Name: "{app}\sk\RecuperationSystem.*"
Type: files; Name: "{app}\en\RecuperationSystem.*"
Type: files; Name: "{autoprograms}\Wafe Recuperation Controller.lnk"
Type: files; Name: "{autodesktop}\Wafe Recuperation Controller.lnk"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Created by the app, not by setup; only removed on uninstall.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "{#StartupValueName}"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"; ValueType: none; ValueName: "{#StartupValueName}"; Flags: dontcreatekey uninsdeletevalue
; The entry's name up to 1.0.1; the app renames it on its first start after the upgrade.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "WafeRecuperation"; Flags: dontcreatekey uninsdeletevalue
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run"; ValueType: none; ValueName: "WafeRecuperation"; Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; The tray app keeps running after its window closes; stop it so its files can be removed.
; Only the copy installed here: other copies (e.g. a dev build) share the exe name.
Filename: "powershell.exe"; Parameters: "-NoProfile -NonInteractive -Command ""Get-Process -Name '{#RemoveFileExt(AppExe)}' -ErrorAction SilentlyContinue | Where-Object {{ $_.Path -and $_.Path.StartsWith('{app}\', 'OrdinalIgnoreCase') } | Stop-Process -Force"""; Flags: runhidden; RunOnceId: "StopApp"
