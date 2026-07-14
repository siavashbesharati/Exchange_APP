# ============================
# Configuration
# ============================

$Site = "Plesk"
$RemoteFolder = "/"
$Publish = Join-Path $PSScriptRoot "bin\Release\net9.0\publish"
$OfflineTemplate = Join-Path $PSScriptRoot "app_offline.htm"
$RemoteOfflineName = "app_offline.htm"

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

if (!(Test-Path $OfflineTemplate)) {
    Write-Host "ERROR: app_offline.htm not found at: $OfflineTemplate" -ForegroundColor Red
    exit 1
}

if (!(Test-Path $winscp)) {
    Write-Host "ERROR: WinSCP not found at: $winscp" -ForegroundColor Red
    exit 1
}

# Helper: Run a WinSCP script. Returns $true if exit code is 0.
function Invoke-WinScpScript {
    param([string]$ScriptContent, [string]$LogPath)
    $tempFile = Join-Path $env:TEMP ([System.IO.Path]::GetRandomFileName() + ".txt")
    $ScriptContent | Set-Content $tempFile -Encoding ASCII
    & $winscp /script="$tempFile" /log="$LogPath"
    $exit = $LASTEXITCODE
    Remove-Item $tempFile -ErrorAction SilentlyContinue
    return ($exit -eq 0)
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
# Delete any existing app_offline.htm (ignore errors), then upload the template.
# ============================

Write-Host "Enabling maintenance mode..." -ForegroundColor Yellow

# 1a) Delete (ignore failure)
$deleteScript = @"
option batch continue
option confirm off
open "$Site"
rm "$RemoteFolder$RemoteOfflineName"
exit
"@
$ignore = Invoke-WinScpScript $deleteScript "$env:TEMP\winscp_delete.log"

# 1b) Upload the offline page (must succeed)
$uploadScript = @"
option batch abort
option confirm off
open "$Site"
put "$OfflineTemplate" "$RemoteFolder$RemoteOfflineName"
exit
"@
$uploadOk = Invoke-WinScpScript $uploadScript "$env:TEMP\winscp_enable.log"

if (-not $uploadOk) {
    Write-Host "Failed to enable maintenance mode! See log: $env:TEMP\winscp_enable.log" -ForegroundColor Red
    exit 1
}

# Give IIS time to unload
Start-Sleep -Seconds 5

# ============================
# Step 2: Upload application files
# ============================

Write-Host "Uploading application files..." -ForegroundColor Cyan

foreach ($file in $MainFiles) {
    $localPath = Join-Path $Publish $file
    if (!(Test-Path $localPath)) {
        Write-Host "File not found: $localPath" -ForegroundColor Red
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
$syncOk = Invoke-WinScpScript $syncScript "$env:TEMP\winscp_sync.log"

if (-not $syncOk) {
    Write-Host "Deployment failed! Site is still in maintenance mode." -ForegroundColor Red
    Write-Host "See log: $env:TEMP\winscp_sync.log" -ForegroundColor Yellow
    Write-Host "Manually delete $RemoteOfflineName on the server to restore the site." -ForegroundColor Yellow
    exit 1
}

# ============================
# Step 3: Disable maintenance mode
# Delete app_offline.htm (ignore errors) – site comes back.
# ============================

Write-Host "Disabling maintenance mode..." -ForegroundColor Yellow

$disableScript = @"
option batch continue
option confirm off
open "$Site"
rm "$RemoteFolder$RemoteOfflineName"
exit
"@
$disableOk = Invoke-WinScpScript $disableScript "$env:TEMP\winscp_disable.log"

# Even if deletion fails, we consider it a success (file might already be gone)
# but we log the failure just in case.
if (-not $disableOk) {
    Write-Host "WARNING: Could not delete $RemoteOfflineName. It may already be gone. See log: $env:TEMP\winscp_disable.log" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Deployment completed successfully!" -ForegroundColor Green