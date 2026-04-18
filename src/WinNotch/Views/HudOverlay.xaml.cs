using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WinNotch.Services;

namespace WinNotch.Views;

public partial class HudOverlay : UserControl
{
    private VolumeService? _volumeService;
    private BrightnessService? _brightnessService;
    private DispatcherTimer? _dismissTimer;
    private HudType _currentType;

    /// <summary>When false, Show() is a no-op (prevents dual-instance conflicts).</summary>
    public bool IsActive { get; set; } = true;

    private enum HudType { None, Volume, Brightness }

    public event Action? HudShown;
    public event Action? HudDismissed;

    public HudOverlay()
    {
        InitializeComponent();
    }

    public void Bind(VolumeService volumeService, BrightnessService brightnessService)
    {
        _volumeService = volumeService;
        _brightnessService = brightnessService;

        volumeService.VolumeChanged += (volume, muted) =>
        {
            Dispatcher.Invoke(() => ShowVolume(volume, muted));
        };

        if (brightnessService.IsSupported)
        {
            brightnessService.BrightnessChanged += brightness =>
            {
                Dispatcher.Invoke(() => ShowBrightness(brightness));
            };
        }
    }

    private void ShowVolume(float volume, bool muted)
    {
        _currentType = HudType.Volume;

        string icon = muted ? "🔇" : volume switch
        {
            < 0.01f => "🔇",
            < 0.33f => "🔈",
            < 0.66f => "🔉",
            _ => "🔊"
        };

        HudIcon.Text = icon;
        int percent = (int)(volume * 100);
        HudPercent.Text = muted ? "Mute" : $"{percent}%";
        UpdateSliderFill(muted ? 0 : volume);

        Show();
    }

    private void ShowBrightness(int brightness)
    {
        _currentType = HudType.Brightness;

        HudIcon.Text = brightness switch
        {
            < 33 => "🔅",
            _ => "🔆"
        };

        HudPercent.Text = $"{brightness}%";
        UpdateSliderFill(brightness / 100f);

        Show();
    }

    private void UpdateSliderFill(float fraction)
    {
        if (SliderFill.Parent is Border parent && parent.ActualWidth > 0)
        {
            SliderFill.Width = Math.Max(0, fraction * parent.ActualWidth);
        }
    }

    private void Show()
    {
        if (!IsActive) return;

        Visibility = Visibility.Visible;
        HudShown?.Invoke();

        // Auto-dismiss after 2 seconds
        _dismissTimer?.Stop();
        _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _dismissTimer.Tick += (_, _) =>
        {
            _dismissTimer.Stop();
            Dismiss();
        };
        _dismissTimer.Start();
    }

    private void Dismiss()
    {
        Visibility = Visibility.Collapsed;
        _currentType = HudType.None;
        HudDismissed?.Invoke();
    }

    private void OnSliderClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border bar) return;
        var pos = e.GetPosition(bar);
        float fraction = (float)Math.Clamp(pos.X / bar.ActualWidth, 0, 1);

        switch (_currentType)
        {
            case HudType.Volume:
                _volumeService?.SetVolume(fraction);
                break;
            case HudType.Brightness:
                _brightnessService?.SetBrightness((int)(fraction * 100));
                break;
        }

        // Reset dismiss timer
        Show();
    }
}
