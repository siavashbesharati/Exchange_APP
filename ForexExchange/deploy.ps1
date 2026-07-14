# ============================
# Configuration
# ============================

# Your saved WinSCP session name
$Site = "Plesk"

# Remote website folder
$RemoteFolder = "/"

# Publish folder (default dotnet publish output location for -c Release)
$Publish = Join-Path $PSScriptRoot "bin\Release\net9.0\publish"

$RemoteOfflineName         = "app_offline.htm"
$RemoteOfflineDisabledName = "app_offline.htm.disabled"

# Only these files get deployed after publish (fast, targeted updates).
# Add more filenames here if you need other files synced too.
$MainFiles = @(
    "ForexExchange.dll",
    "ForexExchange.exe",
    "ForexExchange.pdb"
)

# WinSCP executable
$winscp = "${env:ProgramFiles(x86)}\WinSCP\WinSCP.com"
if (!(Test-Path $winscp)) {
    $winscp = "$env:ProgramFiles\WinSCP\WinSCP.com"
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
# The offline page already lives on the server as app_offline.htm.disabled.
# Just rename it to app_offline.htm so IIS picks it up immediately.
# ============================

Write-Host "Enabling maintenance mode..." -ForegroundColor Yellow

$enableScript = @"
option batch abort
option confirm off
open "$Site"

# Delete any existing live offline page so rename can succeed
option batch continue
rm "$RemoteFolder$RemoteOfflineName"
option batch abort

# Rename .disabled → .htm to enable maintenance mode
mv "$RemoteFolder$RemoteOfflineDisabledName" "$RemoteFolder$RemoteOfflineName"
exit
"@

# Give IIS a moment to actually unload the app pool / release file locks
# before we overwrite the dll/exe below.
Start-Sleep -Seconds 5

# ============================
# Step 2: Upload only the main app files (not the whole publish folder)
# ============================

Write-Host "Uploading application files..." -ForegroundColor Cyan

foreach ($file in $MainFiles) {
    $localPath = Join-Path $Publish $file
    if (!(Test-Path $localPath)) {
        Write-Host "File not found, skipping: $localPath" -ForegroundColor Red
        exit 1
    }
}

$putLines = ($MainFiles | ForEach-Object { "put `"$Publish\$_`" `"$RemoteFolder`"" }) -join "`n"

$syncScript = @"
option batch abort
option confirm off
open "$Site"
$putLines
exit
"@

$syncTemp = Join-Path $env:TEMP "deploy_sync.txt"
$syncScript | Set-Content $syncTemp

& $winscp /script="$syncTemp" /log="$env:TEMP\winscp_sync.log"
$syncExit = $LASTEXITCODE
Remove-Item $syncTemp -ErrorAction SilentlyContinue

if ($syncExit -ne 0) {
    Write-Host "Deployment failed! Site is still in maintenance mode." -ForegroundColor Red
    Write-Host "See log: $env:TEMP\winscp_sync.log" -ForegroundColor Yellow
    Write-Host "Fix the issue and re-run, or manually rename $RemoteOfflineName on the server to restore the site." -ForegroundColor Yellow
    exit 1
}

# ============================
# Step 3: Disable maintenance mode
# Rename app_offline.htm back to app_offline.htm.disabled, ready for next time.
# ============================

Write-Host "Disabling maintenance mode..." -ForegroundColor Yellow

$disableScript = @"
option batch abort
option confirm off
open "$Site"

# Remove any leftover disabled file to avoid collision
option batch continue
rm "$RemoteFolder$RemoteOfflineDisabledName"
option batch abort

# Now rename the live offline file back to .disabled
mv "$RemoteFolder$RemoteOfflineName" "$RemoteFolder$RemoteOfflineDisabledName"
exit
"@

Write-Host ""
Write-Host "Deployment completed successfully!" -ForegroundColor Green