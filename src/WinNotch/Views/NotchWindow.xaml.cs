using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WinNotch.Helpers;
using WinNotch.Models;
using WinNotch.Services;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace WinNotch.Views;

public partial class NotchWindow : Window
{
    private readonly NotchViewModel _vm;

    // Spring animators for each animated property
    private readonly SpringAnimator _widthSpring;
    private readonly SpringAnimator _heightSpring;
    private readonly SpringAnimator _topRadiusSpring;
    private readonly SpringAnimator _bottomRadiusSpring;
    private readonly SpringAnimator _shadowOpacitySpring;
    private readonly SpringAnimator _contentOpacitySpring;

    // Current animated values
    private double _currentWidth;
    private double _currentHeight;
    private double _currentTopRadius;
    private double _currentBottomRadius;
    private double _currentShadowOpacity;
    private double _currentContentOpacity;

    // Fixed window dimensions (large enough for open state + shadow padding)
    private double _fixedWindowWidth;
    private double _fixedWindowHeight;

    // Hover debounce
    private DispatcherTimer? _hoverOpenTimer;
    private DispatcherTimer? _hoverCloseTimer;
    private bool _isMouseOverNotch;

    // Services
    private readonly MediaService _mediaService;
    private readonly AudioCaptureService _audioCaptureService;
    private readonly VolumeService _volumeService;
    private readonly BrightnessService _brightnessService;
    private readonly BatteryService _batteryService;
    private readonly CalendarService _calendarService;

    // Settings
    private AppSettings _settings;

    public NotchWindow(AppSettings settings)
    {
        InitializeComponent();

        _settings = settings;
        _vm = new NotchViewModel();
        DataContext = _vm;

        // Initialize springs
        _widthSpring = new SpringAnimator(0.42, 0.82);
        _heightSpring = new SpringAnimator(0.42, 0.82);
        _topRadiusSpring = new SpringAnimator(0.38, 0.85);
        _bottomRadiusSpring = new SpringAnimator(0.38, 0.85);
        _shadowOpacitySpring = new SpringAnimator(0.30, 0.90);
        _contentOpacitySpring = new SpringAnimator(0.35, 0.90);

        // Set initial values
        _currentWidth = NotchConstants.ClosedWidth;
        _currentHeight = NotchConstants.ClosedHeight;
        _currentTopRadius = NotchConstants.ClosedTopRadius;
        _currentBottomRadius = NotchConstants.ClosedBottomRadius;
        _currentShadowOpacity = 0.0;
        _currentContentOpacity = 0.0;

        // Services
        _mediaService = new MediaService();
        _audioCaptureService = new AudioCaptureService(bandCount: 12);
        _volumeService = new VolumeService();
        _brightnessService = new BrightnessService();
        _batteryService = new BatteryService();
        _calendarService = new CalendarService();

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowHelper.MakeOverlayWindow(this);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        PositionFixedWindow();
        UpdateNotchVisuals();

        // Register for per-frame rendering
        CompositionTarget.Rendering += OnRendering;

        // Mouse events on the notch path
        NotchPath.MouseEnter += OnNotchMouseEnter;
        NotchPath.MouseLeave += OnNotchMouseLeave;
        NotchPath.MouseLeftButtonDown += OnNotchClick;

        // Also track mouse on content grid (so hovering content keeps it open)
        ContentGrid.MouseEnter += OnNotchMouseEnter;
        ContentGrid.MouseLeave += OnNotchMouseLeave;

        // Right-click context menu
        NotchPath.MouseRightButtonDown += OnNotchRightClick;

        // Initialize media services
        _ = InitializeServicesAsync();
    }

    private async System.Threading.Tasks.Task InitializeServicesAsync()
    {
        await _mediaService.InitializeAsync();
        _audioCaptureService.Start();

        // Bind views to services
        MusicPlayer.Bind(_mediaService);
        MusicPlayer.SettingsRequested += OpenSettings;
        LiveActivity.Bind(_mediaService, _audioCaptureService);

        // Toggle clock/live-activity visibility based on music state
        _mediaService.MediaInfo.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MediaInfo.HasMedia) or nameof(MediaInfo.IsPlaying))
                Dispatcher.Invoke(UpdateClosedContentVisibility);
        };
        _mediaService.SessionChanged += () => Dispatcher.Invoke(UpdateClosedContentVisibility);
        UpdateClosedContentVisibility();

        // Volume & Brightness
        _volumeService.Initialize();
        _brightnessService.Initialize();
        HudOverlay.Bind(_volumeService, _brightnessService);

        // Battery
        _batteryService.Initialize();
        BatteryIndicator.Bind(_batteryService);

        // Calendar
        _calendarService.Initialize();
        CalendarPanel_View.Bind(_calendarService);
        CalendarPanel.Visibility = _settings.ShowCalendar ? Visibility.Visible : Visibility.Collapsed;

        // When HUD shows, expand notch horizontally and hide other content
        HudOverlay.HudShown += () => Dispatcher.Invoke(() =>
        {
            if (_vm.NotchState != NotchState.Open)
            {
                LiveActivity.Visibility = Visibility.Collapsed;
                ClockWidget.Visibility = Visibility.Collapsed;
                _widthSpring.AnimateTo(NotchConstants.HudWidth, _currentWidth);
            }
        });
        HudOverlay.HudDismissed += () => Dispatcher.Invoke(() =>
        {
            UpdateClosedContentVisibility();
            if (_vm.NotchState != NotchState.Open)
            {
                _widthSpring.AnimateTo(NotchConstants.ClosedWidth, _currentWidth);
            }
        });
    }

    private void UpdateClosedContentVisibility()
    {
        var info = _mediaService.MediaInfo;
        bool hasMusic = info.HasMedia && info.IsPlaying;
        LiveActivity.Visibility = hasMusic ? Visibility.Visible : Visibility.Collapsed;
        ClockWidget.Visibility = hasMusic ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OpenSettings()
    {
        foreach (Window w in Application.Current.Windows)
        {
            if (w is SettingsWindow existing)
            {
                existing.Activate();
                return;
            }
        }

        var settingsWindow = new SettingsWindow(_settings);
        settingsWindow.SettingsChanged += s => ApplySettings(s);
        settingsWindow.Show();
    }

    /// <summary>
    /// Position the window once: centered at top of primary screen,
    /// sized to fit the largest notch state (open) + shadow padding.
    /// The window never moves or resizes after this.
    /// </summary>
    private void PositionFixedWindow()
    {
        double padding = NotchConstants.WindowPadding;
        double maxOpenWidth = Math.Max(NotchConstants.OpenWidth, NotchConstants.OpenWidthWithCalendar);
        _fixedWindowWidth = maxOpenWidth + padding * 2;
        _fixedWindowHeight = NotchConstants.OpenHeight + padding + 10;

        var screenBounds = ScreenHelper.GetPrimaryScreenBounds(this);

        Width = _fixedWindowWidth;
        Height = _fixedWindowHeight;
        Left = screenBounds.Left + (screenBounds.Width - _fixedWindowWidth) / 2;
        Top = screenBounds.Top - 2;
    }

    #region Animation Loop

    private void OnRendering(object? sender, EventArgs e)
    {
        bool anyAnimating = false;

        if (_widthSpring.IsAnimating)
        {
            _currentWidth = _widthSpring.Tick();
            anyAnimating = true;
        }
        if (_heightSpring.IsAnimating)
        {
            _currentHeight = _heightSpring.Tick();
            anyAnimating = true;
        }
        if (_topRadiusSpring.IsAnimating)
        {
            _currentTopRadius = _topRadiusSpring.Tick();
            anyAnimating = true;
        }
        if (_bottomRadiusSpring.IsAnimating)
        {
            _currentBottomRadius = _bottomRadiusSpring.Tick();
            anyAnimating = true;
        }
        if (_shadowOpacitySpring.IsAnimating)
        {
            _currentShadowOpacity = _shadowOpacitySpring.Tick();
            anyAnimating = true;
        }
        if (_contentOpacitySpring.IsAnimating)
        {
            _currentContentOpacity = _contentOpacitySpring.Tick();
            anyAnimating = true;
        }

        if (anyAnimating)
        {
            UpdateNotchVisuals();
        }
    }

    private void UpdateNotchVisuals()
    {
        double w = _currentWidth;
        double h = _currentHeight;
        double topR = _currentTopRadius;
        double bottomR = _currentBottomRadius;

        // Generate notch geometry
        var rect = new Rect(0, 0, w, h);
        var geometry = NotchShape.CreateNotchGeometry(rect, topR, bottomR);

        NotchPath.Data = geometry;
        ShadowPath.Data = geometry;

        // Center the notch horizontally within the fixed-size window
        double offsetX = (_fixedWindowWidth - w) / 2;
        double offsetY = 0; // Notch anchored at top edge

        System.Windows.Controls.Canvas.SetLeft(NotchPath, offsetX);
        System.Windows.Controls.Canvas.SetTop(NotchPath, offsetY);
        System.Windows.Controls.Canvas.SetLeft(ShadowPath, offsetX);
        System.Windows.Controls.Canvas.SetTop(ShadowPath, offsetY);

        // Shadow opacity
        ShadowPath.Opacity = _currentShadowOpacity;

        // Content grid: positioned inside the notch shape (inset from corners)
        double contentLeft = offsetX + topR + bottomR;
        double contentTop = offsetY + topR;
        double contentWidth = Math.Max(0, w - (topR + bottomR) * 2);
        double contentHeight = Math.Max(0, h - topR - bottomR);

        System.Windows.Controls.Canvas.SetLeft(ContentGrid, contentLeft);
        System.Windows.Controls.Canvas.SetTop(ContentGrid, contentTop);
        ContentGrid.Width = contentWidth;
        ContentGrid.Height = contentHeight;

        // Content opacity
        OpenContent.Opacity = _currentContentOpacity;
        ClosedContent.Opacity = 1.0 - _currentContentOpacity;

        // Canvas covers the full fixed window
        NotchCanvas.Width = _fixedWindowWidth;
        NotchCanvas.Height = _fixedWindowHeight;
    }

    #endregion

    #region State Transitions

    private void TransitionToOpen()
    {
        if (_vm.NotchState == NotchState.Open) return;
        _vm.Open();

        _widthSpring.Response = 0.42;
        _widthSpring.DampingFraction = 0.80;
        _heightSpring.Response = 0.42;
        _heightSpring.DampingFraction = 0.80;

        double openW = _settings.ShowCalendar
            ? NotchConstants.OpenWidthWithCalendar
            : NotchConstants.OpenWidth;

        _widthSpring.AnimateTo(openW, _currentWidth);
        _heightSpring.AnimateTo(NotchConstants.OpenHeight, _currentHeight);
        _topRadiusSpring.AnimateTo(NotchConstants.OpenTopRadius, _currentTopRadius);
        _bottomRadiusSpring.AnimateTo(NotchConstants.OpenBottomRadius, _currentBottomRadius);
        _shadowOpacitySpring.AnimateTo(0.7, _currentShadowOpacity);
        _contentOpacitySpring.AnimateTo(1.0, _currentContentOpacity);
    }

    private void TransitionToPeek()
    {
        if (_vm.NotchState == NotchState.Peeking || _vm.NotchState == NotchState.Open) return;
        _vm.Peek();

        _widthSpring.Response = 0.30;
        _widthSpring.DampingFraction = 0.90;
        _heightSpring.Response = 0.30;
        _heightSpring.DampingFraction = 0.90;

        _widthSpring.AnimateTo(NotchConstants.PeekWidth, _currentWidth);
        _heightSpring.AnimateTo(NotchConstants.PeekHeight, _currentHeight);
    }

    private void TransitionToClose()
    {
        if (_vm.NotchState == NotchState.Closed) return;
        _vm.Close();

        _widthSpring.Response = 0.45;
        _widthSpring.DampingFraction = 1.0;
        _heightSpring.Response = 0.45;
        _heightSpring.DampingFraction = 1.0;

        _widthSpring.AnimateTo(NotchConstants.ClosedWidth, _currentWidth);
        _heightSpring.AnimateTo(NotchConstants.ClosedHeight, _currentHeight);
        _topRadiusSpring.AnimateTo(NotchConstants.ClosedTopRadius, _currentTopRadius);
        _bottomRadiusSpring.AnimateTo(NotchConstants.ClosedBottomRadius, _currentBottomRadius);
        _shadowOpacitySpring.AnimateTo(0.0, _currentShadowOpacity);
        _contentOpacitySpring.AnimateTo(0.0, _currentContentOpacity);
    }

    #endregion

    #region Hover Logic

    private void OnNotchMouseEnter(object sender, MouseEventArgs e)
    {
        _isMouseOverNotch = true;
        _hoverCloseTimer?.Stop();

        if (_vm.NotchState == NotchState.Closed)
        {
            _hoverOpenTimer?.Stop();

            if (_settings.HoverMode == HoverMode.HoverPeekClickOpen)
            {
                // Peek mode: slight expand on hover, click to fully open
                _hoverOpenTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(100)
                };
                _hoverOpenTimer.Tick += (_, _) =>
                {
                    _hoverOpenTimer!.Stop();
                    if (_isMouseOverNotch && _vm.NotchState == NotchState.Closed)
                        TransitionToPeek();
                };
                _hoverOpenTimer.Start();
            }
            else // LongHoverOpen
            {
                // Long hover: wait longer then fully open
                _hoverOpenTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(_settings.LongHoverDelayMs)
                };
                _hoverOpenTimer.Tick += (_, _) =>
                {
                    _hoverOpenTimer!.Stop();
                    if (_isMouseOverNotch)
                        TransitionToOpen();
                };
                _hoverOpenTimer.Start();
            }
        }
    }

    private void OnNotchMouseLeave(object sender, MouseEventArgs e)
    {
        _isMouseOverNotch = false;
        _hoverOpenTimer?.Stop();

        if (_vm.NotchState == NotchState.Open || _vm.NotchState == NotchState.Peeking)
        {
            _hoverCloseTimer?.Stop();
            _hoverCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(
                    _vm.NotchState == NotchState.Peeking ? 80 : NotchConstants.HoverCloseDelayMs)
            };
            _hoverCloseTimer.Tick += (_, _) =>
            {
                _hoverCloseTimer!.Stop();
                if (!_isMouseOverNotch)
                    TransitionToClose();
            };
            _hoverCloseTimer.Start();
        }
    }

    private void OnNotchClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.NotchState == NotchState.Closed || _vm.NotchState == NotchState.Peeking)
            TransitionToOpen();
    }

    private void OnNotchRightClick(object sender, MouseButtonEventArgs e)
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings" };
        settingsItem.Click += (_, _) => OpenSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var quitItem = new System.Windows.Controls.MenuItem { Header = "Quit" };
        quitItem.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(quitItem);

        menu.IsOpen = true;
        e.Handled = true;
    }

    #endregion

    protected override void OnClosed(EventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
        _audioCaptureService.Dispose();
        _mediaService.Dispose();
        _volumeService.Dispose();
        _brightnessService.Dispose();
        _batteryService.Dispose();
        base.OnClosed(e);
    }

    public void ApplySettings(AppSettings settings)
    {
        _settings = settings;

        // Toggle visibility of components based on settings
        LiveActivity.Visibility = settings.ShowMusicControls ? Visibility.Visible : Visibility.Collapsed;
        MusicPlayer.Visibility = settings.ShowMusicControls ? Visibility.Visible : Visibility.Collapsed;
        BatteryIndicator.Visibility = settings.ShowBattery ? Visibility.Visible : Visibility.Collapsed;
        CalendarPanel.Visibility = settings.ShowCalendar ? Visibility.Visible : Visibility.Collapsed;
        CompactVisualizer_SetVisible(settings.ShowVisualizer);
    }

    private void CompactVisualizer_SetVisible(bool visible)
    {
        // The visualizer is inside LiveActivity; toggle via its property
        if (LiveActivity.FindName("CompactVisualizer") is UIElement viz)
            viz.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
}
