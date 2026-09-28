# Tommi

English | [简体中文](README.zh-CN.md)

[![Windows quality](https://github.com/Dante9k/tommi/actions/workflows/ci.yml/badge.svg)](https://github.com/Dante9k/tommi/actions/workflows/ci.yml)

**One tomato. One uninterrupted moment of focus.**

A transparent desktop Pomodoro timer for Windows and macOS, with a rounded clay-style tomato, soft lighting, and inertial time wheels. When time is up, little tomatoes fly from the widget's current position across your desktop to remind you to take a break.

**Mac edition:** [Install and use the native macOS app](https://github.com/Dante9k/tommi/tree/main/macOS) · macOS 13+, Apple Silicon and Intel. The first Mac package is ad-hoc signed and has not been notarized. The Windows instructions follow below.

![Tomato Focus desktop widget](docs/images/preview.png)

## How to use

1. Set **hours, minutes, and seconds** in the center of the tomato. Scroll, drag vertically, or click adjacent numbers. Use Tab to switch columns, arrow keys to adjust, or type digits directly.
2. **Double-click the green stem to start.** Over 460 ms, the widget shrinks toward its own upper-right corner to 125 × 125 logical pixels, fades the fruit to 32% opacity, and stays on top. The editing controls disappear, leaving rounded, slightly wide and flat cutout digits with a soft white inner glow. Times under an hour show minutes and seconds; longer times include hours. Canceling restores the original size and color. Double-clicking the lower fruit or pressing Enter also starts a timer; double-clicking during focus does not restart it.
3. When time is up, the tomato stays at its current location, smoothly returns to reminder size, and throws little tomatoes from its stem across the desktop. Quiet launch sounds and soft landing sounds follow the animation; a second landing is quieter, with stereo position matching the image.
4. **Drag or double-click the large tomato to stop the reminder**, or press Esc.

The notification-area icon lets you bring back the widget, check the remaining time, cancel focus, or quit. While focusing, drag and quickly shake the fruit back and forth to cancel; an ordinary drag only moves it. Right-click the tray icon or tomato, or click `···`, to open the dark green settings panel. It includes 25/5/15-minute presets, separate switches for the completion chime, wheel clicks, and throw/landing sounds, plus an eight-second animated preview with sound. Stopping a reminder also stops its sounds.

The editing window is **220 × 220 logical pixels**, with proportionally scaled digits and time columns. It displays `00:25:00` without visible unit labels; tooltips and accessibility names still identify each column. Durations range from one second to 23:59:59. Digits rotate along a curved wheel and settle on whole values after an inertial scroll, with original mechanical detent sounds at each step. Disabling system animations makes the values switch directly. The completion chime plays once; the reminder animation continues until stopped.

On compatible Windows versions and hardware, settings also show an independent haptic-feedback option. Ordinary mice and older Windows versions use sound and visual feedback. The fallback has been verified on Windows 10; actual haptics have not yet been validated on compatible hardware. See [Microsoft's haptics documentation](https://learn.microsoft.com/en-us/windows/apps/develop/input/haptics).

## Requirements and installation

The Windows interface supports **English and Simplified Chinese**. Right-click the tomato or its tray icon, then choose **Language → English / 简体中文 / System**. The change applies immediately without restarting the timer. System mode uses Chinese on Chinese Windows and English otherwise. The installer has its own English / 简体中文 buttons. [Preview the English settings](docs/images/settings-panel-en.png).

Download the [Windows installer](https://github.com/Dante9k/tommi/releases/download/v1.1.19/Tommi-1.1.19-Setup.exe) or [portable ZIP](https://github.com/Dante9k/tommi/releases/download/v1.1.19/Tommi-1.1.19-win-x64.zip).

- Windows 10 or 11, x64, with .NET Framework 4.8.
- The graphical installer accepts an editable full path and has a **Browse** button for selecting a dedicated empty folder. The default is `%LOCALAPPDATA%/Programs/TomatoFocus/1.1.19`; other writable folders on local drives are supported, including paths containing spaces or Chinese characters. The displayed path is the exact destination, with no extra subfolder appended.
- An existing empty folder can be used. An existing installation is reused only when every file matches the package. Other nonempty folders are preserved; choose a new version folder when upgrading. The installer runs with your current user permissions and asks you to choose another location if it cannot write there.
- For the portable version, extract the ZIP and open `Tomato.exe`. No installation or administrator permission is required.
- The desktop app needs no internet connection and includes no account login or telemetry. Launch at login defaults on when you first open the app; turn it off in Tommi settings. This also applies to the portable edition.

Quit a running copy through its tray menu before installing. The installer creates a Start menu shortcut and optionally a desktop shortcut, backing up existing shortcuts with the same name. It does not launch the app automatically or change saved timers. This lightweight installer does not register in Windows Installed Apps: to uninstall, turn off launch at login in Tommi settings, quit the app, then remove its installation folder and shortcuts. Your settings are retained by default.

The app and installer are currently unsigned. For performance measurements and the limits of mixed-DPI, multi-monitor, and accessibility verification, see the [validation record](docs/VALIDATION.md).

## Build

The desktop project has no third-party NuGet dependencies. Build with the .NET Framework compiler included in Windows:

```powershell
./build.ps1 -Test
./package.ps1
./scripts/check-package.ps1
# Install to the current user's default location and create shortcuts.
# First quit any running version through its tray menu.
./scripts/install-local.ps1 -Launch
```

| Output | Location |
| --- | --- |
| Desktop application | `build/Tomato.exe` |
| Developer verification tool | `build/Tomato.Verify.exe` |
| Distributable app and bilingual documentation | `dist/Tommi-1.1.19-win-x64/` |
| Portable ZIP and SHA-256 checksum | `dist/` |
| Windows graphical installer | `dist/Tommi-<version>-Setup.exe`, generated by `./package.ps1` |
| Website ZIP | `dist/`, generated by `./scripts/build-website.ps1` |

Release archives use an explicit file allowlist. They exclude verification tools, runtime state, logs, debug symbols, and development caches. Neither `build/` nor `dist/` is tracked in Git.

The developer helper `install-local.ps1` still uses the fixed `%LOCALAPPDATA%/Programs/TomatoFocus/<version>` location; use the graphical installer to choose a custom folder. The helper backs up settings and shortcut information, retains previous versions, and records rollback paths in the parent folder's `backups` directory. Existing launch-at-login choices are preserved. Windows startup settings remain authoritative; the app does not override system-disabled startup entries or recreate entries removed outside the app. Antivirus settings are never changed. If security software blocks a file, preserve the exact alert for investigation rather than disabling protection or adding an automatic exclusion.

Alternatively, open **`Tomato.Focus.sln`** in Visual Studio with the .NET Framework 4.8 development tools installed, and build for x64.

## Verification

```powershell
# Logic checks without visible windows
./build/Tomato.Verify.exe --self-test

# Render the actual editing and reminder controls
./build/Tomato.Verify.exe --render-preview

# Requires an interactive desktop; shows about 17 seconds of animation
./build/Tomato.Verify.exe --smoke-test
./build/Tomato.Verify.exe --wheel-smoke
./build/Tomato.Verify.exe --language-smoke

# Verify custom installation and shortcuts in isolated temporary folders
./build/Tomato.Verify.exe --installer-test ./dist/Tommi-1.1.19-Setup.exe
```

Results are written to `build/*-results.txt`. Desktop checks use separate state files and leave everyday timers untouched. Installer checks cover spaces and Chinese characters, new and existing empty folders, repeat installation, nonempty-folder protection, unwritable locations, and shortcuts targeting a custom path. These checks call application actions and do not replace physical mouse or touch testing.

GitHub Actions builds on Windows, runs logic checks, renders controls, packages the app, and verifies the outputs. It retains installers, portable packages, and checksums. Brand icons are generated from `assets/brand/tomato-focus.png` for the application, shortcuts, and installer. Versions come from `VERSION`. Desktop animation acceptance testing requires an interactive Windows session.

Pushes to `main` publish the verified website bundle to GitHub Pages. A `v<version>` tag matching `VERSION` creates a GitHub Release with the installer, portable package, website bundle, and their SHA-256 checksums.

## Project layout

```text
Tomato.Focus.sln
src/Tomato.Focus/
  Application/       Lifecycle, tray integration, timer coordination
  Domain/            Countdown state and motion physics
  Infrastructure/    State storage, preferences, Windows integration
  Presentation/      Windows, time wheels, animation, artwork
  Properties/        Application metadata
tests/Tomato.Focus.Verification/
scripts/             Build, packaging, installer, verification
assets/              Embedded images, icons, audio, provenance
docs/                Architecture, validation records, previews
website/             Website source, downloads, deployment templates
marketing/           Promotional video, cover, and copy source
.github/             Workflows and issue/PR templates
```

Generated content is kept in `build/` for compilation output, `artifacts/` for verification screenshots, and `dist/` for deliverables and promotional media. Website builds generate `website/assets/`, `website/downloads/`, and `website/release.js`; the website ZIP contains only the current version's allowlisted files and per-file hashes. These outputs are excluded from Git. Installed older versions and user settings are outside source cleanup.

Old release packages, obsolete artwork, and duplicate projects are not kept in the source tree; previously committed content remains available in Git history. See the [website guide](website/README.md) and [deployment and rollback guide](website/deploy/RUNBOOK.md). The repository excludes private server operations records.

## State and privacy

Timers use a UTC deadline, so time spent asleep counts toward the countdown. The app does not wake the computer. Reopening restores an unexpired timer or shows a reminder for an expired one; no background alert runs while the app is closed.

State is stored in the current user's `LocalApplicationData/TomatoFocus/state.xml`, using a temporary file and atomic replacement. Corrupt settings fall back to defaults. Error logs stay on the local machine. The transparent animation overlay allows clicks through; stopping the reminder unregisters its shortcut and releases animation resources.

## Developer documentation

The detailed guides below are currently in Chinese.

- [Architecture](docs/ARCHITECTURE.md)
- [Validation record](docs/VALIDATION.md)
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)
- [Changelog](CHANGELOG.md)
- [Release operations ledger](docs/RELEASES.md)
- [Artwork and source prompts](assets/ARTWORK.md)

This project currently reserves **all rights** and does not grant an open-source license by default. See [LICENSE](LICENSE).
