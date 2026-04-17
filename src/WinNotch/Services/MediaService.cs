using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;
using WinNotch.Models;

namespace WinNotch.Services;

public class MediaService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private readonly DispatcherTimer _positionTimer;

    public MediaInfo MediaInfo { get; } = new();

    public event Action? SessionChanged;

    public MediaService()
    {
        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _positionTimer.Tick += OnPositionTimerTick;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            AttachSession(_manager.GetCurrentSession());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MediaService init failed: {ex.Message}");
        }
    }

    private void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            AttachSession(sender.GetCurrentSession());
            SessionChanged?.Invoke();
        });
    }

    private void AttachSession(GlobalSystemMediaTransportControlsSession? session)
    {
        // Detach old
        if (_session != null)
        {
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _session.TimelinePropertiesChanged -= OnTimelineChanged;
        }

        _session = session;

        if (_session != null)
        {
            _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            _session.TimelinePropertiesChanged += OnTimelineChanged;

            MediaInfo.HasMedia = true;
            _ = UpdateMediaPropertiesAsync();
            UpdatePlaybackInfo();
            UpdateTimeline();
            _positionTimer.Start();
        }
        else
        {
            MediaInfo.HasMedia = false;
            MediaInfo.IsPlaying = false;
            _positionTimer.Stop();
        }
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        Application.Current?.Dispatcher.Invoke(() => _ = UpdateMediaPropertiesAsync());
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        Application.Current?.Dispatcher.Invoke(UpdatePlaybackInfo);
    }

    private void OnTimelineChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
    {
        Application.Current?.Dispatcher.Invoke(UpdateTimeline);
    }

    private async Task UpdateMediaPropertiesAsync()
    {
        if (_session == null) return;

        try
        {
            var props = await _session.TryGetMediaPropertiesAsync();
            if (props == null) return;

            MediaInfo.Title = props.Title ?? string.Empty;
            MediaInfo.Artist = props.Artist ?? string.Empty;
            MediaInfo.AlbumTitle = props.AlbumTitle ?? string.Empty;

            // Load album art
            if (props.Thumbnail != null)
            {
                try
                {
                    using var stream = await props.Thumbnail.OpenReadAsync();
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream.AsStreamForRead();
                    bitmap.DecodePixelWidth = 300;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    MediaInfo.AlbumArt = bitmap;

                    // Extract dominant color
                    MediaInfo.DominantColor = ColorExtractionService.GetDominantColor(bitmap);
                }
                catch
                {
                    MediaInfo.AlbumArt = null;
                }
            }
            else
            {
                MediaInfo.AlbumArt = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateMediaProperties failed: {ex.Message}");
        }
    }

    private void UpdatePlaybackInfo()
    {
        if (_session == null) return;

        try
        {
            var info = _session.GetPlaybackInfo();
            if (info == null) return;

            MediaInfo.IsPlaying = info.PlaybackStatus ==
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            MediaInfo.IsShuffle = info.IsShuffleActive ?? false;

            MediaInfo.RepeatMode = info.AutoRepeatMode switch
            {
                Windows.Media.MediaPlaybackAutoRepeatMode.List => 1,
                Windows.Media.MediaPlaybackAutoRepeatMode.Track => 2,
                _ => 0
            };
        }
        catch { }
    }

    private void UpdateTimeline()
    {
        if (_session == null) return;

        try
        {
            var timeline = _session.GetTimelineProperties();
            if (timeline == null) return;

            MediaInfo.Position = timeline.Position;
            MediaInfo.Duration = timeline.EndTime;
        }
        catch { }
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (_session == null || !MediaInfo.IsPlaying) return;
        UpdateTimeline();
    }

    // Transport controls
    public async Task PlayPauseAsync()
    {
        if (_session == null) return;
        try { await _session.TryTogglePlayPauseAsync(); } catch { }
    }

    public async Task NextAsync()
    {
        if (_session == null) return;
        try { await _session.TrySkipNextAsync(); } catch { }
    }

    public async Task PreviousAsync()
    {
        if (_session == null) return;
        try { await _session.TrySkipPreviousAsync(); } catch { }
    }

    public async Task ToggleShuffleAsync()
    {
        if (_session == null) return;
        try { await _session.TryChangeShuffleActiveAsync(!MediaInfo.IsShuffle); } catch { }
    }

    public async Task CycleRepeatAsync()
    {
        if (_session == null) return;
        try
        {
            var next = MediaInfo.RepeatMode switch
            {
                0 => Windows.Media.MediaPlaybackAutoRepeatMode.List,
                1 => Windows.Media.MediaPlaybackAutoRepeatMode.Track,
                _ => Windows.Media.MediaPlaybackAutoRepeatMode.None
            };
            await _session.TryChangeAutoRepeatModeAsync(next);
        }
        catch { }
    }

    public async Task SeekToAsync(double percent)
    {
        if (_session == null || MediaInfo.Duration.TotalSeconds <= 0) return;
        try
        {
            var targetTicks = (long)(percent * MediaInfo.Duration.Ticks);
            await _session.TryChangePlaybackPositionAsync(targetTicks);
        }
        catch { }
    }

    public void Dispose()
    {
        _positionTimer.Stop();
        if (_session != null)
        {
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _session.TimelinePropertiesChanged -= OnTimelineChanged;
        }
        if (_manager != null)
        {
            _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
        }
    }
}
