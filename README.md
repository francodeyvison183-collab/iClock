<div align="center">

# <img src="docs/icon-64.png" width="36" height="36" style="vertical-align: -6px;" alt="iClock Logo" /> iClock

### 专为开发者与效率玩家打造的 Windows 原生极简效率中枢 (300 KB)
**⏱ 极简悬浮倒计时 · 💬 桌面每日格言 · 🤖 AI 智能体命令行协同**

[![Release](https://img.shields.io/github/v/release/francodeyvison183-collab/iClock?color=blue&label=Release)](https://github.com/francodeyvison183-collab/iClock/releases/latest)
[![Platform](https://img.shields.io/badge/Platform-Windows%207%20%7C%208%20%7C%2010%20%7C%2011-0078D6?logo=windows&logoColor=white)](https://github.com/francodeyvison183-collab/iClock/releases/latest)
[![Size](https://img.shields.io/badge/Size-300%20KB-success)](https://github.com/francodeyvison183-collab/iClock/releases/latest)
[![WinGet](https://img.shields.io/badge/WinGet-FrancoDeyvison.iClock-0078D6?logo=windows-terminal&logoColor=white)](https://github.com/microsoft/winget-pkgs/pull/449511)
[![Author](https://img.shields.io/badge/Author-gujiangwen-blue.svg)](mailto:francodeyvison183@gmail.com)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

<p align="center">
  <b>单文件原生应用 · 运行内存 &lt; 15 MB · 0.0% CPU 占用 · 鼠标点击穿透 · 零轮询桌面格言 · CLI 智能体协同</b>
</p>

<p align="center">
  <a href="https://francodeyvison183-collab.github.io/iClock/"><b>🌐 访问官网</b></a> ·
  <a href="https://github.com/francodeyvison183-collab/iClock/releases/latest"><b>🚀 下载最新版 (ZIP 便携包 · 推荐)</b></a> ·
  <a href="https://francodeyvison183-collab.github.io/iClock/llms.txt"><b>🤖 llms.txt</b></a> ·
  <a href="README_EN.md"><b>English Documentation</b></a> ·
  <a href="docs/USER_GUIDE.md">使用指南</a> ·
  <a href="CHANGELOG.md">更新日志</a>
</p>

<img src="docs/preview.png" alt="iClock Preview" width="860" />

</div>

---

## 💡 为什么选择 iClock？

市面上的桌面工具往往走向两个极端：要么功能单一粗糙，要么动辄 **100MB+**（Electron 臃肿应用），常驻后台持续消耗 CPU 与数百兆内存，甚至带有弹窗广告和后台遥测；而传统窗口还会死死遮挡桌面，严重干扰鼠标操作与编辑视线。

**iClock 坚持极致的工程师极简克制哲学**：基于 Windows 原生底层 Win32 与 GDI+ API 构建，不含任何第三方依赖或庞大运行时，运行内存稳定在 **15 MB** 以内。它不仅是一个支持点击穿透的极简悬浮倒计时，更是融合了**零轮询桌面灵感格言**与**命令行驱动的智能体桌面协同**的桌面效率中枢。

---

## 🌟 三大核心功能

### 1. ⏱ 极简悬浮倒计时
* 🪟 **原生 32 位 ARGB 渲染 & 鼠标全穿透**：基于 Windows 原生 `UpdateLayeredWindow` 渲染，文字边缘细腻平滑无毛刺，自适应微投影在深浅色背景与游戏画面上清晰锐利；默认完全点击穿透（`WS_EX_TRANSPARENT`），不抢焦点、不阻挡代码与网页点击。
* ⌨️ **随心微交互 & 托盘滚轮调时**：在屏幕上**按住 `Alt` + 鼠标左键直接拖拽**瞬时定位；鼠标悬停托盘图标**滚动滚轮快速微调时长**（`±1m`，按住 `Shift` 为 `±5m`）；支持 `Ctrl + Shift + T` 全局热键盲操启停与托盘单击切换。
* 🔔 **阶梯提醒与全屏免打扰**：支持**全屏置顶弹窗强提醒**（经典机械电话铃声/闹铃，防沉迷）与**原位呼吸轻提醒**（桌面悬浮文字原地柔和呼吸微闪烁 5 秒+单次轻音，绝不抢占前台焦点打断心流）；智能检测前台全屏游戏、DirectX/Vulkan 渲染或 PPT 幻灯片放映，**自动平滑降级为原位呼吸免打扰**。
* 📊 **今日专注与多维统计**：支持【单日明细】（历史日期切换、当日汇总）与【按天汇总】（查看最近 7/14/30 天或全部历史的专注总时长、周期数、达成率与成就评价），选中文本 `Ctrl+C` 一键复制，本地 TSV 自动归档。

### 2. 💬 桌面每日格言与灵感常驻
* 🌅 **晨间唤醒与常驻桌面**：开机/解锁晨间唤醒居中优雅浮现；支持一键将格言钉在桌面背景或悬浮常驻。
* ⚡ **0-Polling 零轮询极致节能**：基于 Windows 原生消息驱动架构，彻底剔除了后台循环轮询定时器，桌面常驻时 **0 唤醒、0.0% CPU 占用**。
* 🎨 **桌面随心微交互**：在桌面格言文字上**按住 `Alt` + 拖拽调整位置**，拖动右边缘调节文本换行宽度，**`Alt + Ctrl + 滚轮` 快速缩放字号**，双击快速换一句。
* 📚 **海量词库与 AI 智能体批量扩充**：内置 300+ 离线中英文精选格言，支持在记事本中自定义编辑 `quotes.txt`；AI 智能体可生成格言文件，再通过 CLI 导入，自动去重并创建 `quotes.txt.bak` 安全备份。

### 3. 🤖 AI 智能体长任务命令行协同
* 👁 **20+ 款主流编程智能体任务感知矩阵**：全面覆盖 Cursor、Antigravity、Claude Code、GitHub Copilot、Windsurf、Trae、通义灵码、CodeGeeX、文心快码、MiniMax、CodeBuddy、Aider 等主流智能体。采用四级感知矩阵（Windows Toast 拦截、`HSHELL_FLASH` 窗口闪烁、窗口标题变化、多进程生命周期），智能体跑长任务完毕 **0ms 零误报即时提醒**，支持一键点击还原智能体窗口。
* 💻 **结构化 CLI 桌面协作**：智能体通过 `--ask` 请求人工决策、`--progress` 更新任务状态、`--notice` 发送完成提醒；加 `--json` 获取结构化结果。`--agent-name` 支持区分并行智能体，`--quote-add` 可导入 AI 生成的格言。
* 🔗 **轻量 IPC 与脚本自动化**：通过 `WM_COPYDATA` 将命令发送给已运行的 iClock；`iclock --install-cli` 可将程序目录加入用户 PATH，供智能体、CI/CD 和脚本调用。

---

## 📊 性能与体验横向对比

| 核心维度 | **iClock (本项目)** | 常见 Electron 计时器 | Windows 自带闹钟 | 网页在线番茄钟 |
| :--- | :--- | :--- | :--- | :--- |
| **安装包体积** | ⚡ **300 KB** (单文件免安装) | 120 MB ~ 250 MB | 系统预装应用 | 每次加载数 MB |
| **常驻内存** | ⚡ **~15 MB** (物理基线) | 150 MB ~ 300 MB | ~30 MB | 80 MB ~ 200 MB (浏览器标签) |
| **CPU 占用** | ⚡ **0.0%** (动态心跳对齐) | 0.5% ~ 3.0% (后台轮询) | 0.0% | 0.3% ~ 1.5% |
| **交互阻碍** | ⚡ **完全点击穿透，不挡鼠标** | 实体窗口遮挡代码与网页 | 最小化或占用全屏 | 必须切出浏览器查看 |
| **智能体桌面协同** | ⚡ **CLI + 结构化 JSON** | ❌ 无 | ❌ 无 | ❌ 无 |
| **AI 任务监听** | ⚡ **20+ 款智能体任务结束感知** | ❌ 无 | ❌ 无 | ❌ 无 |
| **桌面灵感格言** | ⚡ **0-Polling 悬浮 + AI 扩充** | ❌ 无 | ❌ 无 | ❌ 仅限网页内显示 |
| **网络行为** | ⚡ **0 联网请求，纯本地隐私** | 多数携带统计与更新上报 | 绑定微软在线账户 | 数据上报云端服务器 |

---

## 🚀 快速上手

### 方式 A：绿色便携压缩包运行（官方推荐 · 防浏览器误报）

1. 前往 [GitHub Releases](../../releases/latest) 下载 **`iClock-1.09-windows.zip` 便携压缩包**（推荐优先下载压缩包，可有效避免 Chrome / Edge 等浏览器对未签名独立 `.exe` 发出信任警告与 SmartScreen 误报拦截）。
2. 解压压缩包后，双击 `iClock.exe` 直接运行（绿色免安装，不写注册表垃圾）。
3. 默认情况下，启动后倒计时处于待命状态（屏幕不显示文字）。

### 方式 B：Windows 官方包管理器 WinGet 一键安装

在 PowerShell 或 CMD 中运行：
```powershell
winget install FrancoDeyvison.iClock
```
> 自动完成安全下载、安装并加入系统环境变量，可在任意终端直接敲 `iClock` 运行。

### 方式 C：Scoop 包管理器安装

```powershell
scoop install https://raw.githubusercontent.com/francodeyvison183-collab/iClock/main/scoop/iclock.json
```

---

## 🤖 AI 智能体 CLI 协同

在「设置 → AI 协作」复制命令行协作说明并提供给智能体。先运行 `iclock --install-cli` 配置 PATH，然后智能体即可调用以下桌面能力；`--json` 提供结构化结果：

```powershell
iclock --ask "是否继续部署？" --options "继续,取消" --json
iclock --progress 3/5 "正在运行测试" --agent-name "Codex" --json
iclock --progress clear --agent-name "Codex" --json
iclock --notice "任务已完成" --agent-name "Codex" --json
iclock --quote-add "quotes.txt" --json
```

旧版智能体 MCP 配置不会由 iClock 自动修改；请在对应智能体设置中手动删除旧的 `iclock` 服务条目。`--mcp` 等旧参数已停用。

---

## 💻 命令行与脚本自动化 (CLI)

iClock 内置原生 **Win32 IPC 消息管道 (`WM_COPYDATA`)**。二次运行命令行不会启动重复进程，而是秒级穿透控制正在后台常驻的 iClock。

### 1. 一键全局免 `.exe` 终端调用（像 claude / git 一样）

首次使用时，只需在终端运行一次内置的 `--install-cli` 指令：
```powershell
iclock --install-cli
```
程序会自动将当前程序所在目录写入用户的 **User PATH** 环境变量。之后在任何目录、任何终端（PowerShell / CMD / Git Bash / Windows Terminal），均可直接输入 `iclock` 全局调用：

### 2. 常用命令行参数速查

| 命令 | 简写 | 功能说明 | 典型示例 |
| :--- | :--- | :--- | :--- |
| `iclock` | - | 唤醒或启动 iClock 后台常驻 | `iclock` |
| `iclock --start [分钟]` | `iclock -s [m]` | 启动指定分钟的工作倒计时 | `iclock -s 25` 或 `iclock -s 45` |
| `iclock --pause` | `iclock -p` | 暂停当前运行的倒计时 | `iclock -p` |
| `iclock --resume` | `iclock -r` | 恢复已暂停的倒计时 | `iclock -r` |
| `iclock --toggle` | `iclock -t` | 快速切换 暂停 / 继续 | `iclock -t` |
| `iclock --reset` | - | 重置倒计时为待命状态并隐藏悬浮字 | `iclock --reset` |
| `iclock --rest [分钟]` | - | 开启休息倒计时（默认 5 分钟） | `iclock --rest 10` |
| `iclock -n <消息>` | `iclock --notice` | 触发桌面置顶强通知弹窗与提示音 | `iclock -n "生产构建已完成！"` |
| `iclock -a <问题>` | `iclock --ask` | 极速桌面授权门禁：弹出人机决策门禁卡片 | `iclock -a "发布到生产?" --options "同意,取消"` |
| `iclock -pr <进度|clear> [说明]` | `iclock --progress` | 态势感知：在悬浮时钟底部投射发丝进度条 | `iclock -pr 60 "编译中..."` 或 `iclock -pr clear` |
| `iclock -q` | `iclock --quote` | 随机换一句桌面格言 | `iclock -q` |
| `iclock --install-cli` | - | 自动将当前目录加入用户 PATH 环境变量 | `iclock --install-cli` |
| `iclock --help` | `iclock -h` | 在终端控制台打印帮助手册 | `iclock -h` |

### 3. 典型脚本联动示例
```bash
# 极速桌面授权门禁：关键/危险脚本执行前弹窗获取人工授权确认（支持 Enter/1-9/Esc 盲打）
RESP=$(iclock -a "即将对生产数据库执行迁移，是否继续？" --options "确认执行,取消")
if [ "$RESP" = "确认执行" ]; then
    ./deploy.sh
fi

# 前端长构建完成提醒
npm run build && iclock -n "前端构建完成，用时 45 秒！"

# 单元测试与自动化回归通过提醒
pytest && iclock -n "44 项全量测试全部通过！"

# 终端一键开启 50 分钟极客冲刺
iclock -s 50
```

---

## 🛡️ 智能体 Harness 伴生扩展 (Agent Runtime Harness)

当运行 **DeepSeek、Pi、Aider、AutoGPT** 等自主智能体或长推理模型时，传统的执行外壳（Harness）往往局限在终端字符流或重型 Web 容器中，带来**严重的注意力脱节与窗口打断感**。

iClock 单文件仅 300 KB、内存 < 15 MB、0.0% CPU 占用且原生全鼠标穿透，是极具辨识度的 **Windows 桌面原生伴生外骨骼（Ambient Desktop Harness）**：

### 1. 为什么用 iClock 作为智能体 Harness 扩展？

* 👻 **幽灵态态势感知 (Ambient HUD)**：长任务（如 DeepSeek `<think>` 深度推理、全量自动化测试、大型重构）中，通过屏幕底部 2px 发丝进度条与微呼吸律动投射进度，余光即可掌握状态，彻底告别盯着终端日志刷屏的焦虑。
* 🚦 **零焦点破坏的门禁授权 (HITL Gatekeeper)**：遇到危险操作（删除数据、`git push --force`）时唤起置顶半透明卡片，**绝不抢占前台键盘输入焦点**；支持 `1-9` 或 `Enter/Esc` 盲打秒级决断，无需频繁 Alt+Tab 切屏。
* ⏳ **硬性时间预算锚点 (Budget-Anchored Guardrail)**：自主 Agent 启动时绑定 iClock 倒计时（如 `iclock -s 15`），提供物理时限约束与超时熔断，防止 Agent 陷入逻辑死循环消耗过多 Token。
* 💬 **桌面灵感轻语 (Ambient Whispers)**：类似 Pi 伴生助手的温感关怀，或 DeepSeek 得出的阶段性关键结论，可无感投射在桌面壁纸背景，不遮挡任何前台工作心流。

### 2. 极简 Python Harness 范例 (30 秒即插即用)

任何 Python 智能体脚本或框架（LangGraph、Aider、自定义 LLM Loop）无需任何重型依赖，仅需调用底层 IPC 即可快速接入：

```python
import subprocess

class iClockHarness:
    """iClock 极简桌面伴生外骨骼"""
    @staticmethod
    def progress(percent: int, label: str = ""):
        """投射屏幕底部发丝进度条 (如 45 'DeepSeek 推理中...')"""
        subprocess.run(["iclock", "-pr", str(percent), label], check=False)

    @staticmethod
    def ask_human(prompt: str, options: str = "批准执行,取消") -> str:
        """非侵入式桌面授权门禁 (返回用户选择，支持盲打快速确认)"""
        res = subprocess.run(
            ["iclock", "-a", prompt, "--options", options],
            capture_output=True, text=True, check=False
        )
        return res.stdout.strip()

    @staticmethod
    def notify(message: str):
        """里程碑强置顶通知与提示音"""
        subprocess.run(["iclock", "-n", message], check=False)

# ── 典型自主 Agent 循环中使用 ──
harness = iClockHarness()

# 1. 投射任务阶段
harness.progress(35, "DeepSeek 正在分析代码依赖与思维链...")

# 2. 遇到高风险工具调用时请求人类决策门禁
if tool_call.is_sensitive:
    choice = harness.ask_human(f"即将执行: {tool_call.name}，是否允许？")
    if choice != "批准执行":
        raise PermissionError("人类拒绝了该操作")

# 3. 任务圆满交付
harness.progress(100, "重构完成")
harness.notify("✨ 自动化测试全部通过，耗时 3 分 20 秒！")
```

---

## 🎯 常用快捷键与交互指南

* **启动 / 暂停**：按下全局快捷键 `Ctrl + Shift + T`，或直接鼠标左键单击托盘图标。
* **随心移动倒计时位置**：在屏幕上**按住 `Alt` 键 + 鼠标左键直接拖拽**倒计时文字，松开即自动保存。
* **托盘滚轮微调时长**：鼠标悬停在系统托盘小图标上，直接滚动滚轮快速微调时长（`±1m`，按住 `Shift` 为 `±5m`）。
* **桌面每日格言微交互**：在桌面格言文字上**按住 `Alt` + 拖拽调整位置**，拖动右边缘调节宽度，**`Alt + Ctrl + 滚轮` 快速缩放字号**，双击快速换一句，点击 `📌` 钉在桌面背景。
* **自定义配置**：右键托盘图标 $\rightarrow$ **“设置”**，原生 Win11 资源管理器风格 3 标签页：
  - **⏱ 倒计时**：配置时长、字号、颜色、字体、显示格式、提示声音（QQ提示音/机械电话铃/闹铃）、提醒方式、开机自启。
  - **💬 每日格言**：开启/关闭每日格言、桌面常驻显示、层级、10 色预设卡、`[ 自定义… ]` 编辑与 `[ ✨ AI 扩充 ]` 批量扩充。
  - **🤖 AI 协作**：智能体后台任务监控开关、CLI 协作提示词与终端 `iclock -h` / `iclock --install-cli` 指引。

---

## ❓ 常见问题解答

* **Q: 为什么打游戏或写代码时不挡鼠标操作？**
  **A**: iClock 深度利用了 Windows 底层 `WS_EX_TRANSPARENT` 穿透特性。倒计时浮层浮现在所有窗口最顶层，但所有鼠标点击与滚轮事件均直接穿透到底层的编辑器、终端或全屏游戏，绝不抢占焦点。
* **Q: 为什么软件体积只有 300 KB？**
  **A**: 坚持纯粹原生。基于 Windows 内置的 .NET Framework 与 GDI+ 绘制，坚决不打包任何动辄上百兆的 Electron 或 Chromium 浏览器壳，运行内存稳定在 15 MB 以内。
* **Q: 为什么桌面格言常驻后台不耗电？**
  **A**: iClock 采用原生 Windows 消息驱动与 0-Polling 架构，没有后台死循环轮询。静止常驻时 CPU 占用为 0.0%，唤醒开销降至极限，笔记本长驻运行零耗电。
* **Q: 智能体如何使用桌面协作？**
  **A**: 让智能体通过 CLI 调用 iClock 的 `--ask`、`--progress`、`--notice` 等命令；`--json` 可获得机器可读的返回结果。
* **Q: 软件是否安全、纯净无广告？**
  **A**: 100% 开源（MIT 协议），零流氓广告，零捆绑插件，零后台常驻守护进程，退出即彻底释放。详见 [PRIVACY.md](docs/PRIVACY.md)。

---

## 🛠️ 从源码构建

本项目采用极简设计，基于 Windows 原生 C# 与 .NET Framework 4.x，**无需安装任何庞大的 Visual Studio 或依赖包**，使用系统自带的 `csc.exe` 即可极速编译：

```powershell
git clone https://github.com/francodeyvison183-collab/iClock.git
cd iClock
.\build.ps1
```
> 编译耗时不到 1 秒，产物输出在 `dist/iClock.exe`（单文件约 300 KB）。详见[构建说明](docs/BUILDING.md)。

---

## 🔒 隐私与安全说明

* **存储位置**：所有配置文件与历史记录均保存在本地目录（便携模式优先）或 `%APPDATA%\iClock`，不留系统垃圾。
* **系统修改**：仅当在设置中显式勾选“开机自启动”时，才会写入当前用户的注册表自启项 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`；运行 `--install-cli` 时仅向当前用户的 User PATH 追加单条路径。
* **零网络通信**：代码无任何第三方 Socket 遥测或云端上报，可完全断网离线安全运行。详见[隐私说明](docs/PRIVACY.md)。

---

## 💖 支持与赞赏

如果您觉得 iClock 让您的桌面更清爽、工作更专注，欢迎为本项目点亮一颗 ⭐️ **Star**，也欢迎请作者喝一杯咖啡鼓励持续维护：

<div align="center">
  <img src="src/zan.jpg" width="220" alt="微信赞赏码" />
  <p><b>微信扫一扫 赞赏作者</b></p>
  <p><i>感谢每一位支持开源创作的朋友！</i></p>
</div>

---

## 👨‍💻 作者与联系方式

* **作者 / 开发者**：`gujiangwen`
* **反馈与交流邮箱**：[`francodeyvison183@gmail.com`](mailto:francodeyvison183@gmail.com)
* **GitHub 仓库**：[francodeyvison183-collab/iClock](https://github.com/francodeyvison183-collab/iClock)
* **官方主页**：[https://francodeyvison183-collab.github.io/iClock/](https://francodeyvison183-collab.github.io/iClock/)

欢迎提交 Issue、Pull Request 或随时通过邮件交流产品建议与使用体验！

---

## 📄 开源许可

本项目基于 [MIT License](LICENSE) 开源许可，自由商用与个人使用。欢迎提交 Issue 与 Pull Request！
