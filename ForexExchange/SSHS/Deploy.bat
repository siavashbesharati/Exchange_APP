@echo off
setlocal ENABLEDELAYEDEXPANSION
title 🚀 Micro Deploy Taban (.NET 9.0 - Fixed)

:: === CONFIG ===
set SERVER=root@104.234.46.151
set APP_DIR=/var/www/taban
set BACKUP_DIR=/var/www/Taban_backUp
set SERVICE=taban.service

:: Go one folder up from sshs to project root
pushd "%~dp0.."
set PROJECT_PATH=%CD%\
set FRAMEWORK=net9.0
set LOCAL_PUBLISH_DIR=%PROJECT_PATH%bin\Release\%FRAMEWORK%\publish

:: === Timestamp ===
for /f "tokens=1-4 delims=/ " %%a in ("%date%") do (
    set YYYY=%%d
    set MM=%%b
    set DD=%%c
)
for /f "tokens=1-4 delims=:." %%a in ("%time%") do (
    set HH=%%a
    set MN=%%b
    set SS=%%c
)
set HH=%HH: =0%
set DATETIME=%YYYY%-%MM%-%DD%_%HH%-%MN%-%SS%

echo =============================================
echo 🧱 Publishing project (Release, %FRAMEWORK%)
echo =============================================

dotnet publish -c Release
if errorlevel 1 (
    echo ❌ Build failed.
    popd
    exit /b 1
)

echo =============================================
echo 💾 Backing up current DLLs on server
echo =============================================
ssh -o ConnectTimeout=10 -o BatchMode=yes %SERVER% "mkdir -p %BACKUP_DIR%/bin_%DATETIME%; cp -f %APP_DIR%/ForexExchange.dll %APP_DIR%/ForexExchange.pdb %APP_DIR%/ForexExchange.deps.json %BACKUP_DIR%/bin_%DATETIME%/ 2>/dev/null || echo '(missing files, skipping)'"

echo =============================================
echo 🚦 Stopping service %SERVICE%
echo =============================================
ssh -o ConnectTimeout=10 -o BatchMode=yes %SERVER% "systemctl stop %SERVICE%"

echo =============================================
echo 🚚 Uploading 3 main DLL files
echo =============================================
echo Uploading ForexExchange.dll...
scp -o ConnectTimeout=10 -o BatchMode=yes "%LOCAL_PUBLISH_DIR%\ForexExchange.dll" %SERVER%:%APP_DIR%/
if errorlevel 1 (
    echo ❌ ForexExchange.dll upload failed.
    popd
    exit /b 1
)

echo Uploading ForexExchange.pdb...
scp -o ConnectTimeout=10 -o BatchMode=yes "%LOCAL_PUBLISH_DIR%\ForexExchange.pdb" %SERVER%:%APP_DIR%/
if errorlevel 1 (
    echo ⚠️ ForexExchange.pdb upload failed (non-critical, continuing...)
)

echo Uploading ForexExchange.deps.json...
scp -o ConnectTimeout=10 -o BatchMode=yes "%LOCAL_PUBLISH_DIR%\ForexExchange.deps.json" %SERVER%:%APP_DIR%/
if errorlevel 1 (
    echo ❌ ForexExchange.deps.json upload failed.
    popd
    exit /b 1
)

echo =============================================
echo 🔁 Restarting service %SERVICE%
echo =============================================
ssh -o ConnectTimeout=10 -o BatchMode=yes %SERVER% "systemctl start %SERVICE% && systemctl status %SERVICE% --no-pager -l | grep Active"

echo =============================================
echo ✅ Done! Backup saved as: bin_%DATETIME%
echo =============================================

popd
endlocal
pause
