using System;
using System.Drawing;
using System.Threading;
using System.Windows;
using WinNotch.Models;
using WinNotch.Services;
using WinNotch.Views;
using Forms = System.Windows.Forms;

namespace WinNotch;

public partial class App : Application
{
    private static Mutex? _mutex;
    private NotchWindow? _notchWindow;
    private Forms.NotifyIcon? _trayIcon;
    private AppSettings _settings = null!;
    private ThemeService _themeService = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handler for diagnostics
        DispatcherUnhandledException += (_, args) =>
        {
            System.Windows.MessageBox.Show(args.Exception.ToString(), "WinNotch Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // Single instance enforcement
        const string mutexName = "WinNotch_SingleInstance_Mutex";
        _mutex = new Mutex(true, mutexName, out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        // Load settings
        _settings = AppSettings.Load();

        // Initialize theme
        _themeService = new ThemeService();
        _themeService.Apply(_settings.Theme);
        _themeService.StartWatchingSystemTheme();

        // System tray icon
        SetupTrayIcon();

        // Launch the notch window
        _notchWindow = new NotchWindow(_settings, _themeService);
        _notchWindow.Show();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "WinNotch",
            Visible = true
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Settings", null, (_, _) => OpenSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => QuitApp());

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => OpenSettings();
    }

    private static Icon CreateDefaultIcon()
    {
        // Create a simple 16x16 black circle icon
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.FillEllipse(Brushes.White, 1, 1, 14, 14);
        return System.Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    private void OpenSettings()
    {
        _notchWindow?.OpenSettings();
    }

    private void QuitApp()
    {
        _themeService?.StopWatchingSystemTheme();
        _trayIcon?.Dispose();
        _notchWindow?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
