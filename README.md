<div align="center">

# ⏱️ iClock

### 专为开发者与效率玩家打造的 Windows 极简透明悬浮倒计时

[![Release](https://img.shields.io/github/v/release/francodeyvison183-collab/iClock?color=blue&label=Release)](https://github.com/francodeyvison183-collab/iClock/releases/latest)
[![Platform](https://img.shields.io/badge/Platform-Windows%207%20%7C%208%20%7C%2010%20%7C%2011-0078D6?logo=windows&logoColor=white)](https://github.com/francodeyvison183-collab/iClock/releases/latest)
[![Size](https://img.shields.io/badge/Size-202%20KB-success)](https://github.com/francodeyvison183-collab/iClock/releases/latest)
[![WinGet](https://img.shields.io/badge/WinGet-FrancoDeyvison.iClock-0078D6?logo=windows-terminal&logoColor=white)](https://github.com/microsoft/winget-pkgs/pull/449511)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

<p align="center">
  <b>单文件 202 KB · 运行内存 &lt; 15 MB · 鼠标点击穿透 · 全局热键 · 零第三方依赖 · 纯本地隐私</b>
</p>

<p align="center">
  <a href="https://francodeyvison183-collab.github.io/iClock/"><b>🌐 访问官网</b></a> ·
  <a href="https://github.com/francodeyvison183-collab/iClock/releases/latest"><b>🚀 下载最新版</b></a> ·
  <a href="https://francodeyvison183-collab.github.io/iClock/llms.txt"><b>🤖 llms.txt</b></a> ·
  <a href="README_EN.md">English Documentation</a> ·
  <a href="docs/USER_GUIDE.md">使用指南</a> ·
  <a href="CHANGELOG.md">更新日志</a>
</p>

<img src="docs/preview.png" alt="iClock Preview" width="860" />

</div>

---

## 💡 为什么选择 iClock？

市面上的倒计时与番茄钟软件，往往动辄 **100MB+**（Electron 臃肿应用），常驻后台持续消耗 CPU 与内存，甚至带有弹窗广告和后台遥测；而传统窗口则会死死遮挡桌面，干扰鼠标操作。

**iClock 坚持极致的工程师极简克制哲学**：

* 🪟 **原生 32 位 ARGB 渲染 & 鼠标穿透**：基于 Windows 原生 `UpdateLayeredWindow` 渲染，彻底消除传统色键点阵毛刺，文字边缘细腻平滑（抗锯齿）；自适应边缘微投影保证在浅色文档、深黑终端或游戏画面上清晰锐利；默认完全点击穿透，不抢焦点、不阻挡操作。
* ⚡ **单文件极简 & 0.0% CPU**：不含任何第三方依赖或庞大运行时。经过深度的资源预分配与动态心跳对齐优化，唤醒开销降至极限，笔记本长驻运行零耗电。
* ⌨️ **随心微交互 & 托盘滚轮**：在屏幕上**按住 `Alt` + 鼠标左键直接拖拽**瞬时定位（无需繁琐进出托盘菜单）；鼠标悬停托盘图标**滚动滚轮快速微调时长**（`±1m`，按住 `Shift` 为 `±5m`）；默认 `Ctrl + Shift + T` 全局热键盲开/暂停，支持托盘单击启停。
* 🔔 **阶梯提醒（强置顶 vs 原位呼吸轻提醒）与全屏免打扰**：支持**弹窗强提醒**（全屏置顶弹窗+循环提示音，防错过）与**原位呼吸轻提醒**（桌面悬浮文字原地柔和呼吸微闪烁 5 秒+单次提示音，绝不抢占前台焦点打断心流）；**全屏/游戏场景自动免打扰**：智能检测前台全屏游戏、DirectX/Vulkan 渲染或 PPT 幻灯片放映，自动将强弹窗平滑降级为原位呼吸提醒，彻底告别打游戏被切出桌面或做报告被强弹窗打断的尴尬。
* 🤖 **AI 智能体后台长任务完成监控**：专为深度使用 AI 编程助手与自主 Agent 的开发者打造。全面覆盖国内外主流智能体（Cursor、Antigravity、Claude Code、GitHub Copilot、Trae、通义灵码、CodeGeeX、文心快码、MiniMax、CodeBuddy、Aider 等 20+ 款）。独创 **Toast 系统通知 $\rightarrow$ Win32 Shell 任务栏闪烁 (0ms) $\rightarrow$ 终端/IDE 标题状态 Badge $\rightarrow$ 高阈值集群算力边沿滤波** 四级梯队感知矩阵，零误报、零延迟，任务结束即时提醒，支持一键点击还原前台窗口！
* 🔒 **纯本地、便携免安装**：配置文件优先读取程序同目录（便携模式），未找到时保存在 `%APPDATA%\iClock`，不写系统垃圾，不收集任何隐私遥测。
* 🌍 **中英双语与历史日志**：托盘菜单一键无缝切换语言；自动生成按天归档的专注历史 TSV 文件。

---

## 📊 性能与体验横向对比

| 核心维度 | **iClock (本项目)** | 常见 Electron 计时器 | Windows 自带闹钟 |
| :--- | :--- | :--- | :--- |
| **安装包体积** | ⚡ **202 KB** (绿色单文件，即下即用) | 120 MB ~ 250 MB | 预装应用 |
| **常驻内存** | ⚡ **~15 MB** (物理基线) | 150 MB ~ 300 MB | ~30 MB |
| **CPU 占用** | ⚡ **0.0%** (动态心跳对齐) | 0.5% ~ 3.0% (后台轮询) | 0.0% |
| **交互阻碍** | ⚡ **完全点击穿透，无边框** | 实体窗口遮挡代码与网页 | 最小化或全屏窗口 |
| **启动速度** | ⚡ **< 100ms** (瞬间启动) | 2 ~ 4 秒 | 1 ~ 2 秒 |
| **网络行为** | ⚡ **0 联网请求，纯本地** | 多数携带统计与更新上报 | 绑定微软在线账户 |

---

## 🎯 典型使用场景

* 🍅 **开发者深度心流 / 番茄工作法**：写代码时一键启动 25 分钟，数字透明显示在屏幕一角，不遮挡编辑器视线。
* 🎤 **技术分享 / 会议演讲控时**：在屏幕右下角低调提示剩余发言时间，鼠标自由翻动 PPT。
* 🎮 **游戏技能 / 任务等待冷却**：全屏或无边框窗口直接穿透，激烈操作绝不误点。
* ☕ **久坐起身 / 喝水护眼提醒**：设定 45 分钟，到点后强制置顶提醒活动身体。

---

## 🚀 快速上手

### 方式 A：Windows 官方包管理器 WinGet 一键安装（推荐）

在 PowerShell 或 CMD 中运行：
```powershell
winget install FrancoDeyvison.iClock
```
> 自动完成安全下载、安装并加入系统环境变量，可在任意终端直接敲 `iClock` 运行。

### 方式 B：Scoop 包管理器安装

```powershell
scoop install https://raw.githubusercontent.com/francodeyvison183-collab/iClock/main/scoop/iclock.json
```

### 方式 C：绿色便携单文件运行

1. 前往 [GitHub Releases](../../releases/latest) 下载 `iClock-1.05-windows.zip` 或单文件 `iClock-1.05-windows.exe`。
2. 双击直接运行（无需安装，不写注册表垃圾）。
3. 默认情况下，启动后倒计时处于待命状态（屏幕不显示文字）。

### 常用操作

* **启动 / 暂停**：按下全局快捷键 `Ctrl + Shift + T`（支持备选冲突顺延与自定义），或直接鼠标左键单击托盘图标。
* **随心移动位置（推荐）**：在屏幕上**按住 `Alt` 键 + 鼠标左键直接拖拽**倒计时文字，拖到任意位置松开即自动保存。亦可通过托盘菜单“调整文字位置”定位。
* **托盘滚轮微调时长**：鼠标悬停在系统托盘小图标上，直接滚动滚轮快速微调时长（`±1分钟`，按住 `Shift` 为 `±5分钟`）。
* **自定义配置**：右键托盘图标 $\rightarrow$ **“设置”**，采用现代化胶囊双标签页（⏱ 倒计时 / 🤖 AI 智能体协作），可配置时长、字号、颜色、字体、显示格式、提示声音（柔和清脆 / 经典通知 / 渐进闹铃）、提醒方式、开机自启，以及查看已检测的国内外 AI 智能体（涵盖 Cursor、Antigravity、Claude Code、GitHub Copilot、Trae、通义灵码、CodeGeeX、文心快码、MiniMax、CodeBuddy、WorkBuddy、Gemini 等）并启用后台长任务完成通知。
* **查看专注历史与统计**：右键托盘图标 $\rightarrow$ **“查看今日记录”**，支持【单日明细】（带历史日期切换器、当日汇总与键盘 `←` / `→` 步进）与【按天汇总】（查看最近 7/14/30 天或全部历史的专注总时长、周期数、完成率及达成评价，支持双击任意日期穿透下钻），选中文本支持 `Ctrl+C` 一键复制。

---

## ❓ 常见问题 (FAQ)

* **Q: 为什么打游戏或写代码时不挡鼠标操作？**  
  **A**: iClock 深度利用了 Windows 底层 `WS_EX_TRANSPARENT` 穿透特性。倒计时文字置顶浮现于所有窗口之上，但所有鼠标点击和滚轮事件都会直接穿透到底层的游戏或编辑器，丝毫不抢占焦点。
* **Q: 为什么软件体积只有 202 KB？**  
  **A**: 坚持纯粹原生。基于 Windows 内置的 .NET Framework 与 GDI+ 绘制，坚决不打包任何动辄上百兆的 Electron 或 Chromium 浏览器壳，运行内存稳定在 15 MB 以内。
* **Q: 软件是否安全、纯净无广告？**  
  **A**: 100% 开源（MIT 协议），零流氓广告，零捆绑插件，零后台常驻进程，退出即彻底释放。详见 [PRIVACY.md](docs/PRIVACY.md)。

---

## 🛠️ 从源码构建

本项目采用极简设计，基于 Windows 原生 C# 与 .NET Framework 4.x，**无需安装任何庞大的 Visual Studio 或依赖包**，使用系统自带的 `csc.exe` 即可极速编译：

```powershell
git clone https://github.com/francodeyvison183-collab/iClock.git
cd iClock
.\build.ps1
```
> 编译耗时不到 1 秒，产物输出在 `dist/iClock.exe`（单文件约 202 KB）。详见[构建说明](docs/BUILDING.md)。

---

## 🔒 隐私与安全说明

* **存储位置**：所有配置文件与历史记录均存放在本地 `%APPDATA%\iClock`，卸载时直接删除该文件夹即可。
* **系统修改**：仅当在设置中显式勾选“开机自启动”时，才会写入当前用户的注册表自启项 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`。
* **零网络通信**：代码无任何 Socket、HTTP 或遥测 API 调用，可完全断网运行。详见[隐私说明](docs/PRIVACY.md)。

---

## 💖 支持与赞赏

如果您觉得 iClock 让您的桌面更清爽、工作更专注，欢迎为本项目点亮一颗 ⭐️ **Star**，也欢迎请作者喝一杯咖啡鼓励持续维护：

<div align="center">
  <img src="src/zan.jpg" width="220" alt="微信赞赏码" />
  <p><b>微信扫一扫 赞赏作者</b></p>
  <p><i>感谢每一位支持开源创作的朋友！</i></p>
</div>

---

## 📄 开源许可

本项目基于 [MIT License](LICENSE) 开源许可，自由商用与个人使用。欢迎提交 Issue 与 Pull Request！
