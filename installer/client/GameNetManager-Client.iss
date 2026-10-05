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
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: string; ValueName: "GAMENET_AGENT_REGISTRATION_TOKEN"; ValueData: "{code:GetRegistrationToken}"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: string; ValueName: "GAMENET_AGENT_NAME"; ValueData: "{code:GetAgentName}"; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "GameNetManagerAgent"; ValueData: """{app}\GameNetManager.Client.exe"""; Flags: uninsdeletevalue

[Code]
var
  ServerPage: TInputQueryWizardPage;
  TokenPage: TInputQueryWizardPage;
  NamePage: TInputQueryWizardPage;

procedure InitializeWizard;
begin
  ServerPage := CreateInputQueryPage(wpSelectDir, 'Server', 'آدرس Server گیم‌نت را وارد کنید.', 'برای نمونه: http://192.168.0.9:5080');
  ServerPage.Add('Server URL:', False);
  ServerPage.Values[0] := 'http://192.168.0.9:5080';

  TokenPage := CreateInputQueryPage(ServerPage.ID, 'Agent registration token', 'توکن ثبت Agent را که هنگام نصب Server تعیین کردید وارد کنید.', 'این مقدار برای ثبت اولیه Agent لازم است.');
  TokenPage.Add('Registration token:', False);

  NamePage := CreateInputQueryPage(TokenPage.ID, 'نام دستگاه', 'نامی که در Dashboard برای این Agent دیده می‌شود.', '');
  NamePage.Add('Agent name:', False);
  NamePage.Values[0] := GetComputerNameString;
end;

function GetServerUrl(Param: String): String;
begin Result := Trim(ServerPage.Values[0]); end;

function GetRegistrationToken(Param: String): String;
begin Result := Trim(TokenPage.Values[0]); end;

function GetAgentName(Param: String): String;
begin Result := Trim(NamePage.Values[0]); end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = ServerPage.ID then begin
    if Pos('http://', Lowercase(Trim(ServerPage.Values[0]))) <> 1 then begin
      MsgBox('فعلاً Server URL باید با http:// شروع شود.', mbError, MB_OK);
      Result := False;
    end;
  end else if CurPageID = TokenPage.ID then begin
    if Length(Trim(TokenPage.Values[0])) < 16 then begin
      MsgBox('توکن Agent باید حداقل 16 کاراکتر باشد.', mbError, MB_OK);
      Result := False;
    end;
  end else if CurPageID = NamePage.ID then begin
    if Trim(NamePage.Values[0]) = '' then begin
      MsgBox('نام Agent نمی‌تواند خالی باشد.', mbError, MB_OK);
      Result := False;
    end;
  end;
end;