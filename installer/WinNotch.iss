; WinNotch Inno Setup Script
; Requires Inno Setup 6.x (https://jrsoftware.org/isinfo.php)

#define MyAppName "WinNotch"
#define MyAppVersion "0.3.9"
#define MyAppPublisher "N3uralCreativity"
#define MyAppURL "https://github.com/N3uralCreativity/WinNotch"
#define MyAppExeName "WinNotch.exe"

[Setup]
AppId={{B8F3A2D1-7E4C-4F5A-9D6B-1C2E3F4A5B6C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=..\publish\installer
OutputBaseFilename=WinNotch-{#MyAppVersion}-Setup
SetupIconFile=..\src\WinNotch\Assets\Icons\WinNotch.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
DisableProgramGroupPage=yes
; Upgrade support: use previous install dir, close running app
UsePreviousAppDir=yes
CloseApplications=yes
RestartApplications=yes
CloseApplicationsFilter=WinNotch.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupentry"; Description: "Launch WinNotch at Windows startup"; GroupDescription: "System:"

[Files]
Source: "..\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startupentry

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{localappdata}\WinNotch\*"
Type: dirifempty; Name: "{localappdata}\WinNotch"

[Code]

procedure KillRunningProcess();
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM {#MyAppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  // Small wait to ensure process is fully terminated
  Sleep(500);
end;

procedure InitializeUninstallProgressForm();
begin
  // Kill WinNotch before uninstalling
  KillRunningProcess();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Always remove startup registry entry
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', '{#MyAppName}');

    // Ask user if they want to delete all data
    if MsgBox('Do you want to delete all WinNotch data?' + #13#10 + #13#10 +
              'This includes plugins, settings, logs, and all user data.' + #13#10 +
              '(Located in: ' + ExpandConstant('{userappdata}\WinNotch') + ')',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      // Delete %AppData%\WinNotch (plugins, settings, logs)
      AppDataDir := ExpandConstant('{userappdata}\WinNotch');
      if DirExists(AppDataDir) then
        DelTree(AppDataDir, True, True, True);

      // Delete %LocalAppData%\WinNotch
      AppDataDir := ExpandConstant('{localappdata}\WinNotch');
      if DirExists(AppDataDir) then
        DelTree(AppDataDir, True, True, True);
    end;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  // Kill running process before installing/upgrading
  KillRunningProcess();
  Result := '';
end;
function GetUninstallString(): String;
var
  sUnInstPath: String;
  sUnInstallString: String;
begin
  sUnInstPath := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1';
  sUnInstallString := '';
  RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString);
  if sUnInstallString = '' then
    RegQueryStringValue(HKLM, sUnInstPath, 'UninstallString', sUnInstallString);
  Result := sUnInstallString;
end;

function GetInstalledVersion(): String;
var
  sUnInstPath: String;
  sVersion: String;
begin
  sUnInstPath := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}_is1';
  sVersion := '';
  RegQueryStringValue(HKCU, sUnInstPath, 'DisplayVersion', sVersion);
  if sVersion = '' then
    RegQueryStringValue(HKLM, sUnInstPath, 'DisplayVersion', sVersion);
  Result := sVersion;
end;

function IsAppInstalled(): Boolean;
begin
  Result := GetUninstallString() <> '';
end;

function InitializeSetup(): Boolean;
var
  InstalledVersion: String;
  UninstallString: String;
  ResultCode: Integer;
  Msg: Integer;
begin
  Result := True;

  if IsAppInstalled() then
  begin
    InstalledVersion := GetInstalledVersion();

    if InstalledVersion = '{#MyAppVersion}' then
    begin
      // Same version: offer repair or uninstall
      Msg := MsgBox('WinNotch v' + InstalledVersion + ' is already installed.' + #13#10 + #13#10 +
                     'Click YES to repair (reinstall).' + #13#10 +
                     'Click NO to uninstall it.' + #13#10 +
                     'Click CANCEL to abort.',
                     mbConfirmation, MB_YESNOCANCEL);

      if Msg = IDYES then
      begin
        // Repair = continue with install (overwrites files)
        Result := True;
      end
      else if Msg = IDNO then
      begin
        // Uninstall
        UninstallString := RemoveQuotes(GetUninstallString());
        Exec(UninstallString, '/SILENT', '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
        Result := False; // exit installer after uninstall
      end
      else
      begin
        Result := False; // cancel
      end;
    end
    else
    begin
      // Different version: offer upgrade
      Msg := MsgBox('WinNotch v' + InstalledVersion + ' is currently installed.' + #13#10 + #13#10 +
                     'Do you want to upgrade to v{#MyAppVersion}?',
                     mbConfirmation, MB_YESNO);

      if Msg = IDYES then
      begin
        // Silently uninstall old version first, then install new
        UninstallString := RemoveQuotes(GetUninstallString());
        Exec(UninstallString, '/SILENT /NORESTART', '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
        Result := True;
      end
      else
      begin
        Result := False;
      end;
    end;
  end;
end;
