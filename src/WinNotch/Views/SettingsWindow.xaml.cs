using System;
using System.Windows;
using System.Windows.Input;
using WinNotch.Models;
using WinNotch.Services;

namespace WinNotch.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private bool _isLoading = true;
    private readonly UpdateService _updateService = new();

    /// <summary>Fired when a setting changes so the notch can react.</summary>
    public event Action<AppSettings>? SettingsChanged;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        LoadSettings();
        _isLoading = false;

        // Allow dragging the borderless window
        MouseLeftButtonDown += (_, _) => DragMove();
    }

    private void LoadSettings()
    {
        StartOnBootCheck.IsChecked = _settings.StartOnBoot;
        ShowShadowCheck.IsChecked = _settings.ShowShadow;
        ShowMusicCheck.IsChecked = _settings.ShowMusicControls;
        ShowVisualizerCheck.IsChecked = _settings.ShowVisualizer;
        VolumeHudCheck.IsChecked = _settings.ShowVolumeHud;
        BrightnessHudCheck.IsChecked = _settings.ShowBrightnessHud;
        ShowBatteryCheck.IsChecked = _settings.ShowBattery;
        ShowCalendarCheck.IsChecked = _settings.ShowCalendar;
        LiquidGlassCheck.IsChecked = _settings.UseLiquidGlassTheme;
        AdaptToWindowsThemeCheck.IsChecked = _settings.AdaptToWindowsTheme;

        // Hover mode radio buttons
        if (_settings.HoverMode == HoverMode.LongHoverOpen)
            LongHoverRadio.IsChecked = true;
        else
            HoverPeekRadio.IsChecked = true;

        if (_settings.GetManualTheme() == AppTheme.Light)
            ThemeLightRadio.IsChecked = true;
        else
            ThemeDarkRadio.IsChecked = true;

        ApplyThemeSelectionState(_settings.AdaptToWindowsTheme);
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        _settings.StartOnBoot = StartOnBootCheck.IsChecked == true;
        _settings.ShowShadow = ShowShadowCheck.IsChecked == true;
        _settings.ShowMusicControls = ShowMusicCheck.IsChecked == true;
        _settings.ShowVisualizer = ShowVisualizerCheck.IsChecked == true;
        _settings.ShowVolumeHud = VolumeHudCheck.IsChecked == true;
        _settings.ShowBrightnessHud = BrightnessHudCheck.IsChecked == true;
        _settings.ShowBattery = ShowBatteryCheck.IsChecked == true;
        _settings.ShowCalendar = ShowCalendarCheck.IsChecked == true;

        _settings.ApplyStartOnBoot();
        _settings.Save();
        SettingsChanged?.Invoke(_settings);
    }

    private void OnHoverModeChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        _settings.HoverMode = LongHoverRadio.IsChecked == true
            ? HoverMode.LongHoverOpen
            : HoverMode.HoverPeekClickOpen;

        _settings.Save();
        SettingsChanged?.Invoke(_settings);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        _settings.Theme = ThemeLightRadio.IsChecked == true ? AppTheme.Light : AppTheme.Dark;
        _settings.Save();
        SettingsChanged?.Invoke(_settings);
    }

    private void OnAdaptThemeChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        _settings.AdaptToWindowsTheme = AdaptToWindowsThemeCheck.IsChecked == true;
        ApplyThemeSelectionState(_settings.AdaptToWindowsTheme);
        _settings.Save();
        SettingsChanged?.Invoke(_settings);
    }

    private void OnLiquidGlassChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        _settings.UseLiquidGlassTheme = LiquidGlassCheck.IsChecked == true;
        _settings.Save();
        SettingsChanged?.Invoke(_settings);
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
    private void ApplyThemeSelectionState(bool adaptToWindowsTheme)
    {
        ThemeDarkRadio.IsEnabled = !adaptToWindowsTheme;
        ThemeLightRadio.IsEnabled = !adaptToWindowsTheme;
    }
}
