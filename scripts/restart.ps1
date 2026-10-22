# restart.ps1 — stop the running iClock and relaunch the freshly built dist exe.
# Used after each code change so the user always sees the latest build.
Stop-Process -Name iClock -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500
Start-Process -FilePath D:\AIStudio\iClock\dist\iClock.exe -WorkingDirectory D:\AIStudio\iClock\dist
Start-Sleep -Seconds 2
$p = Get-Process -Name iClock -ErrorAction SilentlyContinue
if ($p) { Write-Host ("iClock restarted, PID " + $p.Id) }
else { Write-Host "WARNING: iClock did not stay running" }
