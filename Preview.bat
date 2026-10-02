@echo off
setlocal EnableExtensions

set "SOURCE=%~dp0"
set "PREVIEW=C:\GameNetManager-Preview"
set "SERVER_PROJECT=%PREVIEW%\src\Server\GameNetManager.Server.csproj"
set "DASHBOARD=%PREVIEW%\src\Dashboard"

echo.
echo ==========================================
echo      GameNet Manager - Local Preview
echo ==========================================
echo.
echo Source : %SOURCE%
echo Preview: %PREVIEW%
echo.

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

if not exist "%SOURCE%src\Server\GameNetManager.Server.csproj" (
  echo [ERROR] Server project not found in source:
  echo %SOURCE%src\Server\GameNetManager.Server.csproj
  pause
  exit /b 1
)

if not exist "%SOURCE%src\Dashboard\package.json" (
  echo [ERROR] Dashboard package.json not found in source:
  echo %SOURCE%src\Dashboard\package.json
  pause
  exit /b 1
)

echo [INFO] Syncing source to isolated preview folder...
if not exist "%PREVIEW%" mkdir "%PREVIEW%"

robocopy "%SOURCE%" "%PREVIEW%" /MIR /XD ".git" "bin" "obj" "node_modules" "App_Data" >nul
set "ROBOCODE=%ERRORLEVEL%"
if %ROBOCODE% GEQ 8 (
  echo [ERROR] Preview sync failed. Robocopy code: %ROBOCODE%
  pause
  exit /b 1
)

if not exist "%DASHBOARD%\node_modules" (
  echo [INFO] Dashboard dependencies are missing. Installing...
  pushd "%DASHBOARD%"
  call npm ci
  if errorlevel 1 (
    popd
    echo [ERROR] npm ci failed.
    pause
    exit /b 1
  )
  popd
)

echo [INFO] Starting Server on http://localhost:5080 ...
start "GameNet Server" /D "%PREVIEW%\src\Server" dotnet run --project "%SERVER_PROJECT%"

echo [INFO] Starting Dashboard on http://localhost:5173 ...
start "GameNet Dashboard" /D "%DASHBOARD%" npm run dev -- --host 0.0.0.0

echo [INFO] Waiting for Server...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ok=$false; 1..60 | %% { if (Test-NetConnection 127.0.0.1 -Port 5080 -InformationLevel Quiet) { $ok=$true; break }; Start-Sleep -Milliseconds 500 }; if (-not $ok) { exit 1 }"
if errorlevel 1 (
  echo [ERROR] Server did not become ready on port 5080.
  pause
  exit /b 1
)

echo [INFO] Waiting for Dashboard...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ok=$false; 1..60 | %% { if (Test-NetConnection 127.0.0.1 -Port 5173 -InformationLevel Quiet) { $ok=$true; break }; Start-Sleep -Milliseconds 500 }; if (-not $ok) { exit /b 1 }"
if errorlevel 1 (
  echo [ERROR] Dashboard did not become ready on port 5173.
  pause
  exit /b 1
)

echo.
echo [OK] Preview is ready:
echo      http://localhost:5173/
echo      http://localhost:5080/api/health
echo.
start "" "http://localhost:5173/"
echo [OK] Browser opened.
echo.
pause
