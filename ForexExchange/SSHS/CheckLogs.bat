@echo off
setlocal ENABLEDELAYEDEXPANSION
title 📋 Check Application Logs

:: === CONFIG ===
set SERVER=root@104.234.46.151
set APP_DIR=/var/www/taban
set SERVICE=taban.service
set LOGS_DIR=%APP_DIR%/Logs

echo =============================================
echo 📋 Checking Application Logs
echo =============================================
echo.
echo Choose an option:
echo 1. Check service status
echo 2. View recent service logs (journalctl)
echo 3. View application log files (Serilog)
echo 4. View real-time service logs (follow mode)
echo 5. View last 100 lines of service logs
echo 6. View all log files in Logs directory
echo.
set /p choice="Enter your choice (1-6): "

if "%choice%"=="1" (
    echo.
    echo =============================================
    echo 🔍 Service Status
    echo =============================================
    ssh -o ConnectTimeout=10 -o BatchMode=yes %SERVER% "systemctl status %SERVICE% --no-pager -l"
)

if "%choice%"=="2" (
    echo.
    echo =============================================
    echo 📜 Recent Service Logs (Last 50 lines)
    echo =============================================
    ssh -o ConnectTimeout=10 -o BatchMode=yes %SERVER% "journalctl -u %SERVICE% -n 50 --no-pager"
)

if "%choice%"=="3" (
    echo.
    echo =============================================
    echo 📄 Application Log Files (Serilog)
    echo =============================================
    ssh -o ConnectTimeout=10 -o BatchMode=yes %SERVER% "ls -lah %LOGS_DIR%/ 2>/dev/null || echo 'Logs directory not found. Checking if it exists...'"
    echo.
    echo =============================================
    echo 📝 Latest Log File Content (Last 100 lines)
    echo =============================================
    ssh -o ConnectTimeout=10 -o BatchMode=yes %SERVER% "if [ -d %LOGS_DIR% ]; then LATEST_LOG=\$(ls -t %LOGS_DIR%/log-*.txt 2>/dev/null | head -1); if [ -n \"\$LATEST_LOG\" ]; then echo 'File: '\$LATEST_LOG; echo '---'; tail -100 \"\$LATEST_LOG\"; else echo 'No log files found in %LOGS_DIR%'; fi; else echo 'Logs directory does not exist: %LOGS_DIR%'; fi"
)

if "%choice%"=="4" (
    echo.
    echo =============================================
    echo 🔴 Real-time Service Logs (Press Ctrl+C to exit)
    echo =============================================
    ssh -o ConnectTimeout=10 -o BatchMode=yes %SERVER% "journalctl -u %SERVICE% -f"
)

if "%choice%"=="5" (
    echo.
    echo =============================================
    echo 📜 Last 100 Lines of Service Logs
    echo =============================================
    ssh -o ConnectTimeout=10 -o BatchMode=yes %SERVER% "journalctl -u %SERVICE% -n 100 --no-pager"
)

if "%choice%"=="6" (
    echo.
    echo =============================================
    echo 📁 All Log Files in Logs Directory
    echo =============================================
    ssh -o ConnectTimeout=10 -o BatchMode=yes %SERVER% "if [ -d %LOGS_DIR% ]; then echo 'Log files:'; ls -lah %LOGS_DIR%/log-*.txt 2>/dev/null | tail -10; echo ''; echo 'Total size:'; du -sh %LOGS_DIR% 2>/dev/null; else echo 'Logs directory does not exist: %LOGS_DIR%'; echo 'Current directory contents:'; ls -lah %APP_DIR%/ | head -20; fi"
)

echo.
echo =============================================
echo ✅ Done
echo =============================================
pause











