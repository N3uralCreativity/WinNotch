using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinNotch.Models;
using WinNotch.Services;

namespace WinNotch.Views;

public partial class MusicLiveActivity : UserControl
{
    private MediaService? _mediaService;
    private AudioCaptureService? _audioCaptureService;

    public MusicLiveActivity()
    {
        InitializeComponent();
    }

    public void Bind(MediaService mediaService, AudioCaptureService audioCaptureService)
    {
        _mediaService = mediaService;
        _audioCaptureService = audioCaptureService;

        var info = mediaService.MediaInfo;
        info.PropertyChanged += (_, e) =>
        {
            Dispatcher.Invoke(() => UpdateCompact(info, e.PropertyName));
        };

        audioCaptureService.SpectrumUpdated += () =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (audioCaptureService.SpectrumData != null)
                    CompactVisualizer.UpdateSpectrum(audioCaptureService.SpectrumData);
            });
        };

        UpdateCompact(info, null);
    }

    private void UpdateCompact(MediaInfo info, string? propertyName)
    {
        if (!info.HasMedia || !info.IsPlaying)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        Visibility = Visibility.Visible;

        if (propertyName == null || propertyName == nameof(info.AlbumArt))
            CompactAlbumArt.Source = info.AlbumArt;

        if (propertyName == null || propertyName == nameof(info.Title))
            CompactTitle.Text = info.Title;

        if (propertyName == null || propertyName == nameof(info.DominantColor))
        {
            var color = info.DominantColor;
            // Ensure the visualizer bar color is always visible against the dark notch
            // If dominant color is too dark, use white instead
            double brightness = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
            CompactVisualizer.BarColor = brightness < 0.3 ? Colors.White : color;
        }
    }
}
