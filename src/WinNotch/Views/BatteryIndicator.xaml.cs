using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinNotch.Services;

namespace WinNotch.Views;

public partial class BatteryIndicator : UserControl
{
    private BatteryService? _batteryService;

    public BatteryIndicator()
    {
        InitializeComponent();
    }

    public void Bind(BatteryService service)
    {
        _batteryService = service;

        service.PropertyChanged += OnServicePropertyChanged;
        UpdateUI();
    }

    private void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(UpdateUI);
    }

    private void UpdateUI()
    {
        if (_batteryService == null) return;

        if (!_batteryService.HasBattery)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;

        int percent = _batteryService.ChargePercent;
        bool charging = _batteryService.IsCharging;

        PercentText.Text = $"{percent}%";

        // Battery fill width: body is ~14px inner width
        double maxFillWidth = 12.0;
        BatteryFill.Width = Math.Max(0, (percent / 100.0) * maxFillWidth);

        // Color based on level
        Color fillColor;
        if (charging)
            fillColor = GetThemeColor("SuccessBrush", Color.FromRgb(0x4C, 0xAF, 0x50));
        else if (percent <= 10)
            fillColor = GetThemeColor("DangerBrush", Color.FromRgb(0xF4, 0x43, 0x36));
        else if (percent <= 20)
            fillColor = GetThemeColor("WarningBrush", Color.FromRgb(0xFF, 0x98, 0x00));
        else if (TryFindResource("BatteryNormalFillBrush") is SolidColorBrush normalBrush)
            fillColor = normalBrush.Color;
        else
            fillColor = Colors.White;

        BatteryFill.Background = new SolidColorBrush(fillColor);

        // Percent text color
        if (percent <= 10)
            PercentText.Foreground = new SolidColorBrush(GetThemeColor("DangerBrush", Color.FromRgb(0xF4, 0x43, 0x36)));
        else if (percent <= 20)
            PercentText.Foreground = new SolidColorBrush(GetThemeColor("WarningBrush", Color.FromRgb(0xFF, 0x98, 0x00)));
        else if (TryFindResource("BatteryTextBrush") is SolidColorBrush textBrush)
            PercentText.Foreground = textBrush;
        else
            PercentText.Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
    }

    private Color GetThemeColor(string resourceKey, Color fallback)
    {
        return TryFindResource(resourceKey) is SolidColorBrush brush ? brush.Color : fallback;
    }
}
