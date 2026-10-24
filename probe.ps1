New-Item -ItemType Directory -Force -Path build_probe | Out-Null
Copy-Item src\*.cs build_probe\
foreach ($f in Get-ChildItem build_probe\*.cs) {
    $raw = [System.IO.File]::ReadAllText($f.FullName)
    $stripped = [regex]::Replace($raw, '"(?:[^"\\\r\n]|\\.)*"', '""')
    [System.IO.File]::WriteAllText($f.FullName, $stripped)
}
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 "/out:build_probe\probe.exe" /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Runtime.WindowsRuntime.dll /r:System.Web.Extensions.dll /resource:src\zan.jpg,zan.jpg /resource:src\app.ico,app.ico /win32icon:src\app.ico /resource:docs\icon-32.png,logo32.png (Get-ChildItem build_probe\*.cs | ForEach-Object { $_.FullName }) | Out-Null
if ($LASTEXITCODE -eq 0) { "strings-empty probe: $((Get-Item build_probe\probe.exe).Length)  vs real: 333824" } else { "probe build failed" }
