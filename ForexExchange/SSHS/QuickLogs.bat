@echo off
setlocal
title 🚀 Quick Logs Check

:: === CONFIG ===
set SERVER=root@104.234.46.151
set SERVICE=taban.service

echo =============================================
echo 🚀 Quick Logs Check
echo =============================================
echo.

echo [1/3] Service Status:
ssh -o ConnectTimeout=10 -o BatchMode=yes %SERVER% "systemctl status %SERVICE% --no-pager -l | head -15"
echo.

echo [2/3] Last 30 lines of service logs:
ssh -o ConnectTimeout=10 -o BatchMode=yes %SERVER% "journalctl -u %SERVICE% -n 30 --no-pager"
echo.

echo [3/3] Application log files:
ssh -o ConnectTimeout=10 -o BatchMode=yes %SERVER% "if [ -d /var/www/taban/Logs ]; then LATEST=\$(ls -t /var/www/taban/Logs/log-*.txt 2>/dev/null | head -1); if [ -n \"\$LATEST\" ]; then echo 'Latest log: '\$LATEST; echo 'Last 20 lines:'; tail -20 \"\$LATEST\"; else echo 'No log files found'; fi; else echo 'Logs directory not found'; fi"
echo.

pause


