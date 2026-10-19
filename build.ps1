$ErrorActionPreference = 'Stop'

$compilerCandidates = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) {
    throw 'C# compiler not found. Install .NET Framework 4.x Developer/Targeting Pack or Visual Studio Build Tools.'
}

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputDir = Join-Path $projectRoot 'dist'
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$outputExe = Join-Path $outputDir 'iClock.exe'
$source = Join-Path $projectRoot 'src\iClock.cs'
$zan = Join-Path $projectRoot 'src\zan.jpg'
$appIco = Join-Path $projectRoot 'src\app.ico'

Get-Process | Where-Object { $_.ProcessName -like "iClock*" -or $_.ProcessName -eq "SelfCheck" } | Stop-Process -Force

$compilerArgs = @('/nologo', '/target:winexe', '/optimize+', '/codepage:65001', "/out:$outputExe", '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Runtime.WindowsRuntime.dll', '/r:System.Web.Extensions.dll')
if (Test-Path -LiteralPath $zan) {
    $compilerArgs += "/resource:$zan,zan.jpg"
}
if (Test-Path -LiteralPath $appIco) {
    $compilerArgs += "/resource:$appIco,app.ico"
    $compilerArgs += "/win32icon:$appIco"
}
$compilerArgs += $source

& $compiler $compilerArgs
if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE." }

$size = (Get-Item -LiteralPath $outputExe).Length
Write-Host "Built $outputExe ($size bytes)."

$cliSource = Join-Path $projectRoot 'src\iclock_cli.cs'
if (Test-Path -LiteralPath $cliSource) {
    $cliExe = Join-Path $outputDir 'iclock.com'
    $cliArgs = @('/nologo', '/target:exe', '/optimize+', '/codepage:65001', "/out:$cliExe", '/r:System.Web.Extensions.dll')
    if (Test-Path -LiteralPath $appIco) { $cliArgs += "/win32icon:$appIco" }
    $cliArgs += $cliSource
    & $compiler $cliArgs
    if ($LASTEXITCODE -ne 0) { throw "CLI shim compilation failed with exit code $LASTEXITCODE." }
    $cliSize = (Get-Item -LiteralPath $cliExe).Length
    Write-Host "Built $cliExe ($cliSize bytes)."
}

$testSource = Join-Path $projectRoot 'tests\SelfCheck.cs'
if (Test-Path -LiteralPath $testSource) {
    $testExe = Join-Path $outputDir 'SelfCheck.exe'
    & $compiler @('/nologo', '/target:exe', '/optimize+', '/codepage:65001', "/out:$testExe", '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Web.Extensions.dll', $testSource)
    if ($LASTEXITCODE -ne 0) { throw "SelfCheck compilation failed with exit code $LASTEXITCODE." }
    $testSize = (Get-Item -LiteralPath $testExe).Length
    Write-Host "Built $testExe ($testSize bytes)."
}

$quoteLayoutTestSource = Join-Path $projectRoot 'tests\QuoteLayoutCheck.cs'
if (Test-Path -LiteralPath $quoteLayoutTestSource) {
    $quoteLayoutTestExe = Join-Path $outputDir 'QuoteLayoutCheck.exe'
    & $compiler @('/nologo', '/target:exe', '/optimize+', '/codepage:65001', "/out:$quoteLayoutTestExe", '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', $quoteLayoutTestSource)
    if ($LASTEXITCODE -ne 0) { throw "QuoteLayoutCheck compilation failed with exit code $LASTEXITCODE." }
}
