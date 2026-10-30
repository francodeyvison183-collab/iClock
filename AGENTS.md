# iClock Project Agent Instructions & Workflow Rules

## 1. 方案文档管理 (Plan Documents)
- **本地存储路径**：所有方案文档、重构计划、设计草案必须保存到本地 `plan/` 目录（例如 `plan/xxx_plan.md`）。
- **仅保留本地**：`plan/` 目录已列入 `.gitignore`，**严禁提交到 GitHub**。
- **讨论与实施纪律**：
  - 在需求分析、讨论和方案设计阶段，**只更新 `plan/` 中的方案文档**。
  - **严禁提前进入代码实施阶段**，必须在方案获得用户明确确认或下达实施指令后，方可开始修改代码。

## 2. 任务完成生命周期 (Task Completion Protocol)
- **重启 iClock**：每次代码修改、构建与验证（SelfCheck）全部完成后，**必须自动重启 iClock**：
  1. 先强制终止已有进程：`Get-Process iClock -ErrorAction SilentlyContinue | Stop-Process -Force`
  2. 必须使用 WMI 彻底脱钩启动，避免被 Agent 临时执行会话连带杀死：
     `Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine = '"d:\AIStudio\iClock\dist\iClock.exe"'}`
  3. 检查进程存活：确保 `iClock` 在后台平稳常驻，用户在桌面上即时获得最新版本。
- **临时文件清理**：`scratch/` 目录仅用于临时调试排查，任务完成后必须彻底清理删除，严禁提交到 Git。

## 3. 质量与体积红线 (Quality & Size Constraints)
- **二进制体积上限**：`dist\iClock.exe` 严格不得超过 **1,048,576 字节 (1MB)**。
- **自检覆盖**：任何改动必须先运行 `powershell -ExecutionPolicy Bypass -File .\build.ps1` 并执行 `dist\SelfCheck.exe`，确保所有自检项 100% PASS。
