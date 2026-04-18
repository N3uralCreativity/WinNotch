using System;
using System.Windows;
using WinNotch.Models;

namespace WinNotch.Views;

public partial class InlineSettingsView : System.Windows.Controls.UserControl
{
    private AppSettings? _settings;
    private bool _isLoading;

    /// <summary>Fired when any setting changes.</summary>
    public event Action<AppSettings>? SettingsChanged;

    /// <summary>Fired when the user clicks the back arrow.</summary>
    public event Action? BackRequested;

    /// <summary>Fired when theme changes (passes new AppTheme).</summary>
    public event Action<AppTheme>? ThemeChangeRequested;

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

        if (settings.HoverMode == HoverMode.LongHoverOpen)
            LongHoverRadio.IsChecked = true;
        else
            HoverPeekRadio.IsChecked = true;

        // Theme
        switch (settings.Theme)
        {
            case AppTheme.Light: ThemeLightRadio.IsChecked = true; break;
            case AppTheme.Auto: ThemeAutoRadio.IsChecked = true; break;
            default: ThemeDarkRadio.IsChecked = true; break;
        }

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

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke();
    }

    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _settings == null) return;

        _settings.Theme = ThemeAutoRadio.IsChecked == true ? AppTheme.Auto
            : ThemeLightRadio.IsChecked == true ? AppTheme.Light
            : AppTheme.Dark;

        _settings.Save();
        ThemeChangeRequested?.Invoke(_settings.Theme);
    }
}
