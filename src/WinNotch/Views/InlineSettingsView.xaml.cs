using System;
using System.Windows;
using WinNotch.Models;
using WinNotch.Services;

namespace WinNotch.Views;

public partial class InlineSettingsView : System.Windows.Controls.UserControl
{
    private AppSettings? _settings;
    private bool _isLoading;
    private readonly UpdateService _updateService = new();

    /// <summary>Fired when any setting changes.</summary>
    public event Action<AppSettings>? SettingsChanged;

    /// <summary>Fired when the user clicks the back arrow.</summary>
    public event Action? BackRequested;

    /// <summary>Fired when theme-related appearance changes.</summary>
    public event Action<AppSettings>? ThemeChangeRequested;

    /// <summary>Fired when the user wants to manage plugins.</summary>
    public event Action? ManagePluginsRequested;

    public InlineSettingsView()
    {
        InitializeComponent();
    }

    public void LoadSettings(AppSettings settings)
    {
        _isLoading = true;
        _settings = settings;

        StartOnBootCheck.IsChecked = settings.StartOnBoot;
        ShowShadowCheck.IsChecked = settings.ShowShadow;
        ShowMusicCheck.IsChecked = settings.ShowMusicControls;
        ShowVisualizerCheck.IsChecked = settings.ShowVisualizer;
        VolumeHudCheck.IsChecked = settings.ShowVolumeHud;
        BrightnessHudCheck.IsChecked = settings.ShowBrightnessHud;
        ShowBatteryCheck.IsChecked = settings.ShowBattery;
        ShowCalendarCheck.IsChecked = settings.ShowCalendar;
        ShowWebcamCheck.IsChecked = settings.ShowWebcam;
        WebcamFpsSlider.Value = settings.WebcamFps;
        WebcamFpsLabel.Text = $"{settings.WebcamFps}";
        LiquidGlassCheck.IsChecked = settings.UseLiquidGlassTheme;
        AdaptToWindowsThemeCheck.IsChecked = settings.AdaptToWindowsTheme;

        if (settings.HoverMode == HoverMode.LongHoverOpen)
            LongHoverRadio.IsChecked = true;
        else
            HoverPeekRadio.IsChecked = true;

        if (settings.GetManualTheme() == AppTheme.Light)
            ThemeLightRadio.IsChecked = true;
        else
            ThemeDarkRadio.IsChecked = true;

        _isLoading = false;
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _settings == null) return;

        _settings.StartOnBoot = StartOnBootCheck.IsChecked == true;
        _settings.ShowShadow = ShowShadowCheck.IsChecked == true;
        _settings.ShowMusicControls = ShowMusicCheck.IsChecked == true;
        _settings.ShowVisualizer = ShowVisualizerCheck.IsChecked == true;
        _settings.ShowVolumeHud = VolumeHudCheck.IsChecked == true;
        _settings.ShowBrightnessHud = BrightnessHudCheck.IsChecked == true;
        _settings.ShowBattery = ShowBatteryCheck.IsChecked == true;
        _settings.ShowCalendar = ShowCalendarCheck.IsChecked == true;
        _settings.ShowWebcam = ShowWebcamCheck.IsChecked == true;

        _settings.ApplyStartOnBoot();
        _settings.Save();
        SettingsChanged?.Invoke(_settings);
    }

    private void OnHoverModeChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _settings == null) return;

        _settings.HoverMode = LongHoverRadio.IsChecked == true
            ? HoverMode.LongHoverOpen
            : HoverMode.HoverPeekClickOpen;

        _settings.Save();
        SettingsChanged?.Invoke(_settings);
    }

    private void OnWebcamFpsChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading || _settings == null) return;
        _settings.WebcamFps = (int)e.NewValue;
        WebcamFpsLabel.Text = $"{_settings.WebcamFps}";
        _settings.Save();
        SettingsChanged?.Invoke(_settings);
    }

    public void UpdateWebcamFpsMax(double maxFps)
    {
        if (maxFps > 5)
            WebcamFpsSlider.Maximum = Math.Min(maxFps, 60);
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke();
    }

    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _settings == null) return;

        // Picking a manual theme should immediately opt out of Windows-follow mode.
        _settings.AdaptToWindowsTheme = false;
        AdaptToWindowsThemeCheck.IsChecked = false;
        _settings.Theme = ThemeLightRadio.IsChecked == true ? AppTheme.Light : AppTheme.Dark;

        _settings.Save();
        ThemeChangeRequested?.Invoke(_settings);
    }

    private void OnAdaptThemeChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _settings == null) return;

        _settings.AdaptToWindowsTheme = AdaptToWindowsThemeCheck.IsChecked == true;
        _settings.Save();
        ThemeChangeRequested?.Invoke(_settings);
    }

    private void OnLiquidGlassChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _settings == null) return;

        _settings.UseLiquidGlassTheme = LiquidGlassCheck.IsChecked == true;
        _settings.Save();
        SettingsChanged?.Invoke(_settings);
    }

    private void OnManagePluginsClick(object sender, RoutedEventArgs e)
    {
        ManagePluginsRequested?.Invoke();
    }

    private async void OnCheckUpdateClick(object sender, RoutedEventArgs e)
    {
        CheckUpdateText.Text = "Checking...";
        CheckUpdateButton.IsEnabled = false;

        var hasUpdate = await _updateService.CheckForUpdateAsync();

        if (hasUpdate)
        {
            CheckUpdateButton.Visibility = Visibility.Collapsed;
            UpdateButton.Visibility = Visibility.Visible;
            UpdateButtonText.Text = $"⬇ Update to v{_updateService.LatestVersion}";
        }
        else
        {
            CheckUpdateText.Text = "You're up to date ✓";
        }

        CheckUpdateButton.IsEnabled = true;
    }

    private async void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        UpdateButtonText.Text = "Downloading...";
        UpdateButton.IsEnabled = false;

        var progress = new Progress<double>(p =>
        {
            var pct = (int)(p * 100);
            UpdateButtonText.Text = $"Downloading... {pct}%";
        });

        var ok = await _updateService.DownloadAndLaunchInstallerAsync(progress);
        if (!ok)
        {
            UpdateButtonText.Text = "Download failed — retry";
            UpdateButton.IsEnabled = true;
        }
    }
}
