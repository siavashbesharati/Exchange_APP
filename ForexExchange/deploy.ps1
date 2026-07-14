Write-Host ""
Write-Host "Publishing..." -ForegroundColor Cyan

dotnet publish $Project -c Release -o $Publish

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

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

$script = @"
option batch abort
option confirm off

open $Site

echo Uploading app_offline.htm...
put "$Publish\app_offline.htm" "$RemoteFolder"

echo Waiting for IIS...
call timeout /t 2 > nul

echo Synchronizing files...
synchronize remote "$Publish" "$RemoteFolder"

echo Removing app_offline.htm...
rm "$RemoteFolder/app_offline.htm"

exit
"@

$temp = "$env:TEMP\deploy.txt"
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