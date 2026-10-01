@echo off
setlocal EnableExtensions

set "REPO_ZIP=https://github.com/alizebal73/2/archive/refs/heads/main.zip"
set "PREVIEW=C:\GameNetManager-Preview"
set "DOWNLOAD=%TEMP%\gamenet-manager-main.zip"
set "EXTRACT=%TEMP%\gamenet-manager-main"
set "SOURCE="

echo.
echo ==========================================
echo   GameNet Manager - One Click Preview
echo ==========================================
echo.

where powershell >nul 2>nul
if errorlevel 1 (
  echo [ERROR] Windows PowerShell not found.
  pause
  exit /b 1
)

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] dotnet not found in PATH.
  pause
  exit /b 1
)

where npm >nul 2>nul
if errorlevel 1 (
  echo [ERROR] npm not found in PATH.
  pause
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=Get-NetTCPConnection -LocalPort 5080 -State Listen -ErrorAction SilentlyContinue; if($p){Write-Host '[ERROR] Port 5080 is already in use. Close the previous GameNet Server window first.'; exit 1}"
if errorlevel 1 (
  pause
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "$p=Get-NetTCPConnection -LocalPort 5173 -State Listen -ErrorAction SilentlyContinue; if($p){Write-Host '[ERROR] Port 5173 is already in use. Close the previous GameNet Dashboard window first.'; exit 1}"
if errorlevel 1 (
  pause
  exit /b 1
)

echo [1/5] Downloading latest main...
if exist "%DOWNLOAD%" del /q "%DOWNLOAD%" >nul 2>nul
if exist "%EXTRACT%" rmdir /s /q "%EXTRACT%" >nul 2>nul

powershell -NoProfile -ExecutionPolicy Bypass -Command "Invoke-WebRequest -UseBasicParsing '%REPO_ZIP%' -OutFile '%DOWNLOAD%'"
if errorlevel 1 (
  echo [ERROR] Download failed.
  pause
  exit /b 1
)

echo [2/5] Extracting...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Expand-Archive -Force '%DOWNLOAD%' '%EXTRACT%'"
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
  call npm ci
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

echo [5/5] Starting Server and Dashboard...
start "GameNet Server" cmd /k "cd /d ""%PREVIEW%\src\Server"" && dotnet run --project ""%PREVIEW%\src\Server\GameNetManager.Server.csproj"""
start "GameNet Dashboard" cmd /k "cd /d ""%PREVIEW%\src\Dashboard"" && npm run dev -- --host 0.0.0.0"

echo Waiting for services...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ok=$false; 1..60 | %% { if (Test-NetConnection 127.0.0.1 -Port 5080 -InformationLevel Quiet) { $ok=$true; break }; Start-Sleep -Milliseconds 500 }; if (-not $ok) { exit 1 }"
if errorlevel 1 (
  echo [ERROR] Server did not become ready on port 5080.
  pause
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "$ok=$false; 1..60 | %% { if (Test-NetConnection 127.0.0.1 -Port 5173 -InformationLevel Quiet) { $ok=$true; break }; Start-Sleep -Milliseconds 500 }; if (-not $ok) { exit 1 }"
if errorlevel 1 (
  echo [ERROR] Dashboard did not become ready on port 5173.
  pause
  exit /b 1
)

echo.
echo ==========================================
echo [OK] GameNet Manager Preview is ready.
echo     http://localhost:5173/
echo ==========================================
start "" "http://localhost:5173/"
echo.
pause
