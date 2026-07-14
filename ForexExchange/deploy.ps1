# ============================
# Configuration
# ============================

# Your saved WinSCP session name
$Site = "Plesk"

# Remote website folder
$RemoteFolder = "/"

# Publish folder (default dotnet publish output location for -c Release)
$Publish = Join-Path $PSScriptRoot "bin\Release\net9.0\publish"

# Your pre-made app_offline.htm template (the Farsi maintenance page you already have)
# Put it next to this script, or change the path below.
$OfflineTemplate = Join-Path $PSScriptRoot "app_offline.htm"

$RemoteOfflineName         = "app_offline.htm"
$RemoteOfflineDisabledName = "app_offline.htm.disabled.$(Get-Date -Format 'yyyyMMdd_HHmmss')"

# WinSCP executable
$winscp = "${env:ProgramFiles(x86)}\WinSCP\WinSCP.com"
if (!(Test-Path $winscp)) {
    $winscp = "$env:ProgramFiles\WinSCP\WinSCP.com"
}

if (!(Test-Path $OfflineTemplate)) {
    Write-Host "app_offline.htm template not found at: $OfflineTemplate" -ForegroundColor Red
    exit 1
}

# ============================
# Build
# ============================

Write-Host ""
Write-Host "Publishing..." -ForegroundColor Cyan

dotnet publish -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# ============================
# Step 1: Enable maintenance mode
# Upload the ready-made app_offline.htm BEFORE touching anything else,
# so IIS shows it immediately while the rest of the deploy runs.
# ============================

Write-Host "Enabling maintenance mode..." -ForegroundColor Yellow

$enableScript = @"
option batch abort
option confirm off
open "$Site"
put "$OfflineTemplate" "$RemoteFolder$RemoteOfflineName"
exit
"@

$enableTemp = Join-Path $env:TEMP "deploy_enable.txt"
$enableScript | Set-Content $enableTemp

& $winscp /script="$enableTemp"
$enableExit = $LASTEXITCODE
Remove-Item $enableTemp -ErrorAction SilentlyContinue

if ($enableExit -ne 0) {
    Write-Host "Failed to enable maintenance mode!" -ForegroundColor Red
    exit 1
}

# ============================
# Step 2: Sync application files
# -filemask="|app_offline.htm" EXCLUDES that file from the sync,
# so it's never overwritten or touched while the app files upload.
# ============================

Write-Host "Uploading application files..." -ForegroundColor Cyan

$syncScript = @"
option batch abort
option confirm off
open "$Site"
synchronize remote "$Publish" "$RemoteFolder" -filemask="|$RemoteOfflineName"
exit
"@

$syncTemp = Join-Path $env:TEMP "deploy_sync.txt"
$syncScript | Set-Content $syncTemp

& $winscp /script="$syncTemp"
$syncExit = $LASTEXITCODE
Remove-Item $syncTemp -ErrorAction SilentlyContinue

if ($syncExit -ne 0) {
    Write-Host "Deployment failed! Site is still in maintenance mode." -ForegroundColor Red
    Write-Host "Fix the issue and re-run, or manually rename $RemoteOfflineName on the server to restore the site." -ForegroundColor Yellow
    exit 1
}

# ============================
# Step 3: Disable maintenance mode
# Renamed (not deleted) so IIS stops finding it, but you keep a copy
# on the server for reference. The name includes a timestamp so it
# never collides with a leftover .disabled file from an earlier run
# (that collision is what was causing the rename to fail before).
# ============================

Write-Host "Disabling maintenance mode..." -ForegroundColor Yellow

$disableScript = @"
option batch abort
option confirm off
open "$Site"
mv "$RemoteFolder$RemoteOfflineName" "$RemoteFolder$RemoteOfflineDisabledName"
exit
"@

$disableTemp = Join-Path $env:TEMP "deploy_disable.txt"
$disableScript | Set-Content $disableTemp

& $winscp /script="$disableTemp"
$disableExit = $LASTEXITCODE
Remove-Item $disableTemp -ErrorAction SilentlyContinue

if ($disableExit -ne 0) {
    Write-Host "WARNING: failed to rename $RemoteOfflineName away. Site may still be in maintenance mode!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Deployment completed successfully!" -ForegroundColor Green