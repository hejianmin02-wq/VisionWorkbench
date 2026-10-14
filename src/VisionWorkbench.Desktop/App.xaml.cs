using System.Windows;

namespace VisionWorkbench.Desktop;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length > 0 && e.Args[0] == "--smoke-test")
        {
            var directory = e.Args.Length > 1 ? e.Args[1] : "artifacts";
            var exitCode = await SmokeTest.RunAsync(directory);
            Shutdown(exitCode);
            return;
        }
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
