# iClock

[English](README_EN.md) · [GitHub 提交指南](GITHUB_UPLOAD.md) · [使用指南](docs/USER_GUIDE.md) · [构建说明](docs/BUILDING.md) · [发布流程](docs/RELEASING.md) · [更新记录](CHANGELOG.md)

iClock 是一个轻量级 Windows 桌面倒计时工具。它以透明置顶文字显示倒计时，支持点击穿透、全局快捷键、系统托盘控制和开机自启动。

## 功能

- 透明、置顶的桌面文字；普通状态下鼠标点击穿透。
- 可设置倒计时时长、文字颜色和字号，提供 `HH:MM:SS`、`MM:SS` 与中文单位格式。
- 通过托盘菜单进入位置调整状态，拖动半透明区域定位文字。
- 可自定义全局启动/暂停快捷键，默认 `Ctrl+Alt+Space`。
- 倒计时结束时可播放系统声音并显示自定义托盘通知。
- 按日期查看完成、重置和中断记录。
- 托盘菜单提供语言选择，可选简体中文或 English；默认简体中文。可设置当前用户登录时自启动。
- 单文件程序；当前构建约 27 KB，不含安装向导。

## 运行要求

- Windows 10 或 Windows 11。
- .NET Framework 4.x。程序使用 Windows 自带 WinForms、GDI+ 和 Win32 API，不依赖第三方 NuGet 包。

## 获取与构建

**普通用户：**[前往 GitHub Releases 下载 Windows 版](../../releases/latest)，下载 `iClock-1.0.0-windows.zip`，解压后运行 `iClock.exe`，无需编译或安装。首次发布时，维护者需要将本项目提供的 ZIP 包上传为 Release 资产。

程序需要 Windows 和 .NET Framework 4.x；本项目不包含安装向导或运行时。若你要从源代码构建，`build.ps1` 会将程序生成到 `dist/iClock.exe`，详见[构建说明](docs/BUILDING.md)。

```powershell
.\build.ps1
```

程序启动后会显示设定时长，按快捷键开始计时。右键托盘图标可打开设置、查看今日记录，或从“语言”菜单选择简体中文或 English。

## 数据与隐私

设置和倒计时记录仅保存在当前 Windows 用户的 `%APPDATA%\iClock` 目录。程序不联网、不收集遥测数据。启用自启动时，只会修改当前用户的 Windows `Run` 注册表项。详见[隐私说明](docs/PRIVACY.md)。

## 参与贡献

欢迎提交问题和改进建议。提交前请阅读[贡献指南](CONTRIBUTING.md)和[行为准则](CODE_OF_CONDUCT.md)。安全问题请按[安全政策](SECURITY.md)私下报告。

## 许可

本项目采用 MIT License，详见 [LICENSE](LICENSE)。
