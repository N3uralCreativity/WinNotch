using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WinNotch.Models;
using WinNotch.Services;

namespace WinNotch.Views;

public partial class HudOverlay : UserControl
{
    private VolumeService? _volumeService;
    private BrightnessService? _brightnessService;
    private DispatcherTimer? _dismissTimer;
    private HudType _currentType;
    private NotchDock _dock = NotchDock.Top;

    private enum HudType { None, Volume, Brightness }

    public event Action? HudShown;
    public event Action? HudDismissed;

    public HudOverlay()
    {
        InitializeComponent();
    }

    public void SetDock(NotchDock dock)
    {
        _dock = dock;
        UpdateLayout();
    }

    private new void UpdateLayout()
    {
        // For vertical docks, we need to adjust the slider to be vertical
        // The slider fill alignment changes: horizontal uses Left, vertical uses Bottom
        if (_dock != NotchDock.Top)
        {
            // Vertical orientation - slider fills from bottom to top
            SliderTrack.Width = 4;
            SliderTrack.Height = double.NaN; // Auto height
            SliderTrack.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            SliderTrack.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;

            SliderFill.Width = 4;
            SliderFill.Height = double.NaN; // Will be set programmatically
            SliderFill.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            SliderFill.VerticalAlignment = System.Windows.VerticalAlignment.Bottom;
        }
        else
        {
            // Horizontal orientation - slider fills from left to right
            SliderTrack.Width = double.NaN; // Auto width
            SliderTrack.Height = 4;
            SliderTrack.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            SliderTrack.VerticalAlignment = System.Windows.VerticalAlignment.Center;

            SliderFill.Width = double.NaN; // Will be set programmatically
            SliderFill.Height = 4;
            SliderFill.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            SliderFill.VerticalAlignment = System.Windows.VerticalAlignment.Stretch;
        }
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
        if (SliderFill.Parent is Border parent)
        {
            if (_dock == NotchDock.Top)
            {
                // Horizontal slider for top dock
                double parentWidth = parent.ActualWidth;
                // Only update if parent has valid width to prevent visual glitches
                if (parentWidth > 0 && !double.IsNaN(parentWidth) && !double.IsInfinity(parentWidth))
                {
                    double targetWidth = Math.Max(0, Math.Min(fraction * parentWidth, parentWidth));
                    SliderFill.Width = targetWidth;
                }
            }
            else
            {
                // Vertical slider for side docks
                double parentHeight = parent.ActualHeight;
                // Only update if parent has valid height to prevent visual glitches
                if (parentHeight > 0 && !double.IsNaN(parentHeight) && !double.IsInfinity(parentHeight))
                {
                    double targetHeight = Math.Max(0, Math.Min(fraction * parentHeight, parentHeight));
                    SliderFill.Height = targetHeight;
                }
            }
        }
    }

    private void Show()
    {
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
        float fraction;

        if (_dock == NotchDock.Top)
        {
            // Horizontal slider for top dock
            double barWidth = bar.ActualWidth;
            // Validate bar width before calculating position
            if (barWidth <= 0 || double.IsNaN(barWidth) || double.IsInfinity(barWidth)) return;

            fraction = (float)Math.Clamp(pos.X / barWidth, 0, 1);
        }
        else
        {
            // Vertical slider for side docks - inverted (top = max, bottom = min)
            double barHeight = bar.ActualHeight;
            // Validate bar height before calculating position
            if (barHeight <= 0 || double.IsNaN(barHeight) || double.IsInfinity(barHeight)) return;

            fraction = (float)Math.Clamp(1.0 - (pos.Y / barHeight), 0, 1);
        }

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
