using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows;
using WinNotch.Models;
using WinNotch.Services;
using WinNotch.Views;
using Forms = System.Windows.Forms;
using DrawColor = System.Drawing.Color;

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

        var menu = new Forms.ContextMenuStrip
        {
            Renderer = new DarkMenuRenderer(),
            BackColor = DrawColor.FromArgb(28, 28, 30),
            ForeColor = DrawColor.FromArgb(224, 224, 224),
            ShowImageMargin = false,
            ShowCheckMargin = false,
            Padding = new Forms.Padding(4, 6, 4, 6),
            Font = new Font("Segoe UI", 9.5f)
        };

        var settingsItem = new Forms.ToolStripMenuItem("Settings")
        {
            BackColor = DrawColor.FromArgb(28, 28, 30),
            ForeColor = DrawColor.FromArgb(224, 224, 224),
            Padding = new Forms.Padding(12, 6, 20, 6)
        };
        settingsItem.Click += (_, _) => OpenSettings();

        var separator = new Forms.ToolStripSeparator();

        var quitItem = new Forms.ToolStripMenuItem("Quit")
        {
            BackColor = DrawColor.FromArgb(28, 28, 30),
            ForeColor = DrawColor.FromArgb(224, 224, 224),
            Padding = new Forms.Padding(12, 6, 20, 6)
        };
        quitItem.Click += (_, _) => QuitApp();

        menu.Items.Add(settingsItem);
        menu.Items.Add(separator);
        menu.Items.Add(quitItem);

        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => OpenSettings();
    }

    private static Icon CreateDefaultIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.FillEllipse(System.Drawing.Brushes.White, 1, 1, 14, 14);
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

/// <summary>
/// Custom renderer for dark-themed ContextMenuStrip.
/// </summary>
internal class DarkMenuRenderer : Forms.ToolStripProfessionalRenderer
{
    private static readonly DrawColor BgColor = DrawColor.FromArgb(28, 28, 30);
    private static readonly DrawColor HoverColor = DrawColor.FromArgb(50, 50, 54);
    private static readonly DrawColor SepColor = DrawColor.FromArgb(50, 50, 54);
    private static readonly DrawColor BorderColor = DrawColor.FromArgb(58, 58, 60);

    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderToolStripBackground(Forms.ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(BgColor);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(Forms.ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(BorderColor);
        var r = e.AffectedBounds;
        e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(Forms.ToolStripItemRenderEventArgs e)
    {
        var rc = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
        if (e.Item.Selected)
        {
            using var brush = new SolidBrush(HoverColor);
            using var gp = RoundedRect(rc, 4);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, gp);
        }
        else
        {
            using var brush = new SolidBrush(BgColor);
            e.Graphics.FillRectangle(brush, rc);
        }
    }

    protected override void OnRenderSeparator(Forms.ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new Pen(SepColor);
        e.Graphics.DrawLine(pen, 12, y, e.Item.Width - 12, y);
    }

    protected override void OnRenderItemText(Forms.ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Selected ? DrawColor.White : DrawColor.FromArgb(224, 224, 224);
        base.OnRenderItemText(e);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal class DarkColorTable : Forms.ProfessionalColorTable
{
    private static readonly DrawColor Bg = DrawColor.FromArgb(28, 28, 30);
    public override DrawColor MenuBorder => DrawColor.FromArgb(58, 58, 60);
    public override DrawColor MenuItemBorder => DrawColor.Transparent;
    public override DrawColor MenuItemSelected => DrawColor.FromArgb(50, 50, 54);
    public override DrawColor MenuStripGradientBegin => Bg;
    public override DrawColor MenuStripGradientEnd => Bg;
    public override DrawColor MenuItemSelectedGradientBegin => DrawColor.FromArgb(50, 50, 54);
    public override DrawColor MenuItemSelectedGradientEnd => DrawColor.FromArgb(50, 50, 54);
    public override DrawColor MenuItemPressedGradientBegin => DrawColor.FromArgb(60, 60, 64);
    public override DrawColor MenuItemPressedGradientEnd => DrawColor.FromArgb(60, 60, 64);
    public override DrawColor ImageMarginGradientBegin => Bg;
    public override DrawColor ImageMarginGradientMiddle => Bg;
    public override DrawColor ImageMarginGradientEnd => Bg;
    public override DrawColor ToolStripDropDownBackground => Bg;
    public override DrawColor SeparatorDark => DrawColor.FromArgb(50, 50, 54);
    public override DrawColor SeparatorLight => DrawColor.Transparent;
}
