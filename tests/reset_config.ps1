[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$Host.UI.RawUI.WindowTitle = "iClock 配置重置工具"

Write-Host "======================================================" -ForegroundColor Cyan
Write-Host "             iClock 配置重置与首次运行测试              " -ForegroundColor Cyan
Write-Host "======================================================" -ForegroundColor Cyan
Write-Host ""

$configPath = Join-Path $env:APPDATA 'iClock\settings.ini'

if (-not (Test-Path $configPath)) {
    Write-Host "[提示] 配置文件尚未生成: $configPath" -ForegroundColor Yellow
    Write-Host "当前已是出厂纯净状态（启动时会自动触发首次欢迎引导）。" -ForegroundColor Green
} else {
    Write-Host "当前配置文件位置: $configPath" -ForegroundColor Gray
    Write-Host ""
    Write-Host "请选择操作:" -ForegroundColor White
    Write-Host "  [1] 仅重置首次启动标记 (保留时长/字体/颜色等偏好，下次启动必出欢迎引导)" -ForegroundColor Green
    Write-Host "  [2] 彻底删除配置文件   (完全恢复全新出厂纯净默认值)" -ForegroundColor Yellow
    Write-Host "  [3] 退出" -ForegroundColor Gray
    Write-Host ""

    $choice = Read-Host "请输入选项编号 [1/2/3] (默认 1)"
    if ([string]::IsNullOrWhiteSpace($choice)) { $choice = "1" }

    if ($choice -eq "3") {
        Write-Host "已取消操作。" -ForegroundColor Gray
        exit 0
    }

    Stop-Process -Name iClock -Force -ErrorAction SilentlyContinue

    if ($choice -eq "1") {
        $content = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8
        if ($content -match 'FirstRun=False') {
            $content = $content -replace 'FirstRun=False', 'FirstRun=True'
        } elseif (-not ($content -match 'FirstRun=')) {
            $content += "`r`nFirstRun=True"
        }
        [System.IO.File]::WriteAllText($configPath, $content, (New-Object System.Text.UTF8Encoding($false)))
        Write-Host ""
        Write-Host "[√] 操作成功：已重置首次运行标记 (FirstRun=True)！" -ForegroundColor Green
    } elseif ($choice -eq "2") {
        Remove-Item -LiteralPath $configPath -Force -ErrorAction SilentlyContinue
        Write-Host ""
        Write-Host "[√] 操作成功：已彻底删除配置文件，恢复纯净出厂默认状态！" -ForegroundColor Green
    } else {
        Write-Host "无效输入，操作已取消。" -ForegroundColor Red
        exit 0
    }
}

Write-Host ""
$launch = Read-Host "是否立即启动 iClock 查看效果？[Y/N] (默认 Y)"
if ([string]::IsNullOrWhiteSpace($launch) -or $launch -eq "y" -or $launch -eq "Y") {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $candidates = @(
        (Join-Path $scriptDir '..\dist\iClock.exe'),
        (Join-Path $scriptDir '..\iClock.exe'),
        (Join-Path $scriptDir 'iClock.exe')
    )
    $exe = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($exe) {
        Start-Process -FilePath (Resolve-Path $exe).Path
        Write-Host "已启动 iClock。" -ForegroundColor Cyan
    } else {
        Write-Host "未找到 iClock.exe，请手动启动程序测试。" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "按回车键退出..." -ForegroundColor Gray