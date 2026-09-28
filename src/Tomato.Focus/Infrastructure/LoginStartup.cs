using System;
using System.IO;
using Microsoft.Win32;

namespace Tomato
{
    public interface IStartupRegistration
    {
        string Read();
        void Write(string command);
    }

    // Only this app's current-user Run value is touched. Windows startup approval stays authoritative.
    public sealed class RegistryStartupRegistration : IStartupRegistration
    {
        const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "Tommi";
        public string Read()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(KeyPath))
                return key == null ? null : key.GetValue(ValueName) as string;
        }

        public void Write(string command)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                if (command == null)
                    key.DeleteValue(ValueName, false);
                else
                    key.SetValue(ValueName, command, RegistryValueKind.String);
            }

            if (!String.Equals(Read(), command, StringComparison.Ordinal))
                throw new IOException(Texts.Get("startup.saveFailed"));
        }
    }

    public sealed class LoginStartup
    {
        readonly Preferences preferences;
        readonly IStartupRegistration registration;
        readonly string executable;
        public string Error { get; private set; }
        public bool Configured { get; private set; }

        public LoginStartup(Preferences preferences, IStartupRegistration registration, string executable)
        {
            this.preferences = preferences;
            this.registration = registration;
            this.executable = executable;
        }

        public static string Command(string executable)
        {
            if (String.IsNullOrWhiteSpace(executable) || !Path.IsPathRooted(executable) || executable.IndexOf('"') >= 0)
                throw new IOException(Texts.Get("startup.invalidPath"));
            string command = "\"" + Path.GetFullPath(executable) + "\" --startup";
            if (command.Length > 260)
                throw new IOException(Texts.Get("startup.pathLong"));
            return command;
        }

        public void Initialize()
        {
            bool first = !preferences.LoginStartupInitialized;
            // Attempt the default once; a denied request must not be retried at every launch.
            preferences.LoginStartupInitialized = true;
            try
            {
                string existing = registration.Read();
                Configured = existing != null;
                if (preferences.LaunchAtLogin && (first || existing != null))
                {
                    string command = Command(executable);
                    if (!String.Equals(existing, command, StringComparison.Ordinal))
                        registration.Write(command);
                }
                else if (!first && preferences.LaunchAtLogin && existing == null)
                    preferences.LaunchAtLogin = false;
                Refresh();
            }
            catch (Exception ex)
            {
                RecordError(ex);
            }
        }

        public void Refresh()
        {
            try
            {
                Configured = registration.Read() != null;
                Error = null;
            }
            catch (Exception ex)
            {
                RecordError(ex);
            }
        }

        public bool SetEnabled(bool enabled)
        {
            try
            {
                registration.Write(enabled ? Command(executable) : null);
                preferences.LaunchAtLogin = enabled;
                preferences.LoginStartupInitialized = true;
                Configured = enabled;
                Error = null;
                return true;
            }
            catch (Exception ex)
            {
                RecordError(ex);
                return false;
            }
        }

        void RecordError(Exception ex)
        {
            if (!(ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException))
                throw ex;
            Error = Texts.Get("startup.error");
        }
    }
}
