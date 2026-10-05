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
  RegistrationTokenPage: TInputQueryWizardPage;

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

  RegistrationTokenPage := CreateInputQueryPage(AdminPasswordPage.ID, 'توکن ثبت Agent', 'یک توکن حداقل 16 کاراکتری برای ثبت Client Agentها وارد کنید.', 'همین مقدار را برای Client Setupهای این Server استفاده کنید.');
  RegistrationTokenPage.Add('Agent registration token:', False);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = DataRootPage.ID then
    if Trim(DataRootPage.Values[0]) = '' then begin MsgBox('مسیر Data Root نمی‌تواند خالی باشد.', mbError, MB_OK); Result := False; end
  else if CurPageID = AdminPasswordPage.ID then
    if not IsValidSecret(AdminPasswordPage.Values[0], 8) then begin MsgBox('رمز admin باید حداقل 8 کاراکتر باشد.', mbError, MB_OK); Result := False; end
  else if CurPageID = RegistrationTokenPage.ID then
    if not IsValidSecret(RegistrationTokenPage.Values[0], 16) then begin MsgBox('توکن Agent باید حداقل 16 کاراکتر باشد.', mbError, MB_OK); Result := False; end;
end;

procedure ConfigureMachineEnvironment;
var
  Key: String;
begin
  Key := 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';
  if not RegWriteStringValue(HKLM, Key, 'GAMENET_ADMIN_PASSWORD', AdminPasswordPage.Values[0]) then RaiseException('ثبت GAMENET_ADMIN_PASSWORD شکست خورد.');
  if not RegWriteStringValue(HKLM, Key, 'Agent__RegistrationToken', RegistrationTokenPage.Values[0]) then RaiseException('ثبت Agent registration token شکست خورد.');
  if not RegWriteStringValue(HKLM, Key, 'GAMENET_DATA_ROOT', DataRootPage.Values[0]) then RaiseException('ثبت GAMENET_DATA_ROOT شکست خورد.');
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

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    ForceDirectories(DataRootPage.Values[0]);
    ConfigureMachineEnvironment;
    GrantDataRootAccess;
    ConfigureFirewall;
    RegisterServerService;
    StartServerService;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var ResultCode: Integer;
begin
  if CurUninstallStep = usUninstall then begin
    Exec(ExpandConstant('{sys}\sc.exe'), 'stop "GameNet Manager Server"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec(ExpandConstant('{sys}\sc.exe'), 'delete "GameNet Manager Server"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec(ExpandConstant('{sys}\netsh.exe'), 'advfirewall firewall delete rule name="GameNet Manager Server (TCP 5080)"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    RegDeleteValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'GAMENET_ADMIN_PASSWORD');
    RegDeleteValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Agent__RegistrationToken');
    RegDeleteValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'GAMENET_DATA_ROOT');
  end;
end;