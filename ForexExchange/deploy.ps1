# ============================
# Configuration
# ============================

# Your saved WinSCP session name
$Site = "Plesk"

# Remote website folder
$RemoteFolder = "/httpdocs/"

# Publish folder (created next to this script)
$Publish = Join-Path $PSScriptRoot "publish"

# WinSCP executable
$winscp = "${env:ProgramFiles(x86)}\WinSCP\WinSCP.com"
if (!(Test-Path $winscp)) {
    $winscp = "$env:ProgramFiles\WinSCP\WinSCP.com"
}

# ============================
# Publish
# ============================

Write-Host ""
Write-Host "Publishing..." -ForegroundColor Cyan

# No need for $Project if deploy.ps1 is in the project folder
dotnet publish -c Release 

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# ============================
# Create app_offline.htm
# ============================

Write-Host "Creating app_offline.htm..." -ForegroundColor Yellow

@"
<html>
<head><title>Updating...</title></head>
<body style="font-family:Arial;text-align:center;margin-top:100px">
<h2>Application is updating...</h2>
<p>Please try again in a few seconds.</p>
</body>
</html>
"@ | Set-Content "$Publish\app_offline.htm"

# ============================
# WinSCP script
# ============================

$script = @"
option batch abort
option confirm off

open "$Site"

put "$Publish\app_offline.htm" "$RemoteFolder"

synchronize remote "$Publish" "$RemoteFolder"

rm "$RemoteFolder/app_offline.htm"

exit
"@

$temp = Join-Path $env:TEMP "deploy.txt"
$script | Set-Content $temp

Write-Host "Uploading..." -ForegroundColor Cyan

& $winscp /script="$temp"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Deployment failed!" -ForegroundColor Red
    Remove-Item $temp -ErrorAction SilentlyContinue
    exit 1
}

Remove-Item $temp -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "Deployment completed successfully!" -ForegroundColor Green