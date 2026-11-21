using System.Threading;
using System.Windows;

namespace TinyQuakeLauncher;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        const string mutexName = @"Local\TinyQuakeLauncher_SingleInstance";
        var mutex = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out bool createdNew);

        if (!createdNew)
        {
            mutex.Dispose();

            System.Windows.MessageBox.Show(
                "Please close the other instance first.",
                "TQLauncher already running",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            Shutdown();
            return;
        }

        _singleInstanceMutex = mutex;
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }

        base.OnExit(e);
    }
}