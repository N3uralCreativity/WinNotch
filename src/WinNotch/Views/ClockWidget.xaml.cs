using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WinNotch.Views;

public partial class ClockWidget : UserControl
{
    private DispatcherTimer? _timer;

    public ClockWidget()
    {
        InitializeComponent();
        UpdateTime();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _timer.Tick += (_, _) => UpdateTime();
        _timer.Start();
    }

    private void UpdateTime()
    {
        var now = DateTime.Now;
        TimeText.Text = now.ToString("HH:mm");
        DateText.Text = now.ToString("ddd d", CultureInfo.CurrentCulture);
    }
}
