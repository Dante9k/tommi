using System;
using System.Collections.Generic;
using System.Globalization;

namespace Tomato
{
    // Shared by the app and its standalone installer; stable keys keep UI code language-neutral.
    public static class Texts
    {
        static string language = "system";
        public static string Language
        {
            get
            {
                return language;
            }
        }

        public static string Normalize(string value)
        {
            return value == "en" || value == "zh-CN" ? value : "system";
        }

        public static string Resolve(string value, CultureInfo systemCulture)
        {
            value = Normalize(value);
            return value == "system" ? (systemCulture.TwoLetterISOLanguageName == "zh" ? "zh-CN" : "en") : value;
        }

        public static void SetLanguage(string value)
        {
            language = Normalize(value);
        }

        public static bool Chinese
        {
            get
            {
                return Resolve(language, CultureInfo.CurrentUICulture) == "zh-CN";
            }
        }

        public static string FontFamily
        {
            get
            {
                return Chinese ? "Microsoft YaHei UI" : "Segoe UI";
            }
        }

        static readonly Dictionary<string, string[]> catalog = new Dictionary<string, string[]>
        {
            {
                "app.title",
                new[]
                {
                    "Tommi · 番茄钟",
                    "Tommi · Focus timer"
                }
            },
            {
                "app.alreadyRunning",
                new[]
                {
                    "Tommi 已经在运行。请双击任务栏右下角的番茄图标。",
                    "Tommi is already running. Double-click the tomato in the system tray."
                }
            },
            {
                "app.error",
                new[]
                {
                    "Tommi遇到问题，已记录错误。请重新打开软件。",
                    "Tommi encountered a problem and recorded the error. Please reopen the app."
                }
            },
            {
                "app.startFailed",
                new[]
                {
                    "启动失败：",
                    "Unable to start: "
                }
            },
            {
                "startup.system",
                new[]
                {
                    "Windows“启动应用”的设置也会生效。",
                    "Windows Startup Apps settings also apply."
                }
            },
            {
                "startup.saveFailed",
                new[]
                {
                    "登录启动设置未能保存，请检查系统权限。",
                    "Could not save launch-at-login settings. Check your system permissions."
                }
            },
            {
                "startup.invalidPath",
                new[]
                {
                    "登录启动需要有效的程序路径。",
                    "Launch at login requires a valid application path."
                }
            },
            {
                "startup.pathLong",
                new[]
                {
                    "程序路径过长，无法设置登录启动。请安装到较短的路径。",
                    "The application path is too long for launch at login. Choose a shorter installation path."
                }
            },
            {
                "startup.error",
                new[]
                {
                    "登录启动设置未完成；可重新切换开关。",
                    "Could not update launch at login. Toggle the switch to retry."
                }
            },
            {
                "timer.empty",
                new[]
                {
                    "先设置一点专注时间",
                    "Set a focus duration first"
                }
            },
            {
                "timer.previewBusy",
                new[]
                {
                    "请先取消当前计时，再预览",
                    "End your focus session to preview"
                }
            },
            {
                "tray.saveFailed",
                new[]
                {
                    "Tommi · 设置保存失败，本次计时继续",
                    "Tommi · Could not save settings; timer continues"
                }
            },
            {
                "tray.running",
                new[]
                {
                    "Tommi · 专注剩余 ",
                    "Tommi · Focus remaining "
                }
            },
            {
                "tray.alarm",
                new[]
                {
                    "Tommi · 时间到了，拖动番茄结束提醒",
                    "Tommi · Time's up! Move the tomato to dismiss"
                }
            },
            {
                "art.missing",
                new[]
                {
                    "缺少番茄图像资源。",
                    "The tomato image resource is missing."
                }
            },
            {
                "throw.title",
                new[]
                {
                    "Tommi · 投掷动画",
                    "Tommi · Tomato shower"
                }
            },
            {
                "widget.tooltip",
                new[]
                {
                    "双击绿蒂开始 · 拖动果身移动 · 专注时摇晃取消",
                    "Double-click the stem to start · Drag to move · Shake to cancel focus"
                }
            },
            {
                "unit.hours",
                new[]
                {
                    "小时",
                    "Hours"
                }
            },
            {
                "unit.minutes",
                new[]
                {
                    "分钟",
                    "Minutes"
                }
            },
            {
                "unit.seconds",
                new[]
                {
                    "秒",
                    "Seconds"
                }
            },
            {
                "countdown.caption",
                new[]
                {
                    "剩余专注时间",
                    "Focus time remaining"
                }
            },
            {
                "menu.tooltip",
                new[]
                {
                    "预设、试听与退出",
                    "Presets, sound preview and quit"
                }
            },
            {
                "menu.name",
                new[]
                {
                    "菜单",
                    "Menu"
                }
            },
            {
                "alarm.dismiss",
                new[]
                {
                    "拖动或双击，结束提醒",
                    "Drag or double-click to dismiss"
                }
            },
            {
                "alarm.title",
                new[]
                {
                    "时间到了",
                    "Time's up"
                }
            },
            {
                "alarm.subtitle",
                new[]
                {
                    "休息一下",
                    "Take a break"
                }
            },
            {
                "timer.cancelled",
                new[]
                {
                    "已取消专注",
                    "Focus cancelled"
                }
            },
            {
                "timer.running",
                new[]
                {
                    "专注中 · 摇晃取消",
                    "Focusing · Shake to cancel"
                }
            },
            {
                "settings.title",
                new[]
                {
                    "Tommi · 专注偏好",
                    "Tommi · Preferences"
                }
            },
            {
                "settings.tagline",
                new[]
                {
                    "专注，有自己的节奏。",
                    "Focus at your own pace."
                }
            },
            {
                "settings.breakStatus",
                new[]
                {
                    "●  休息时间到了",
                    "●  Time for a break"
                }
            },
            {
                "settings.focusStatus",
                new[]
                {
                    "●  专注进行中",
                    "●  Focus in progress"
                }
            },
            {
                "settings.readyStatus",
                new[]
                {
                    "●  准备好，开始一段专注",
                    "●  Ready for a little focus"
                }
            },
            {
                "settings.duration",
                new[]
                {
                    "选择一段时间",
                    "Choose a duration"
                }
            },
            {
                "preset.focus",
                new[]
                {
                    "专注",
                    "Focus"
                }
            },
            {
                "preset.short",
                new[]
                {
                    "短歇",
                    "Short"
                }
            },
            {
                "preset.long",
                new[]
                {
                    "长休",
                    "Long"
                }
            },
            {
                "settings.chime",
                new[]
                {
                    "到时轻提醒",
                    "Gentle end chime"
                }
            },
            {
                "settings.wheel",
                new[]
                {
                    "拨轮卡点声",
                    "Wheel clicks"
                }
            },
            {
                "settings.effects",
                new[]
                {
                    "投掷与落地声",
                    "Throw and landing sounds"
                }
            },
            {
                "settings.haptics",
                new[]
                {
                    "轻触反馈",
                    "Haptic feedback"
                }
            },
            {
                "settings.startup",
                new[]
                {
                    "登录时启动 Tommi",
                    "Launch Tommi at login"
                }
            },
            {
                "settings.preview",
                new[]
                {
                    "试试番茄雨   ↗",
                    "Try a tomato shower   ↗"
                }
            },
            {
                "settings.previewBusy",
                new[]
                {
                    "结束当前专注后可试听",
                    "Preview is available after your focus session"
                }
            },
            {
                "settings.show",
                new[]
                {
                    "回到番茄",
                    "Back to the tomato"
                }
            },
            {
                "settings.dismiss",
                new[]
                {
                    "结束提醒",
                    "Dismiss reminder"
                }
            },
            {
                "settings.cancel",
                new[]
                {
                    "取消专注",
                    "Cancel focus"
                }
            },
            {
                "settings.quit",
                new[]
                {
                    "退出Tommi",
                    "Quit Tommi"
                }
            },
            {
                "preset.suffix",
                new[]
                {
                    " · 分钟",
                    " · min"
                }
            },
            {
                "preset.accessibleSuffix",
                new[]
                {
                    " 分钟",
                    " minutes"
                }
            },
            {
                "wheel.locked",
                new[]
                {
                    "计时过程中不能编辑时间",
                    "You cannot edit the duration while the timer is running."
                }
            },
            {
                "language.label",
                new[]
                {
                    "界面语言",
                    "Language"
                }
            },
            {
                "language.system",
                new[]
                {
                    "跟随系统",
                    "System"
                }
            },
            {
                "wheel.tooltip",
                new[]
                {
                    "滚动或上下拖动调整{0}；方向键微调，也可直接输入数字",
                    "Scroll or drag to adjust {0}. Use arrow keys or type a number."
                }
            },
            {
                "countdown.accessible",
                new[]
                {
                    "剩余专注时间 {0} 时 {1} 分 {2} 秒",
                    "Focus time remaining: {0} hours, {1} minutes, {2} seconds"
                }
            },
            {
                "setup.title",
                new[]
                {
                    "Tommi安装程序",
                    "Tommi Setup"
                }
            },
            {
                "setup.failed",
                new[]
                {
                    "安装未完成",
                    "Installation incomplete"
                }
            },
            {
                "setup.checksum",
                new[]
                {
                    "安装包校验失败，请重新下载。",
                    "The installer checksum is invalid. Please download it again."
                }
            },
            {
                "setup.files",
                new[]
                {
                    "安装包文件清单无效。",
                    "The installer file list is invalid."
                }
            },
            {
                "setup.versionInvalid",
                new[]
                {
                    "版本信息无效。",
                    "The version information is invalid."
                }
            },
            {
                "setup.tagline",
                new[]
                {
                    "一颗番茄，\n一段完整的专注。",
                    "One little tomato.\nTime to focus."
                }
            },
            {
                "setup.welcome",
                new[]
                {
                    "欢迎安装Tommi",
                    "Welcome to Tommi"
                }
            },
            {
                "setup.description",
                new[]
                {
                    "把时间，留给喜欢的事。\n离线使用，无需登录。",
                    "Make time for what you love.\nWorks offline. No sign-in needed."
                }
            },
            {
                "setup.versionPrefix",
                new[]
                {
                    "版本 ",
                    "Version "
                }
            },
            {
                "setup.versionSuffix",
                new[]
                {
                    "    /    桌面番茄钟",
                    "    /    Desktop focus timer"
                }
            },
            {
                "setup.location",
                new[]
                {
                    "安装位置",
                    "Install location"
                }
            },
            {
                "setup.path",
                new[]
                {
                    "完整安装路径",
                    "Full installation path"
                }
            },
            {
                "setup.browse",
                new[]
                {
                    "浏览…",
                    "Browse…"
                }
            },
            {
                "setup.folder",
                new[]
                {
                    "选择安装文件夹",
                    "Choose an installation folder"
                }
            },
            {
                "setup.folderHint",
                new[]
                {
                    "选择用于Tommi的空文件夹，也可以新建文件夹。",
                    "Choose or create an empty folder for Tommi."
                }
            },
            {
                "setup.pathHint",
                new[]
                {
                    "可输入完整路径，或选择一个专用空文件夹。",
                    "Enter a full path or choose a dedicated empty folder."
                }
            },
            {
                "setup.desktop",
                new[]
                {
                    "创建桌面快捷方式",
                    "Create a desktop shortcut"
                }
            },
            {
                "setup.startup",
                new[]
                {
                    "首次打开默认随登录启动，可在 Tommi 设置中关闭。",
                    "Launch at login is on by default. Turn it off in Tommi settings."
                }
            },
            {
                "setup.install",
                new[]
                {
                    "开始安装",
                    "Install"
                }
            },
            {
                "setup.cancel",
                new[]
                {
                    "取消",
                    "Cancel"
                }
            },
            {
                "setup.running",
                new[]
                {
                    "请先从托盘退出Tommi，再点击安装。安装程序不会强制关闭应用。",
                    "Quit Tommi from the system tray, then click Install. Setup will not force the app to close."
                }
            },
            {
                "setup.ready",
                new[]
                {
                    "Tommi已准备就绪",
                    "Tommi is ready"
                }
            },
            {
                "setup.open",
                new[]
                {
                    "从开始菜单打开「Tommi」，\n开始你的下一段专注。",
                    "Open Tommi from the Start menu\nand begin your next focus session."
                }
            },
            {
                "setup.done",
                new[]
                {
                    "完成",
                    "Done"
                }
            },
            {
                "setup.remove",
                new[]
                {
                    "卸载前关闭登录启动，再退出并删除安装文件夹。",
                    "To remove Tommi, turn off launch at login, quit, then delete its folder."
                }
            },
            {
                "setup.denied",
                new[]
                {
                    "无法写入所选位置。请使用“浏览”选择有写入权限的文件夹。",
                    "Cannot write to this location. Use Browse to choose a folder you can write to."
                }
            },
            {
                "setup.fullPath",
                new[]
                {
                    "请输入本地磁盘上的完整路径，例如 D:\\Apps\\TomatoFocus。",
                    "Enter a full local path, such as D:\\Apps\\Tommi."
                }
            },
            {
                "setup.invalidName",
                new[]
                {
                    "安装路径包含无效的文件夹名称，请重新选择。",
                    "The path contains an invalid folder name. Choose another folder."
                }
            },
            {
                "setup.root",
                new[]
                {
                    "请在磁盘内选择一个专用文件夹，不要直接安装到磁盘根目录。",
                    "Choose a dedicated folder, not the root of a drive."
                }
            },
            {
                "setup.pathLong",
                new[]
                {
                    "安装路径太长，请选择层级更少的文件夹。",
                    "The installation path is too long. Choose a shorter path."
                }
            },
            {
                "setup.isFile",
                new[]
                {
                    "所选路径是一个文件，请选择文件夹。",
                    "The selected path is a file. Choose a folder."
                }
            },
            {
                "setup.parentFile",
                new[]
                {
                    "安装路径中有同名文件，请重新选择文件夹。",
                    "A file blocks this installation path. Choose another folder."
                }
            },
            {
                "setup.directoryLink",
                new[]
                {
                    "安装目录包含目录链接，请选择普通文件夹。",
                    "The path contains a directory link. Choose a regular folder."
                }
            },
            {
                "setup.nonempty",
                new[]
                {
                    "所选文件夹不是空的。原文件已保留，请新建文件夹，或选择与当前安装包完全相同的版本目录。",
                    "This folder is not empty. Your files are preserved. Choose an empty folder or an identical installation of this version."
                }
            },
            {
                "setup.fileLink",
                new[]
                {
                    "所选文件夹包含文件链接，请使用专用空文件夹。",
                    "This folder contains file links. Choose a dedicated empty folder."
                }
            },
            {
                "setup.different",
                new[]
                {
                    "现有文件与安装包不同，原文件已保留。请为新版选择其他文件夹。",
                    "The existing files differ from this package and have been preserved. Choose another folder."
                }
            },
            {
                "setup.shortcut",
                new[]
                {
                    "快捷方式校验失败。安装文件已保留，请检查所选路径后重试。",
                    "Shortcut verification failed. The installed files are preserved. Check the selected path and try again."
                }
            }
        };
        public static IEnumerable<string> Keys
        {
            get
            {
                return catalog.Keys;
            }
        }

        public static string Get(string key)
        {
            return catalog[key][Chinese ? 0 : 1];
        }

        public static string Format(string key, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, Get(key), args);
        }
    }
}
