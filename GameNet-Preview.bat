@echo off
setlocal EnableExtensions

set "REPO_ZIP=https://github.com/alizebal73/2/archive/refs/heads/main.zip"
set "PREVIEW=C:\GameNetManager-Preview"
set "DOWNLOAD=%TEMP%\gamenet-manager-main.zip"
set "EXTRACT=%TEMP%\gamenet-manager-main"
set "SOURCE="
set "DOTNET_EXE="
set "NPM_EXE="
set "SERVER_READY=0"

echo.
echo ==========================================
echo   GameNet Manager - One Click Preview
echo ==========================================
echo.

set "PS=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
if not exist "%PS%" (
  echo [ERROR] Windows PowerShell not found.
  pause
  exit /b 1
)

rem Resolve .NET.
if exist "C:\dotnet\dotnet.exe" set "DOTNET_EXE=C:\dotnet\dotnet.exe"
if not defined DOTNET_EXE if exist "%ProgramFiles%\dotnet\dotnet.exe" set "DOTNET_EXE=%ProgramFiles%\dotnet\dotnet.exe"
if not defined DOTNET_EXE (
  echo [ERROR] .NET SDK not found.
  echo Checked:
  echo   C:\dotnet\dotnet.exe
  echo   %ProgramFiles%\dotnet\dotnet.exe
  pause
  exit /b 1
)
for %%D in ("%DOTNET_EXE%") do set "PATH=%%~dpD;%PATH%"

rem Resolve npm/node from the current Windows environment first.
for /f "delims=" %%N in ('where npm.cmd 2^>nul') do (
  if not defined NPM_EXE set "NPM_EXE=%%N"
)

if not defined NPM_EXE if exist "%ProgramFiles%\nodejs\npm.cmd" set "NPM_EXE=%ProgramFiles%\nodejs\npm.cmd"
if not defined NPM_EXE if exist "%APPDATA%\npm\npm.cmd" set "NPM_EXE=%APPDATA%\npm\npm.cmd"

rem Fallback for the self-hosted runner tool cache, without assuming a runner folder name.
if not defined NPM_EXE for /d %%R in ("C:\actions-runner*" ) do (
  if exist "%%~fR\_work\_tool\node" (
    for /f "delims=" %%V in ('dir /b /ad /o-n "%%~fR\_work\_tool\node" 2^>nul') do (
      if not defined NPM_EXE if exist "%%~fR\_work\_tool\node\%%V\x64\npm.cmd" set "NPM_EXE=%%~fR\_work\_tool\node\%%V\x64\npm.cmd"
    )
  )
)

if not defined NPM_EXE (
  echo [ERROR] npm was not found.
  echo Checked:
  echo   %ProgramFiles%\nodejs\npm.cmd
  echo   %APPDATA%\npm\npm.cmd
  echo   PATH / Program Files / AppData / actions-runner* tool cache
  pause
  exit /b 1
)
for %%N in ("%NPM_EXE%") do set "NODE_HOME=%%~dpN"
set "PATH=%NODE_HOME%;%PATH%"

echo [OK] .NET: %DOTNET_EXE%
echo [OK] npm  : %NPM_EXE%

echo [1/5] Downloading latest main...
if exist "%DOWNLOAD%" del /q "%DOWNLOAD%" >nul 2>nul
if exist "%EXTRACT%" rmdir /s /q "%EXTRACT%" >nul 2>nul

"%PS%" -NoProfile -ExecutionPolicy Bypass -Command "Invoke-WebRequest -UseBasicParsing '%REPO_ZIP%' -OutFile '%DOWNLOAD%'"
if errorlevel 1 (
  echo [ERROR] Download failed.
  pause
  exit /b 1
)

echo [2/5] Extracting...
"%PS%" -NoProfile -ExecutionPolicy Bypass -Command "Expand-Archive -Force '%DOWNLOAD%' '%EXTRACT%'"
if errorlevel 1 (
  echo [ERROR] Extraction failed.
  pause
  exit /b 1
)

for /d %%D in ("%EXTRACT%\*") do set "SOURCE=%%~fD"

if not exist "%SOURCE%\src\Server\GameNetManager.Server.csproj" (
  echo [ERROR] Server project not found after extraction.
  pause
  exit /b 1
)

if not exist "%SOURCE%\src\Dashboard\package.json" (
  echo [ERROR] Dashboard package.json not found after extraction.
  pause
  exit /b 1
)

echo [3/5] Syncing to %PREVIEW%...
if not exist "%PREVIEW%" mkdir "%PREVIEW%"
robocopy "%SOURCE%" "%PREVIEW%" /MIR /XD ".git" "bin" "obj" "node_modules" "App_Data" >nul
set "ROBOCODE=%ERRORLEVEL%"
if %ROBOCODE% GEQ 8 (
  echo [ERROR] Preview sync failed. Robocopy code: %ROBOCODE%
  pause
  exit /b 1
)

if not exist "%PREVIEW%\src\Dashboard\node_modules" (
  echo [4/5] Installing Dashboard dependencies...
  pushd "%PREVIEW%\src\Dashboard"
  call "%NPM_EXE%" ci
  if errorlevel 1 (
    popd
    echo [ERROR] npm ci failed.
    pause
    exit /b 1
  )
  popd
) else (
  echo [4/5] Dashboard dependencies already installed.
)

echo [5/5] Cleaning old Preview processes and starting Server and Dashboard...
"%PS%" -NoProfile -ExecutionPolicy Bypass -Command "$ports=@(5173,5080); foreach($port in $ports){ Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique | ForEach-Object { if($_ -and $_ -ne $PID){ Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue } } }"
start "GameNet Server" /D "%PREVIEW%\src\Server" "%DOTNET_EXE%" run --urls "http://127.0.0.1:5080" --project "%PREVIEW%\src\Server\GameNetManager.Server.csproj"
start "GameNet Dashboard" /D "%PREVIEW%\src\Dashboard" "%NPM_EXE%" run dev -- --host 0.0.0.0 --port 5173

echo Waiting for Dashboard and Server...
"%PS%" -NoProfile -ExecutionPolicy Bypass -Command "$ok=$false; 1..60 | %% { try { $r=Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:5173/' -TimeoutSec 2; if($r.StatusCode -eq 200){$ok=$true;break} } catch {}; Start-Sleep -Milliseconds 500 }; if (-not $ok) { exit 1 }"
if errorlevel 1 (
  echo [ERROR] Dashboard did not become ready on port 5173.
  pause
  exit /b 1
)

"%PS%" -NoProfile -ExecutionPolicy Bypass -Command "$ok=$false; 1..60 | %% { try { $r=Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:5080/api/health' -TimeoutSec 2; if($r.StatusCode -eq 200){$ok=$true;break} } catch {}; if ($_ -eq 60) { exit 1 }; Start-Sleep -Milliseconds 500 }"
if errorlevel 1 (
  echo [WARN] Server is not ready on port 5080.
  echo [WARN] Dashboard will still open, but live API and SignalR data are unavailable.
) else (
  set "SERVER_READY=1"
)

echo.
echo ==========================================
if "%SERVER_READY%"=="1" (
  echo [OK] GameNet Manager Preview is ready.
  echo     Server:   http://localhost:5080
  echo     Dashboard: http://localhost:5173/
) else (
  echo [OK] Dashboard Preview is ready.
  echo     http://localhost:5173/
  echo [WARN] Server/API is unavailable. Check the Server window for the error.
)
echo ==========================================
start "" "http://localhost:5173/"
echo.
pause
