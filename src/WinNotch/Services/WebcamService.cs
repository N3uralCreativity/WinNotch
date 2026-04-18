using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Graphics.Imaging;

namespace WinNotch.Services;

public class WebcamService : IDisposable
{
    private MediaCapture? _capture;
    private MediaFrameReader? _reader;
    private bool _isRunning;
    private bool _disposed;

    public event Action<ImageSource>? FrameReady;
    public bool IsAvailable { get; private set; }

    public async Task StartAsync()
    {
        if (_isRunning) return;

        try
        {
            var groups = await MediaFrameSourceGroup.FindAllAsync();
            MediaFrameSourceGroup? selectedGroup = null;
            MediaFrameSourceInfo? selectedInfo = null;

            foreach (var group in groups)
            {
                foreach (var info in group.SourceInfos)
                {
                    if (info.MediaStreamType == Windows.Media.Capture.MediaStreamType.VideoPreview ||
                        info.MediaStreamType == Windows.Media.Capture.MediaStreamType.VideoRecord)
                    {
                        if (info.SourceKind == MediaFrameSourceKind.Color)
                        {
                            selectedGroup = group;
                            selectedInfo = info;
                            break;
                        }
                    }
                }
                if (selectedGroup != null) break;
            }

            if (selectedGroup == null || selectedInfo == null)
            {
                IsAvailable = false;
                return;
            }

            _capture = new MediaCapture();
            var settings = new MediaCaptureInitializationSettings
            {
                SourceGroup = selectedGroup,
                SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                StreamingCaptureMode = StreamingCaptureMode.Video
            };

            await _capture.InitializeAsync(settings);

            var source = _capture.FrameSources[selectedInfo.Id];

            // Pick a small resolution format to minimize CPU usage
            MediaFrameFormat? bestFormat = null;
            foreach (var fmt in source.SupportedFormats)
            {
                if (fmt.VideoFormat.Width <= 640 && fmt.VideoFormat.Width >= 160)
                {
                    if (bestFormat == null || fmt.VideoFormat.Width > bestFormat.VideoFormat.Width)
                        bestFormat = fmt;
                }
            }
            if (bestFormat != null)
                await source.SetFormatAsync(bestFormat);

            _reader = await _capture.CreateFrameReaderAsync(source);
            _reader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
            _reader.FrameArrived += OnFrameArrived;

            var status = await _reader.StartAsync();
            _isRunning = status == MediaFrameReaderStartStatus.Success;
            IsAvailable = _isRunning;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Webcam start failed: {ex.Message}");
            IsAvailable = false;
        }
    }

    private int _frameSkip;

    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        // Only process every 3rd frame (~10fps) to reduce CPU
        if (Interlocked.Increment(ref _frameSkip) % 3 != 0) return;

        using var frameRef = sender.TryAcquireLatestFrame();
        var bitmap = frameRef?.VideoMediaFrame?.SoftwareBitmap;
        if (bitmap == null) return;

        // Convert to Bgra8 if needed
        SoftwareBitmap converted;
        if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
            bitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
        {
            converted = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }
        else
        {
            converted = SoftwareBitmap.Copy(bitmap);
        }

        int w = converted.PixelWidth;
        int h = converted.PixelHeight;
        var buffer = new byte[w * h * 4];

        converted.CopyToBuffer(buffer.AsBuffer());
        converted.Dispose();

        // Create WPF ImageSource on the UI thread
        Application.Current?.Dispatcher?.BeginInvoke(() =>
        {
            try
            {
                var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, buffer, w * 4);
                bmp.Freeze();
                FrameReady?.Invoke(bmp);
            }
            catch { }
        });
    }

    public async Task StopAsync()
    {
        if (!_isRunning) return;
        _isRunning = false;

        if (_reader != null)
        {
            _reader.FrameArrived -= OnFrameArrived;
            await _reader.StopAsync();
            _reader.Dispose();
            _reader = null;
        }

        _capture?.Dispose();
        _capture = null;
        IsAvailable = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ = StopAsync();
    }
}
