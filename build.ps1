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

$compilerArgs = @('/nologo', '/target:winexe', '/optimize+', "/out:$outputExe", '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll')
if (Test-Path -LiteralPath $zan) {
    $compilerArgs += "/resource:$zan,zan.jpg"
}
$compilerArgs += $source

& $compiler $compilerArgs
if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE." }

$size = (Get-Item -LiteralPath $outputExe).Length
Write-Host "Built $outputExe ($size bytes)."
