using System;
using System.Windows;
using System.Windows.Input;
using WinNotch.Models;

namespace WinNotch.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private bool _isLoading = true;

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

        // Hover mode radio buttons
        if (_settings.HoverMode == HoverMode.LongHoverOpen)
            LongHoverRadio.IsChecked = true;
        else
            HoverPeekRadio.IsChecked = true;
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
}
