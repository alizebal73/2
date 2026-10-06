; GameNet Manager Client / Agent Setup

#define AppName "GameNet Manager Client"
#define AppPublisher "GameNet Manager"
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

[Setup]
AppId={{C7B15E8D-2D02-4B74-9F4A-123456789002}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\GameNet Manager\Client
DefaultGroupName=GameNet Manager
OutputBaseFilename=GameNetManager-Client-Setup-{#AppVersion}
OutputDir=..\..\artifacts\installer\out
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern dynamic
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Uninstallable=yes
DisableProgramGroupPage=yes

[Files]
Source: "..\..\artifacts\installer\client-publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\GameNet Manager Client"; Filename: "{app}\GameNetManager.Client.exe"

[Registry]
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: string; ValueName: "GAMENET_SERVER_URL"; ValueData: "{code:GetServerUrl}"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: string; ValueName: "GAMENET_AGENT_PAIRING_CODE"; ValueData: "{code:GetPairingCode}"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: string; ValueName: "GAMENET_AGENT_NAME"; ValueData: "{code:GetAgentName}"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: string; ValueName: "GAMENET_AGENT_DATA_DIR"; ValueData: "{commonappdata}\GameNetManager\Agent"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "GameNetManagerAgent"; ValueData: """{app}\GameNetManager.Client.exe"""; Flags: uninsdeletevalue

[Code]
var
  ServerPage: TInputQueryWizardPage;
  PairingPage: TInputQueryWizardPage;
  NamePage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  ServerPage := CreateInputQueryPage(wpSelectDir, 'Server', 'آدرس Server گیم‌نت را وارد کنید.', 'برای نمونه: http://192.168.0.9:5080');
  ServerPage.Add('Server URL:', False);
  ServerPage.Values[0] := 'http://192.168.0.9:5080';

  PairingPage := CreateInputQueryPage(ServerPage.ID, 'کد اتصال Agent', 'کد موقت را از Server → کلاینت‌ها → ساخت کد اتصال دریافت کنید.', 'کد اتصال برای ثبت اولیه همین Agent استفاده می‌شود و رمز داخلی Server نیست.');
  PairingPage.Add('Pairing code:', False);

  NamePage := CreateInputQueryPage(PairingPage.ID, 'نام دستگاه', 'نامی که در Dashboard برای این Agent دیده می‌شود.', '');
  NamePage.Add('Agent name:', False);
  NamePage.Values[0] := GetComputerNameString;
end;

function GetServerUrl(Param: String): String;
begin Result := Trim(ServerPage.Values[0]); end;

function GetPairingCode(Param: String): String;
begin Result := Trim(PairingPage.Values[0]); end;

function GetAgentName(Param: String): String;
begin Result := Trim(NamePage.Values[0]); end;

procedure GrantAgentDataAccess;
var
  ResultCode: Integer;
  DataDir: String;
begin
  DataDir := ExpandConstant('{commonappdata}\GameNetManager\Agent');
  if not ForceDirectories(DataDir) then
    RaiseException('ساخت پوشه داده Agent شکست خورد.');
  if not Exec(
    ExpandConstant('{sys}\icacls.exe'),
    '"' + DataDir + '" /grant "*S-1-5-32-545":(OI)(CI)M /T',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) then
    RaiseException('تنظیم ACL داده Agent شکست خورد.');
  if ResultCode <> 0 then
    RaiseException('تنظیم ACL داده Agent با کد ' + IntToStr(ResultCode) + ' شکست خورد.');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    GrantAgentDataAccess;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = ServerPage.ID then begin
    if Pos('http://', Lowercase(Trim(ServerPage.Values[0]))) <> 1 then begin
      MsgBox('فعلاً Server URL باید با http:// شروع شود.', mbError, MB_OK);
      Result := False;
    end;
  end else if CurPageID = PairingPage.ID then begin
    if (Length(Trim(PairingPage.Values[0])) <> 6)
      or (StrToIntDef(Trim(PairingPage.Values[0]), -1) < 0) then begin
      MsgBox('کد اتصال باید یک عدد ۶ رقمی باشد.', mbError, MB_OK);
      Result := False;
    end;
  end else if CurPageID = NamePage.ID then begin
    if Trim(NamePage.Values[0]) = '' then begin
      MsgBox('نام Agent نمی‌تواند خالی باشد.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;