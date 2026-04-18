using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinNotch.Services;

namespace WinNotch.Views;

public partial class WebcamView : UserControl
{
    private WebcamService? _service;

    public WebcamView()
    {
        InitializeComponent();
    }

    public async void Bind(WebcamService service)
    {
        _service = service;
        service.FrameReady += OnFrameReady;
        await service.StartAsync();

        if (!service.IsAvailable)
            Visibility = Visibility.Collapsed;
    }

    private void OnFrameReady(ImageSource frame)
    {
        WebcamImage.Source = frame;
    }

    public async void StopCamera()
    {
        if (_service != null)
        {
            _service.FrameReady -= OnFrameReady;
            await _service.StopAsync();
        }
    }
}
