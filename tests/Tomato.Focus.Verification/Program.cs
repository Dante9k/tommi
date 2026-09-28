using System;
using System.IO;
using System.Windows;
using Tomato;

namespace Tomato.Tests
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string mode = args.Length == 0 ? "--self-test" : args[0];
            if (mode == "--self-test")
                return Verification.Run();
            if (mode == "--render-preview")
                return Verification.Render();
            if (mode == "--installer-test" && args.Length == 2)
                return InstallerVerification.Run(args[1]);
            if (mode != "--smoke-test" && mode != "--focus-smoke" && mode != "--settings-smoke" && mode != "--wheel-smoke" && mode != "--language-smoke")
            {
                Console.Error.WriteLine("Unknown mode: " + mode);
                Console.Error.WriteLine("Usage: Tomato.Verify.exe [--self-test|--render-preview|--smoke-test|--focus-smoke|--settings-smoke|--wheel-smoke|--language-smoke|--installer-test SETUP_PATH]");
                return 2;
            }

            var application = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
            AppController controller = null;
            application.DispatcherUnhandledException += delegate (object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                Verification.SmokeExitCode = 1;
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke-results.txt"), "FAIL " + e.Exception);
                if (controller != null)
                    controller.Quit();
                e.Handled = true;
            };
            try
            {
                controller = new AppController(application, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke-state.xml"), new StartupVerification.FakeRegistration());
                controller.Launch(false);
                if (mode == "--language-smoke")
                    LocalizationVerification.Smoke(controller);
                else if (mode == "--wheel-smoke")
                    WheelVerification.Smoke(controller);
                else if (mode == "--settings-smoke")
                    Verification.SettingsSmoke(controller);
                else if (mode == "--focus-smoke")
                    Verification.FocusSmoke(controller);
                else
                    Verification.Smoke(controller);
                application.Run();
                return Verification.SmokeExitCode;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                if (controller != null)
                    controller.Quit();
                return 1;
            }
        }
    }
}
