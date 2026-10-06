; GameNet Manager Server Setup

#define AppName "GameNet Manager Server"
#define AppPublisher "GameNet Manager"
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

[Setup]
AppId={{A4A2C3E7-13AA-4B3E-9D0B-123456789001}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\GameNet Manager\Server
DefaultGroupName=GameNet Manager
OutputBaseFilename=GameNetManager-Server-Setup-{#AppVersion}
OutputDir=..\..\artifacts\installer\out
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern dynamic
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Uninstallable=yes
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no

[Files]
Source: "..\..\artifacts\installer\server-publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\GameNet Manager Server Health"; Filename: "{cmd}"; Parameters: "/c start http://127.0.0.1:5080/api/health"

[Code]
var
  DataRootPage: TInputDirWizardPage;
  AdminPasswordPage: TInputQueryWizardPage;
  RegistrationToken: String;

function IsValidSecret(const Value: String; MinimumLength: Integer): Boolean;
begin
  Result := Length(Trim(Value)) >= MinimumLength;
end;

procedure InitializeWizard;
begin
  DataRootPage := CreateInputDirPage(wpSelectDir, 'مسیر داده‌های Server', 'محل نگهداری دیتابیس، DataProtection و Backup را انتخاب کنید.', 'این مسیر خارج از پوشه نصب Server نگه داشته می‌شود.', False, '');
  DataRootPage.Add('Data Root:');
  DataRootPage.Values[0] := ExpandConstant('{commonappdata}\GameNetManager');

  AdminPasswordPage := CreateInputQueryPage(DataRootPage.ID, 'رمز مدیر Server', 'رمز اولیه کاربر admin را تعیین کنید.', 'این رمز داخل فایل Setup ذخیره نمی‌شود و فقط در زمان نصب دریافت می‌شود.');
  AdminPasswordPage.Add('Admin password:', True);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = DataRootPage.ID then begin
    if Trim(DataRootPage.Values[0]) = '' then begin
      MsgBox('مسیر Data Root نمی‌تواند خالی باشد.', mbError, MB_OK);
      Result := False;
    end else if CompareText(
      Copy(
        AddBackslash(ExpandFileName(Trim(DataRootPage.Values[0]))),
        1,
        Length(AddBackslash(ExpandFileName(ExpandConstant('{app}'))))
      ),
      AddBackslash(ExpandFileName(ExpandConstant('{app}')))
    ) = 0 then begin
      MsgBox('مسیر Data Root نباید داخل پوشه نصب Server باشد.', mbError, MB_OK);
      Result := False;
    end;
  end else if CurPageID = AdminPasswordPage.ID then
    if not IsValidSecret(AdminPasswordPage.Values[0], 8) then begin MsgBox('رمز admin باید حداقل 8 کاراکتر باشد.', mbError, MB_OK); Result := False; end;
end;

procedure ConfigureServiceEnvironment(IncludeBootstrapPassword: Boolean);
var
  Key: String;
  Data: String;
begin
  Key := 'SYSTEM\CurrentControlSet\Services\GameNet Manager Server';
  Data :=
    'Agent__RegistrationToken=' + RegistrationToken + #0 +
    'GAMENET_DATA_ROOT=' + DataRootPage.Values[0] + #0;

  if IncludeBootstrapPassword then
    Data := Data + 'GAMENET_ADMIN_PASSWORD=' + AdminPasswordPage.Values[0] + #0;

  if not RegWriteMultiStringValue(HKLM, Key, 'Environment', Data) then
    RaiseException('ثبت Environment اختصاصی Windows Service شکست خورد.');
end;

procedure GenerateRegistrationToken;
var
  ResultCode: Integer;
  TokenPath: String;
  PowerShellPath: String;
begin
  if RegistrationToken <> '' then
    exit;

  TokenPath := ExpandConstant('{tmp}\gamenet-registration-token.txt');
  DeleteFile(TokenPath);
  PowerShellPath := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');

  if not Exec(
    PowerShellPath,
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "$token = [guid]::NewGuid().ToString(''N'') + [guid]::NewGuid().ToString(''N''); Set-Content -LiteralPath ''' + TokenPath + ''' -Value $token -NoNewline -Encoding ascii"',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) then
    RaiseException('تولید خودکار شناسه امن ثبت Agent انجام نشد.');

  if (ResultCode <> 0) or not FileExists(TokenPath) or not LoadStringFromFile(TokenPath, RegistrationToken) then
    RaiseException('تولید خودکار شناسه امن ثبت Agent ناموفق بود.');

  RegistrationToken := Trim(RegistrationToken);
  DeleteFile(TokenPath);

  if Length(RegistrationToken) < 32 then
    RaiseException('شناسه خودکار ثبت Agent طول کافی ندارد.');
end;

procedure GrantDataRootAccess;
var ResultCode: Integer;
begin
  if not Exec(ExpandConstant('{sys}\icacls.exe'), '"' + DataRootPage.Values[0] + '" /grant "NT AUTHORITY\NETWORK SERVICE":(OI)(CI)M /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then RaiseException('تنظیم ACL برای Data Root شکست خورد.');
  if ResultCode <> 0 then RaiseException('تنظیم ACL برای Data Root با کد ' + IntToStr(ResultCode) + ' شکست خورد.');
end;

procedure ConfigureFirewall;
var ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\netsh.exe'), 'advfirewall firewall delete rule name="GameNet Manager Server (TCP 5080)"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  if not Exec(ExpandConstant('{sys}\netsh.exe'), 'advfirewall firewall add rule name="GameNet Manager Server (TCP 5080)" dir=in action=allow protocol=TCP localport=5080 profile=private', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then RaiseException('ساخت Rule فایروال شکست خورد.');
  if ResultCode <> 0 then RaiseException('Rule فایروال با کد ' + IntToStr(ResultCode) + ' شکست خورد.');
end;

procedure RegisterServerService;
var ResultCode: Integer; BinPath: String;
begin
  BinPath := '"' + ExpandConstant('{app}\GameNetManager.Server.exe') + '"';
  Exec(ExpandConstant('{sys}\sc.exe'), 'stop "GameNet Manager Server"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\sc.exe'), 'delete "GameNet Manager Server"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  if not Exec(ExpandConstant('{sys}\sc.exe'), 'create "GameNet Manager Server" binPath= ' + BinPath + ' start= auto obj= "NT AUTHORITY\NetworkService" DisplayName= "GameNet Manager Server"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then RaiseException('ساخت Windows Service شکست خورد.');
  if ResultCode <> 0 then RaiseException('ساخت Windows Service با کد ' + IntToStr(ResultCode) + ' شکست خورد.');
  Exec(ExpandConstant('{sys}\sc.exe'), 'description "GameNet Manager Server" "GameNet Manager Server - Local LAN authoritative service"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Exec(ExpandConstant('{sys}\sc.exe'), 'failure "GameNet Manager Server" reset= 86400 actions= restart/5000/restart/10000/restart/30000', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

procedure StartServerService;
var ResultCode: Integer;
begin
  if not Exec(ExpandConstant('{sys}\sc.exe'), 'start "GameNet Manager Server"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then RaiseException('شروع Windows Service شکست خورد.');
  if ResultCode <> 0 then RaiseException('Windows Service با کد ' + IntToStr(ResultCode) + ' شروع نشد.');
end;

procedure WaitForServerHealth;
var
  ResultCode: Integer;
begin
  if not Exec(
    ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "$deadline=(Get-Date).AddSeconds(45); do { try { if ((Invoke-WebRequest -UseBasicParsing -Uri ''http://127.0.0.1:5080/api/health'' -TimeoutSec 2).StatusCode -eq 200) { exit 0 } } catch {} ; Start-Sleep -Seconds 1 } while ((Get-Date) -lt $deadline); exit 1"',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) then
    RaiseException('بررسی سلامت Server انجام نشد.');

  if ResultCode <> 0 then
    RaiseException('Server روی TCP 5080 بعد از نصب آماده نشد؛ رمز bootstrap برای بررسی باقی می‌ماند.');
end;

procedure RemoveBootstrapAdminPassword;
begin
  ConfigureServiceEnvironment(False);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    ForceDirectories(DataRootPage.Values[0]);
    GenerateRegistrationToken;
    ConfigureServiceEnvironment(True);
    GrantDataRootAccess;
    ConfigureFirewall;
    RegisterServerService;
    StartServerService;
    WaitForServerHealth;
    RemoveBootstrapAdminPassword;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then begin
    Exec(ExpandConstant('{sys}\sc.exe'), 'stop "GameNet Manager Server"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec(ExpandConstant('{sys}\sc.exe'), 'delete "GameNet Manager Server"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec(ExpandConstant('{sys}\netsh.exe'), 'advfirewall firewall delete rule name="GameNet Manager Server (TCP 5080)"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    RegDeleteValue(HKLM, 'SYSTEM\CurrentControlSet\Services\GameNet Manager Server', 'Environment');
  end;
end;