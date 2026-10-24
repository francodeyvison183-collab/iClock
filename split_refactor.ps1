param([string]$Path = "src\iClock.cs")
$ErrorActionPreference = 'Stop'
$lines = Get-Content $Path
$total = $lines.Count

$marks = @()
for ($i = 0; $i -lt $total; $i++) {
    $l = $lines[$i]
    if ($l -match '^(internal|public)\s' -and $l -match '\b(class|struct|enum)\s+\w+') { $marks += $i }
}
if ($marks.Count -ne 37) { throw "Expected 37 top-level types, found $($marks.Count)." }

# Segment table maps marks indices to files; each segment ends where the next begins.
$segs = @(
    @{ M = 0;  F = 'Core.cs' },
    @{ M = 2;  F = 'Overlay.cs' },
    @{ M = 3;  F = 'AgentHud.cs' },
    @{ M = 5;  F = 'Controls.cs' },
    @{ M = 10; F = 'SettingsDialog.cs' },
    @{ M = 12; F = 'Dialogs.cs' },
    @{ M = 17; F = 'Quotes.cs' },
    @{ M = 21; F = 'WelcomeAbout.cs' },
    @{ M = 23; F = 'Menu.cs' },
    @{ M = 27; F = 'Usage.cs' }
)
for ($s = 0; $s -lt $segs.Count - 1; $s++) { $segs[$s].End = $segs[$s+1].M }
$segs[$segs.Count-1].End = 34   # Usage ends where AppContext begins

$usings = New-Object System.Collections.Generic.List[string]
for ($i = 0; $i -lt $marks[0]; $i++) { if ($lines[$i] -match '^using\s') { $usings.Add($lines[$i]) } }

$srcAbs = (Resolve-Path 'src').Path
$utf8 = New-Object System.Text.UTF8Encoding($true)
function Write-Segment([int]$from, [int]$toExclusive, [string]$file) {
    $body = New-Object System.Collections.Generic.List[string]
    for ($i = $from; $i -lt $toExclusive; $i++) { $body.Add($lines[$i]) }
    $content = ($usings -join "`r`n") + "`r`n`r`n" + (($body -join "`r`n") + "`r`n")
    [System.IO.File]::WriteAllText("$srcAbs\$file", $content, $utf8)
    "  $file <- lines $($from+1)..$toExclusive"
}

"Splitting:"
foreach ($seg in $segs) { Write-Segment $marks[$seg.M] $marks[$seg.End] $seg.F }
Write-Segment $marks[36] $total 'Screensaver.cs'

# iClock.cs keeps the header plus AppContext..Program.
$keep = New-Object System.Collections.Generic.List[string]
for ($i = 0; $i -lt $marks[0]; $i++) { $keep.Add($lines[$i]) }
$keep.Add('')
for ($i = $marks[34]; $i -lt $marks[36]; $i++) { $keep.Add($lines[$i]) }
[System.IO.File]::WriteAllText("$srcAbs\iClock.cs", (($keep -join "`r`n") + "`r`n"), $utf8)

"iClock.cs: $((Get-Item src\iClock.cs).Length) bytes, $((Get-Content src\iClock.cs).Count) lines"
