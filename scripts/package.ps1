$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$releaseDir = Join-Path $projectRoot 'release-assets'
if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir | Out-Null
}

$exeSource = Join-Path $projectRoot 'dist\iClock.exe'
if (-not (Test-Path $exeSource)) {
    & (Join-Path $projectRoot 'build.ps1')
}

$targetExe = Join-Path $releaseDir 'iClock-1.10-windows.exe'
$targetZip = Join-Path $releaseDir 'iClock-1.10-windows.zip'
$targetSums = Join-Path $releaseDir 'iClock-1.10-SHA256SUMS.txt'

Copy-Item $exeSource $targetExe -Force

$stage = Join-Path $env:TEMP 'iClock_v110_package_stage'
if (Test-Path $stage) {
    Remove-Item $stage -Recurse -Force
}
New-Item -ItemType Directory -Path $stage | Out-Null

Copy-Item $exeSource (Join-Path $stage 'iClock.exe')
Copy-Item (Join-Path $projectRoot 'README.md') (Join-Path $stage 'README.md')
Copy-Item (Join-Path $projectRoot 'LICENSE') (Join-Path $stage 'LICENSE')

if (Test-Path $targetZip) {
    Remove-Item $targetZip -Force
}

Compress-Archive -Path "$stage\*" -DestinationPath $targetZip -CompressionLevel Optimal
Remove-Item $stage -Recurse -Force

$exeHash = (Get-FileHash $targetExe -Algorithm SHA256).Hash.ToLowerInvariant()
$zipHash = (Get-FileHash $targetZip -Algorithm SHA256).Hash.ToLowerInvariant()
$exeSize = (Get-Item $targetExe).Length
$zipSize = (Get-Item $targetZip).Length

$sums = "$exeHash  iClock-1.10-windows.exe`r`n$zipHash  iClock-1.10-windows.zip`r`n"
[System.IO.File]::WriteAllText($targetSums, $sums, [System.Text.Encoding]::ASCII)

Write-Host "PACKAGE SUCCESS:"
Write-Host "EXE: $exeSize bytes | SHA256: $exeHash"
Write-Host "ZIP: $zipSize bytes | SHA256: $zipHash"
