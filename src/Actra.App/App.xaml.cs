using System.Windows;

namespace Actra;

public partial class App : Application
{
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mainWindow = new MainWindow();
        MainWindow = _mainWindow;
        _mainWindow.Show();
        _mainWindow.ActivateLauncher();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainWindow?.Dispose();
        base.OnExit(e);
    }
}
