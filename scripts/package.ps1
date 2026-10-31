param(
    [string]$Version
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$releaseDir = Join-Path $projectRoot 'release-assets'
if (-not (Test-Path $releaseDir)) {
    New-Item -ItemType Directory -Path $releaseDir | Out-Null
}

# Default to the version the binary itself reports, so the packaging name can
# never drift away from src/iClock.cs. Pass -Version to override.
if (-not $Version) {
    $src = Get-Content -LiteralPath (Join-Path $projectRoot 'src\iClock.cs') -Raw
    $m = [regex]::Match($src, 'CURRENT_VERSION\s*=\s*"([^"]+)"')
    if (-not $m.Success) { throw 'Could not read CURRENT_VERSION from src\iClock.cs.' }
    $Version = $m.Groups[1].Value
}
$Version = "$Version".Trim()

$exeSource = Join-Path $projectRoot 'dist\iClock.exe'
if (-not (Test-Path $exeSource)) {
    & (Join-Path $projectRoot 'build.ps1')
}

$targetExe = Join-Path $releaseDir "iClock-$Version-windows.exe"
$targetZip = Join-Path $releaseDir "iClock-$Version-windows.zip"
$targetSums = Join-Path $releaseDir "iClock-$Version-SHA256SUMS.txt"

Copy-Item $exeSource $targetExe -Force

$stage = Join-Path $env:TEMP "iClock_v${Version}_package_stage"
if (Test-Path $stage) {
    Remove-Item $stage -Recurse -Force
}
New-Item -ItemType Directory -Path $stage | Out-Null

Copy-Item $exeSource (Join-Path $stage 'iClock.exe')
$comSource = Join-Path $projectRoot 'dist\iclock.com'
if (Test-Path $comSource) {
    Copy-Item $comSource (Join-Path $stage 'iclock.com')
}
$cmdSource = Join-Path $projectRoot 'dist\iclock.cmd'
if (Test-Path $cmdSource) {
    Copy-Item $cmdSource (Join-Path $stage 'iclock.cmd')
}
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

$sums = "$exeHash  iClock-$Version-windows.exe`r`n$zipHash  iClock-$Version-windows.zip`r`n"
[System.IO.File]::WriteAllText($targetSums, $sums, [System.Text.Encoding]::ASCII)

Write-Host "PACKAGE SUCCESS (v$Version):"
Write-Host "EXE: $exeSize bytes | SHA256: $exeHash"
Write-Host "ZIP: $zipSize bytes | SHA256: $zipHash"
