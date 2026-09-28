using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Tomato
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string statePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TomatoFocus", "state.xml");
            Texts.SetLanguage(new StateStore(statePath).Read().Language);
            bool firstInstance;
            using (var mutex = new Mutex(true, "Local\\TomatoFocus.Desktop", out firstInstance))
            {
                if (!firstInstance)
                {
                    if (Array.IndexOf(args, "--startup") < 0)
                        MessageBox.Show(Texts.Get("app.alreadyRunning"), "Tommi");
                    return 0;
                }

                var application = new Application
                {
                    ShutdownMode = ShutdownMode.OnExplicitShutdown
                };
                AppController controller = null;
                application.DispatcherUnhandledException += delegate (object sender, DispatcherUnhandledExceptionEventArgs e)
                {
                    AppController.Log(e.Exception);
                    MessageBox.Show(Texts.Get("app.error"), "Tommi");
                    if (controller != null)
                        controller.Quit();
                    e.Handled = true;
                };
                try
                {
                    controller = new AppController(application, statePath, new RegistryStartupRegistration());
                    controller.Launch(true);
                    application.Run();
                    return 0;
                }
                catch (Exception exception)
                {
                    AppController.Log(exception);
                    MessageBox.Show(Texts.Get("app.startFailed") + exception.Message, "Tommi");
                    return 1;
                }
            }
        }
    }
}
