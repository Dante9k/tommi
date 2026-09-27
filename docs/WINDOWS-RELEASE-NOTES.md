# Tommi 1.1.18 for Windows

- Wheel clicks follow the number actually drawn in each frame. Rapid input and reversals coalesce; presets and hidden controls stay silent.
- A prewarmed native audio stream replaces per-click asynchronous playback. Two approximately 5ms app buffers reduce scheduling delay while preserving all three original click sounds.
- Late clicks are discarded instead of queued. Hiding, muting or starting focus releases the wheel stream; reopening the editor prepares it again. Alarm and throwing audio remain independent.

Download `Tommi-1.1.18-Setup.exe` for the graphical installer, or the `win-x64.zip` for the portable edition. Quit the running app through its tray menu before upgrading. Installation does not open the app automatically. Both editions configure launch at login when first opened; disable this setting before removing the app. The executable remains `Tomato.exe` and the existing settings folder is retained for compatibility.

Windows 10/11 x64 and .NET Framework 4.8 are required. The app and installer are unsigned. Packages exclude developer verification tools. SHA-256 files and release evidence accompany the downloads.

---

## 简体中文

- 卡点声跟随每帧实际画出的数字变化；快速拨动和反向输入合并处理，预设和隐藏状态保持安静。
- 改用预热的原生音频通道，两个约 5ms 的程序侧缓冲降低调度延迟，保留原来的三组卡点音色。
- 过期卡点直接丢弃，不排队补播。隐藏、静音及开始专注时释放拨轮音频，恢复编辑后重新预热；到时与投掷音效保持独立。

下载 `Tommi-1.1.18-Setup.exe` 安装，或使用 `win-x64.zip` 便携版。升级前请从托盘正常退出旧程序。安装完成后需要手动打开应用；两个版本首次打开都会配置登录启动，卸载前请先关闭此选项。为兼容旧版，内部程序名仍为 `Tomato.exe`，设置目录保持不变。

支持 Windows 10/11 x64，需要 .NET Framework 4.8。程序与安装包暂未代码签名；正式包不包含开发验证工具，并附 SHA-256 和发布证据。
