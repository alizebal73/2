@echo off
setlocal EnableExtensions

set "PREVIEW=C:\GameNetManager-Preview"
set "APPDATA_DIR=%PREVIEW%\src\Server\App_Data"

echo.
echo ==========================================
echo   GameNet Manager - Reset Preview DB
echo ==========================================
echo.
echo This resets ONLY the isolated Preview database:
echo   %APPDATA_DIR%
echo.
echo It does NOT touch any production/shop database.
echo.

if not exist "%APPDATA_DIR%" (
  echo [OK] Preview App_Data does not exist. Nothing to reset.
  pause
  exit /b 0
)

choice /C YN /M "Reset the Preview database now"
if errorlevel 2 (
  echo [CANCELLED] No changes made.
  pause
  exit /b 0
)

rmdir /s /q "%APPDATA_DIR%"
if exist "%APPDATA_DIR%" (
  echo [ERROR] Could not remove Preview App_Data.
  echo Close any GameNet Server process and try again.
  pause
  exit /b 1
)

echo.
echo [OK] Preview database reset successfully.
echo Run GameNet-Preview.bat now.
echo.
pause
