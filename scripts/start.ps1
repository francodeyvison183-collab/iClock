# start.ps1 — launch dist\iClock.exe with its own directory as working dir.
Start-Process -FilePath D:\AIStudio\iClock\dist\iClock.exe -WorkingDirectory D:\AIStudio\iClock\dist
Start-Sleep -Seconds 2
Get-Process -Name iClock -ErrorAction SilentlyContinue | Format-Table Id,ProcessName,Path
