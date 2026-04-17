using System;
using System.Threading;
using System.Windows;
using WinNotch.Views;

namespace WinNotch;

public partial class App : Application
{
    private static Mutex? _mutex;
    private NotchWindow? _notchWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Single instance enforcement
        const string mutexName = "WinNotch_SingleInstance_Mutex";
        _mutex = new Mutex(true, mutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("WinNotch is already running.", "WinNotch", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // Launch the notch window
        _notchWindow = new NotchWindow();
        _notchWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
