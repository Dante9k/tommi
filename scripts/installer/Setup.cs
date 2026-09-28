using Tomato;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;

[assembly: AssemblyTitle("Tommi · Setup")]
[assembly: AssemblyDescription("Tommi Windows Setup")]
[assembly: AssemblyProduct("Tommi")]
[assembly: AssemblyCopyright("Tommi · All rights reserved")]

internal static class Setup
{
    private static readonly string[] Files = { "Tomato.exe", "Tomato.exe.config", "README.md", "README.zh-CN.md", "LICENSE", "CHANGELOG.md", "VALIDATION.md", "preview.png" };

    private static string ResourceText(string name)
    {
        using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(name))) return reader.ReadToEnd().Trim();
    }

    private static byte[] Payload()
    {
        byte[] bytes;
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.zip"))
        using (var memory = new MemoryStream()) { stream.CopyTo(memory); bytes = memory.ToArray(); }
        using (var sha = SHA256.Create())
        {
            var hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            if (!String.Equals(hash, ResourceText("Payload.sha256"), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(Texts.Get("setup.checksum"));
        }
        using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
        {
            if (!zip.Entries.Select(e => e.FullName).OrderBy(n => n).SequenceEqual(Files.OrderBy(n => n))) throw new InvalidDataException(Texts.Get("setup.files"));
            // Read every entry to catch truncated compressed data before changing the installation.
            foreach (var entry in zip.Entries) using (var stream = entry.Open()) { stream.CopyTo(Stream.Null); }
        }
        return bytes;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length >= 2 && args[0] == "--language")
            {
                if (args[1] != "en" && args[1] != "zh-CN") return 2;
                Texts.SetLanguage(args[1]);
                args = args.Skip(2).ToArray();
            }
            byte[] bytes = Payload();
            string version = ResourceText("Version.txt");
            if (!System.Text.RegularExpressions.Regex.IsMatch(version, @"^\d+\.\d+\.\d+$")) throw new InvalidDataException(Texts.Get("setup.versionInvalid"));
            if (args.Length == 1 && args[0] == "--verify-payload") return 0;
            bool render = args.Length == 2 && args[0] == "--render-preview";
            if (args.Length != 0 && !render) return 2;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var form = CreateForm(version, bytes))
            {
                if (render)
                {
                    CreateHandles(form);
                    form.PerformLayout();
                    using (var preview = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(preview, new Rectangle(Point.Empty, preview.Size));
                        preview.Save(Path.GetFullPath(args[1]), System.Drawing.Imaging.ImageFormat.Png);
                    }
                }
                else Application.Run(form);
            }
            return 0;
        }
        catch (Exception ex) { if (args.Length == 0) MessageBox.Show(ex.Message, Texts.Get("setup.title"), MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
    }

    private static void CreateHandles(Control control)
    {
        // WM_PRINT needs child HWNDs even though the preview never shows a window.
        var handle = control.Handle;
        foreach (Control child in control.Controls) CreateHandles(child);
    }

    private static Form CreateForm(string version, byte[] bytes)
    {
        Form form = null;
        form = CreateInstallForm(version,
            delegate (string target, bool desktop) { Install(bytes, target, desktop); },
            delegate { return Process.GetProcessesByName("Tomato").Length != 0; },
            delegate (Exception error) { MessageBox.Show(form, error.GetBaseException().Message, Texts.Get("setup.failed"), MessageBoxButtons.OK, MessageBoxIcon.Warning); });
        return form;
    }

    private static Form CreateInstallForm(string version, Action<string, bool> installAction, Func<bool> isRunning, Action<Exception> reportError)
    {
        var ink = Color.FromArgb(36, 44, 56);
        var muted = Color.FromArgb(95, 105, 119);
        var red = Color.FromArgb(207, 59, 40);
        var form = new Form
        {
            Text = Texts.Get("setup.title"),
            Font = new Font("Segoe UI", 10),
            AutoScaleDimensions = new SizeF(96, 96),
            AutoScaleMode = AutoScaleMode.Dpi,
            ClientSize = new Size(760, 500),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            StartPosition = FormStartPosition.CenterScreen,
            BackColor = Color.White,
            ForeColor = ink,
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
        };
        var side = new Panel { Location = Point.Empty, Size = new Size(246, 500), BackColor = Color.FromArgb(255, 243, 238) };
        var logo = new PictureBox { Location = new Point(45, 49), Size = new Size(152, 152), SizeMode = PictureBoxSizeMode.Zoom, TabStop = false };
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Brand.Logo.png"))
        using (var image = Image.FromStream(stream)) logo.Image = new Bitmap(image);
        side.Controls.Add(logo);
        side.Controls.Add(TextLabel("Tommi", 36, 221, 200, 54, 26, ink, true));
        side.Controls.Add(TextLabel("FOCUS TIMER", 43, 279, 190, 24, 10, muted));
        var tagline = TextLabel(Texts.Get("setup.tagline"), 45, 331, 176, 66, 12, muted);
        side.Controls.Add(tagline);
        side.Controls.Add(TextLabel("WINDOWS 10 / 11 · x64", 45, 452, 195, 22, 9, muted));
        form.Controls.Add(side);
        var title = TextLabel(Texts.Get("setup.welcome"), 292, 49, 425, 46, 23, ink, true);
        var description = TextLabel(Texts.Get("setup.description"), 295, 108, 410, 62, 11, muted);
        var versionLabel = TextLabel(Texts.Get("setup.versionPrefix") + version + Texts.Get("setup.versionSuffix"), 295, 182, 410, 28, 10, muted);
        var separator = new Panel { Location = new Point(296, 227), Size = new Size(412, 1), BackColor = Color.FromArgb(232, 234, 238) };
        var pathLabel = TextLabel(Texts.Get("setup.location"), 295, 249, 410, 24, 10, ink, true);
        string target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "TomatoFocus", version);
        var path = new TextBox { Name = "InstallationPath", Text = target, AutoSize = false, Font = new Font("Segoe UI", 9), Location = new Point(297, 282), Size = new Size(306, 32), BackColor = Color.FromArgb(248, 249, 251), BorderStyle = BorderStyle.FixedSingle, AccessibleName = Texts.Get("setup.path"), TabIndex = 0 };
        var browse = new Button { Name = "BrowseFolder", Text = Texts.Get("setup.browse"), Location = new Point(617, 282), Size = new Size(91, 32), BackColor = Color.White, FlatStyle = FlatStyle.Flat, TabIndex = 1, AccessibleName = Texts.Get("setup.folder") };
        browse.FlatAppearance.BorderColor = Color.FromArgb(220, 225, 232);
        browse.Click += delegate
        {
            using (var dialog = new FolderBrowserDialog { Description = Texts.Get("setup.folderHint"), ShowNewFolderButton = true })
            {
                try
                {
                    string initial = NormalizeTarget(path.Text);
                    while (!String.IsNullOrEmpty(initial) && !Directory.Exists(initial)) initial = Path.GetDirectoryName(initial);
                    if (!String.IsNullOrEmpty(initial)) dialog.SelectedPath = initial;
                }
                catch (Exception ex)
                {
                    if (!(ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException)) throw;
                }
                if (dialog.ShowDialog(form) == DialogResult.OK) { path.Text = dialog.SelectedPath; path.Focus(); }
            }
        };
        var pathHint = TextLabel(Texts.Get("setup.pathHint"), 295, 320, 415, 23, 9, muted);
        var desktop = new CheckBox { Text = Texts.Get("setup.desktop"), Checked = true, Location = new Point(296, 352), AutoSize = true, TabIndex = 2 };
        var status = TextLabel(Texts.Get("setup.startup"), 295, 387, 415, 35, 9, muted);
        var install = new Button { Name = "InstallButton", Text = Texts.Get("setup.install"), Location = new Point(558, 431), Size = new Size(150, 43), BackColor = red, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, TabIndex = 3 };
        install.FlatAppearance.BorderSize = 0;
        install.FlatAppearance.MouseOverBackColor = Color.FromArgb(181, 47, 31);
        var cancel = new Button { Text = Texts.Get("setup.cancel"), Location = new Point(445, 431), Size = new Size(95, 43), BackColor = Color.White, FlatStyle = FlatStyle.Flat, TabIndex = 4 };
        cancel.FlatAppearance.BorderColor = Color.FromArgb(220, 225, 232);
        cancel.Click += delegate { form.Close(); };
        bool installed = false;
        var english = new Button { Name = "LanguageEnglish", Text = "English", Location = new Point(534, 12), Size = new Size(82, 27), FlatStyle = FlatStyle.Flat, TabIndex = 5 };
        var chinese = new Button { Name = "LanguageChinese", Text = "简体中文", Location = new Point(622, 12), Size = new Size(86, 27), FlatStyle = FlatStyle.Flat, TabIndex = 6 };
        english.FlatAppearance.BorderSize = chinese.FlatAppearance.BorderSize = 0;
        Action refreshLanguage = delegate
        {
            form.Text = Texts.Get("setup.title");
            tagline.Text = Texts.Get("setup.tagline");
            title.Text = Texts.Get(installed ? "setup.ready" : "setup.welcome");
            description.Text = Texts.Get(installed ? "setup.open" : "setup.description");
            versionLabel.Text = Texts.Get("setup.versionPrefix") + version + Texts.Get("setup.versionSuffix");
            pathLabel.Text = Texts.Get("setup.location");
            path.AccessibleName = Texts.Get("setup.path");
            browse.Text = Texts.Get("setup.browse");
            browse.AccessibleName = Texts.Get("setup.folder");
            pathHint.Text = Texts.Get("setup.pathHint");
            desktop.Text = Texts.Get("setup.desktop");
            status.Text = Texts.Get(installed ? "setup.remove" : "setup.startup");
            install.Text = Texts.Get(installed ? "setup.done" : "setup.install");
            cancel.Text = Texts.Get("setup.cancel");
            english.BackColor = Texts.Chinese ? Color.White : Color.FromArgb(255, 230, 220);
            chinese.BackColor = Texts.Chinese ? Color.FromArgb(255, 230, 220) : Color.White;
        };
        english.Click += delegate { Texts.SetLanguage("en"); refreshLanguage(); };
        chinese.Click += delegate { Texts.SetLanguage("zh-CN"); refreshLanguage(); };
        form.Controls.AddRange(new Control[] { english, chinese });
        refreshLanguage();
        install.Click += delegate
        {
            if (installed) { form.Close(); return; }
            install.Enabled = false;
            path.ReadOnly = true;
            browse.Enabled = false;
            try
            {
                if (isRunning()) throw new IOException(Texts.Get("setup.running"));
                target = NormalizeTarget(path.Text);
                installAction(target, desktop.Checked);
                path.Text = target;
                installed = true;
                title.Text = Texts.Get("setup.ready");
                description.Text = Texts.Get("setup.open");
                install.Text = Texts.Get("setup.done");
                desktop.Enabled = false;
                cancel.Visible = false;
                status.Text = Texts.Get("setup.remove");
            }
            catch (UnauthorizedAccessException) { reportError(new IOException(Texts.Get("setup.denied"))); }
            catch (Exception ex) { reportError(ex); }
            finally { install.Enabled = true; path.ReadOnly = installed; browse.Enabled = !installed; }
        };
        form.Controls.AddRange(new Control[] { title, description, versionLabel, separator, pathLabel, path, browse, pathHint, desktop, status, install, cancel });
        form.AcceptButton = install;
        form.CancelButton = cancel;
        form.Disposed += delegate { logo.Image.Dispose(); form.Icon.Dispose(); };
        return form;
    }

    private static Label TextLabel(string text, int x, int y, int width, int height, float size, Color color, bool bold = false)
    {
        return new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height),
            Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = color, BackColor = Color.Transparent };
    }

    private static void Install(byte[] bytes, string target, bool desktop)
    {
        target = NormalizeTarget(target);
        InstallFiles(bytes, target);
        Shortcut(Environment.GetFolderPath(Environment.SpecialFolder.Programs), target);
        if (desktop) Shortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), target);
    }

    private static string NormalizeTarget(string input)
    {
        string target = (input ?? "").Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(target, @"^[A-Za-z]:[\\/]"))
            throw new IOException(Texts.Get("setup.fullPath"));
        foreach (var part in target.Substring(3).Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.EndsWith(".") || part.EndsWith(" ") ||
                System.Text.RegularExpressions.Regex.IsMatch(part, @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                throw new IOException(Texts.Get("setup.invalidName"));
        }
        target = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (target.Length <= 3) throw new IOException(Texts.Get("setup.root"));
        if (target.Length + 1 + Files.Max(name => name.Length) >= 260) throw new IOException(Texts.Get("setup.pathLong"));
        if (File.Exists(target)) throw new IOException(Texts.Get("setup.isFile"));
        EnsureOrdinaryDirectories(target);
        return target;
    }

    private static void EnsureOrdinaryDirectories(string target)
    {
        for (var directory = new DirectoryInfo(target); directory != null; directory = directory.Parent)
        {
            if (File.Exists(directory.FullName)) throw new IOException(Texts.Get("setup.parentFile"));
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException(Texts.Get("setup.directoryLink"));
        }
    }

    private static void InstallFiles(byte[] bytes, string target)
    {
        target = NormalizeTarget(target);
        // Existing versions are immutable: never overwrite a user's different executable or files.
        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
        {
            if (!Directory.GetFiles(target).Select(Path.GetFileName).OrderBy(n => n).SequenceEqual(Files.OrderBy(n => n)) || Directory.GetDirectories(target).Length != 0)
                throw new IOException(Texts.Get("setup.nonempty"));
            if (Directory.GetFiles(target).Any(file => (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0))
                throw new IOException(Texts.Get("setup.fileLink"));
            using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
            foreach (var entry in zip.Entries)
                using (var source = entry.Open())
                using (var existing = File.OpenRead(Path.Combine(target, entry.FullName)))
                using (var sha = SHA256.Create())
                    if (!sha.ComputeHash(source).SequenceEqual(sha.ComputeHash(existing))) throw new IOException(Texts.Get("setup.different"));
        }
        else
        {
            string root = Path.GetDirectoryName(target);
            Directory.CreateDirectory(root);
            EnsureOrdinaryDirectories(root);
            // A sibling staging folder keeps the final move on the selected volume.
            string staging = Path.Combine(root, ".tomato-install-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                using (var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
                foreach (var entry in zip.Entries) entry.ExtractToFile(Path.Combine(staging, entry.FullName));
                EnsureOrdinaryDirectories(target);
                if (Directory.Exists(target)) Directory.Delete(target, false); // Fails if another process added a file.
                Directory.Move(staging, target);
            }
            finally
            {
                if (Directory.Exists(staging))
                {
                    foreach (var name in Files) { string file = Path.Combine(staging, name); if (File.Exists(file)) File.Delete(file); }
                    Directory.Delete(staging, false);
                }
            }
        }
    }

    private static void Shortcut(string folder, string target)
    {
        Directory.CreateDirectory(folder);
        var linkPath = Path.Combine(folder, "Tommi.lnk");
        if (File.Exists(linkPath))
        {
            var backupRoot = Path.Combine(Path.GetDirectoryName(target), "shortcut-backups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"));
            Directory.CreateDirectory(backupRoot);
            File.Copy(linkPath, Path.Combine(backupRoot, "Tommi.lnk"));
        }
        var link = (IShellLinkW)new ShellLink();
        try
        {
            link.SetPath(Path.Combine(target, "Tomato.exe"));
            link.SetWorkingDirectory(target);
            link.SetArguments("");
            link.SetIconLocation(Path.Combine(target, "Tomato.exe"), 0);
            ((IPersistFile)link).Save(linkPath, true);
        }
        finally { Marshal.FinalReleaseComObject(link); }
        var saved = ReadShortcut(linkPath);
        if (!String.Equals(saved[0], Path.Combine(target, "Tomato.exe"), StringComparison.OrdinalIgnoreCase) ||
            !String.Equals(saved[1], target, StringComparison.OrdinalIgnoreCase))
            throw new IOException(Texts.Get("setup.shortcut"));
        // Retire only the known legacy app link, after the new link has been verified.
        string legacy = Path.Combine(folder, "朱果番茄钟.lnk");
        if (File.Exists(legacy) && String.Equals(Path.GetFileName(ReadShortcut(legacy)[0]), "Tomato.exe", StringComparison.OrdinalIgnoreCase))
        {
            string backup = Path.Combine(Path.GetDirectoryName(target), "shortcut-backups", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backup);
            File.Move(legacy, Path.Combine(backup, Path.GetFileName(legacy)));
        }
    }

    private static string[] ReadShortcut(string path)
    {
        var link = (IShellLinkW)new ShellLink();
        try
        {
            ((IPersistFile)link).Load(path, 0);
            var target = new StringBuilder(1024);
            var directory = new StringBuilder(1024);
            link.GetPath(target, target.Capacity, IntPtr.Zero, 0);
            link.GetWorkingDirectory(directory, directory.Capacity);
            return new[] { target.ToString(), directory.ToString() };
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }

    // Explicit Unicode interfaces preserve Chinese link names on English Windows.
    // https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-ishelllinkw
    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int capacity, IntPtr findData, uint flags);
        void GetIDList(out IntPtr idList);
        void SetIDList(IntPtr idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description, int capacity);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int capacity);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string path);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int capacity);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int capacity, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr owner, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}
