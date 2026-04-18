using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WinNotch.Helpers;
using WinNotch.Models;
using WinNotch.Services;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using DragEventArgs = System.Windows.DragEventArgs;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;

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
    private bool _isSettingsExpanded;
    private bool _isContextMenuOpen;

    // Drag & Dock
    private NotchDock _dock = NotchDock.Top;
    private bool _isDragging;
    private bool _isMouseDownOnNotch;
    private Point _dragStartScreen;
    private Point _dragLastScreen;
    private DateTime _dragLastTime;
    private double _dragVelocityX;
    private double _dragVelocityY;

    // Services
    private readonly MediaService _mediaService;
    private readonly AudioCaptureService _audioCaptureService;
    private readonly VolumeService _volumeService;
    private readonly BrightnessService _brightnessService;
    private readonly BatteryService _batteryService;
    private readonly CalendarService _calendarService;
    private readonly ThemeService _themeService;
    private readonly ShelfService _shelfService;
    private readonly FullscreenService _fullscreenService;
    private readonly WebcamService _webcamService;
    private readonly GlobalHotkey _globalHotkey;

    // Settings
    private AppSettings _settings;

    public NotchWindow(AppSettings settings, ThemeService themeService)
    {
        InitializeComponent();

        _settings = settings;
        _vm = new NotchViewModel();
        DataContext = _vm;

        // Theme
        _themeService = themeService;

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
        _shelfService = new ShelfService();
        _fullscreenService = new FullscreenService();
        _webcamService = new WebcamService();
        _globalHotkey = new GlobalHotkey();

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowHelper.MakeOverlayWindow(this);

        // Register global hotkey (Ctrl+Alt+N)
        _globalHotkey.Register(this);
        _globalHotkey.HotkeyPressed += OnGlobalHotkeyPressed;
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
        NotchPath.MouseLeftButtonDown += OnNotchMouseDown;
        NotchPath.MouseLeftButtonUp += OnNotchMouseUp;
        NotchPath.MouseMove += OnNotchMouseMove;

        // Also track mouse on content grid (so hovering content keeps it open)
        ContentGrid.MouseEnter += OnNotchMouseEnter;
        ContentGrid.MouseLeave += OnNotchMouseLeave;

        // Drag + click from content area (so vertical time label etc. are draggable)
        ContentGrid.PreviewMouseLeftButtonDown += OnNotchMouseDown;
        ContentGrid.PreviewMouseMove += OnContentGridMouseMove;
        ContentGrid.PreviewMouseLeftButtonUp += OnContentGridMouseUp;

        // Handle mouse leaving window during drag
        RootGrid.MouseLeave += OnRootGridMouseLeave;

        // Scroll on notch changes volume (Preview to catch before children)
        RootGrid.PreviewMouseWheel += OnNotchMouseWheel;

        // Right-click context menu
        NotchPath.MouseRightButtonDown += OnNotchRightClick;

        // Drag-drop for file shelf
        DragEnter += OnDragEnter;
        DragOver += OnDragOver;
        DragLeave += OnDragLeave;
        Drop += OnDrop;

        // Initialize media services
        _ = InitializeServicesAsync();
    }

    private async System.Threading.Tasks.Task InitializeServicesAsync()
    {
        await _mediaService.InitializeAsync();
        _audioCaptureService.Start();

        // Bind views to services
        MusicPlayer.Bind(_mediaService);
        LiveActivity.Bind(_mediaService, _audioCaptureService);

        // Inline settings
        InlineSettings.SettingsChanged += s => ApplySettings(s);
        InlineSettings.BackRequested += CollapseSettings;
        InlineSettings.ThemeChangeRequested += async theme =>
        {
            await _themeService.ApplyWithTransition(theme, NotchCanvas);
        };

        // Vertical inline settings (separate instance for side dock)
        VerticalInlineSettings.SettingsChanged += s => ApplySettings(s);
        VerticalInlineSettings.BackRequested += CollapseSettings;
        VerticalInlineSettings.ThemeChangeRequested += async theme =>
        {
            await _themeService.ApplyWithTransition(theme, NotchCanvas);
        };

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
        HudOverlay.SetDock(NotchDock.Top);
        VerticalHudOverlay.Bind(_volumeService, _brightnessService);
        VerticalHudOverlay.SetDock(NotchDock.Left); // Will be updated when dock changes

        // Battery
        _batteryService.Initialize();
        BatteryIndicator.Bind(_batteryService);

        // Calendar
        _calendarService.Initialize();
        CalendarPanel_View.Bind(_calendarService);
        CalendarPanel.Visibility = _settings.ShowCalendar ? Visibility.Visible : Visibility.Collapsed;

        // File Shelf
        ShelfPanel.Bind(_shelfService);
        _shelfService.ShelfChanged += () => Dispatcher.Invoke(UpdateShelfVisibility);

        // Webcam mirror
        _webcamService.SetTargetFps(_settings.WebcamFps);
        if (_settings.ShowWebcam)
        {
            WebcamPanel.Visibility = Visibility.Visible;
            WebcamMirror.Bind(_webcamService);
        }

        // Vertical side panel: wire media + clock
        _mediaService.MediaInfo.PropertyChanged += (_, e) =>
        {
            Dispatcher.Invoke(() => UpdateSideMediaContent(_mediaService.MediaInfo, e.PropertyName));
        };
        _mediaService.SessionChanged += () =>
        {
            Dispatcher.Invoke(() => UpdateSideMediaContent(_mediaService.MediaInfo, null));
        };

        // Side visualizer
        _audioCaptureService.SpectrumUpdated += () =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_audioCaptureService.SpectrumData != null)
                    SideVisualizer.UpdateSpectrum(_audioCaptureService.SpectrumData);
            });
        };

        var sideClockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        sideClockTimer.Tick += (_, _) => UpdateSideClock();
        sideClockTimer.Start();
        UpdateSideClock();
        UpdateSideMediaContent(_mediaService.MediaInfo, null);

        // Fullscreen detection — hide notch when another app is fullscreen
        _fullscreenService.FullscreenChanged += isFs =>
        {
            Dispatcher.Invoke(() =>
            {
                if (isFs)
                {
                    // Hide the notch
                    NotchCanvas.Opacity = 0;
                    IsHitTestVisible = false;
                }
                else
                {
                    NotchCanvas.Opacity = 1;
                    IsHitTestVisible = true;
                }
            });
        };
        _fullscreenService.Start();

        // When HUD shows, expand notch and hide other content
        HudOverlay.HudShown += () => Dispatcher.Invoke(() =>
        {
            if (_vm.NotchState != NotchState.Open)
            {
                LiveActivity.Visibility = Visibility.Collapsed;
                ClockWidget.Visibility = Visibility.Collapsed;
                BatteryIndicator.Visibility = Visibility.Collapsed;
                // Horizontal expansion for top dock
                _widthSpring.AnimateTo(NotchConstants.HudWidth, _currentWidth);
            }
        });
        HudOverlay.HudDismissed += () => Dispatcher.Invoke(() =>
        {
            UpdateClosedContentVisibility();
            if (_settings.ShowBattery)
                BatteryIndicator.Visibility = Visibility.Visible;
            if (_vm.NotchState != NotchState.Open)
            {
                // Return to normal closed width
                _widthSpring.AnimateTo(NotchConstants.ClosedWidth, _currentWidth);
            }
        });

        // Vertical HUD expansion for side docks
        VerticalHudOverlay.HudShown += () => Dispatcher.Invoke(() =>
        {
            if (_vm.NotchState != NotchState.Open)
            {
                SideTimeSmall.Visibility = Visibility.Collapsed;
                // Vertical expansion for side docks (expand height)
                _heightSpring.AnimateTo(NotchConstants.HudWidth, _currentHeight);
            }
        });
        VerticalHudOverlay.HudDismissed += () => Dispatcher.Invoke(() =>
        {
            SideTimeSmall.Visibility = Visibility.Visible;
            if (_vm.NotchState != NotchState.Open)
            {
                // Return to normal closed height (which is swapped for vertical)
                _heightSpring.AnimateTo(NotchConstants.ClosedWidth, _currentHeight);
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

    #region Vertical Side Content

    private void UpdateSideClock()
    {
        var now = DateTime.Now;
        SideTimeText.Text = now.ToString("HH:mm");
        SideDateText.Text = now.ToString("ddd d", System.Globalization.CultureInfo.CurrentCulture);
        SideTimeSmall.Text = now.ToString("HH:mm");
    }

    private void UpdateSideMediaContent(MediaInfo info, string? prop)
    {
        if (prop == null || prop == nameof(info.Title))
            SideSongTitle.Text = info.Title;
        if (prop == null || prop == nameof(info.Artist))
            SideArtistName.Text = info.Artist;
        if (prop == null || prop == nameof(info.AlbumArt))
            SideAlbumArt.Source = info.AlbumArt;
        if (prop == null || prop == nameof(info.IsPlaying))
            SidePlayIcon.Text = info.IsPlaying ? "⏸" : "▶";
        if (prop == null || prop == nameof(info.HasMedia))
            SideMusicPanel.Visibility = info.HasMedia ? Visibility.Visible : Visibility.Collapsed;

        // Progress bar + time
        if (prop == null || prop == nameof(info.Position) || prop == nameof(info.ProgressPercent))
        {
            SideElapsedText.Text = FormatTimeSpan(info.Position);
            UpdateSideProgressBar(info.ProgressPercent);
        }
        if (prop == null || prop == nameof(info.Duration))
            SideDurationText.Text = FormatTimeSpan(info.Duration);

        // Visualizer bar color from album dominant color
        if (prop == null || prop == nameof(info.DominantColor))
        {
            var color = info.DominantColor;
            double brightness = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
            SideVisualizer.BarColor = brightness < 0.3 ? System.Windows.Media.Colors.White : color;
        }
    }

    private void UpdateSideProgressBar(double percent)
    {
        if (SideProgressFill.Parent is System.Windows.Controls.Border parent)
        {
            double parentWidth = parent.ActualWidth;
            // Only update if parent has valid width to prevent visual glitches
            if (parentWidth > 0 && !double.IsNaN(parentWidth) && !double.IsInfinity(parentWidth))
            {
                double targetWidth = Math.Max(0, Math.Min(percent * parentWidth, parentWidth));
                SideProgressFill.Width = targetWidth;
            }
        }
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        return ts.Hours > 0
            ? $"{ts.Hours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes}:{ts.Seconds:D2}";
    }

    private async void OnSidePlayClick(object sender, RoutedEventArgs e) =>
        await _mediaService.PlayPauseAsync();

    private async void OnSidePrevClick(object sender, RoutedEventArgs e) =>
        await _mediaService.PreviousAsync();

    private async void OnSideNextClick(object sender, RoutedEventArgs e) =>
        await _mediaService.NextAsync();

    private async void OnSideProgressBarClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Border bar) return;

        double barWidth = bar.ActualWidth;
        // Validate bar width before calculating seek position
        if (barWidth <= 0 || double.IsNaN(barWidth) || double.IsInfinity(barWidth)) return;

        var pos = e.GetPosition(bar);
        double percent = Math.Clamp(pos.X / barWidth, 0, 1);
        await _mediaService.SeekToAsync(percent);
    }

    private void OnSideSettingsClick(object sender, RoutedEventArgs e)
    {
        // In vertical view, expand settings on the right side dynamically
        if (_isSettingsExpanded)
            CollapseSettings();
        else
            ExpandSettings();
    }

    #endregion

    public void OpenSettings()
    {
        // First ensure the notch is open
        if (_vm.NotchState != NotchState.Open)
            TransitionToOpen();

        ExpandSettings();
    }

    private void OnSettingsGearClick(object sender, RoutedEventArgs e)
    {
        if (_isSettingsExpanded)
            CollapseSettings();
        else
            ExpandSettings();
    }

    private void ExpandSettings()
    {
        _isSettingsExpanded = true;

        if (_dock == NotchDock.Top)
        {
            // Top dock: Load settings and grow the notch taller (slide down)
            InlineSettings.LoadSettings(_settings);
            InlineSettings.Visibility = Visibility.Visible;

            double targetH = _shelfService.HasItems
                ? NotchConstants.OpenSettingsHeightWithShelf
                : NotchConstants.OpenSettingsHeight;
            _heightSpring.Response = 0.42;
            _heightSpring.DampingFraction = 0.80;
            _heightSpring.AnimateTo(targetH, _currentHeight);
        }
        else
        {
            // Side dock: Load settings in vertical panel and expand width to the right
            VerticalInlineSettings.LoadSettings(_settings);
            VerticalSettingsPanel.Visibility = Visibility.Visible;

            double targetW = NotchConstants.SideOpenWidth + 280; // Add settings panel width
            _widthSpring.Response = 0.42;
            _widthSpring.DampingFraction = 0.80;
            _widthSpring.AnimateTo(targetW, _currentWidth);
        }
    }

    private void CollapseSettings()
    {
        _isSettingsExpanded = false;

        if (_dock == NotchDock.Top)
        {
            // Top dock: Hide settings and shrink back to normal open height
            InlineSettings.Visibility = Visibility.Collapsed;

            double targetH = _shelfService.HasItems
                ? NotchConstants.OpenHeightWithShelf
                : NotchConstants.OpenHeight;
            _heightSpring.Response = 0.42;
            _heightSpring.DampingFraction = 0.80;
            _heightSpring.AnimateTo(targetH, _currentHeight);
        }
        else
        {
            // Side dock: Hide settings panel and shrink width back to normal
            VerticalSettingsPanel.Visibility = Visibility.Collapsed;

            _widthSpring.Response = 0.42;
            _widthSpring.DampingFraction = 0.80;
            _widthSpring.AnimateTo(NotchConstants.SideOpenWidth, _currentWidth);
        }
    }

    // Removed OpenSettingsWindow - now using integrated settings panel

    /// <summary>
    /// Position the window once: centered at top of primary screen,
    /// sized to fit the largest notch state (open) + shadow padding.
    /// The window never moves or resizes after this.
    /// </summary>
    private void PositionFixedWindow()
    {
        double padding = NotchConstants.WindowPadding;
        double maxOpenWidth = new[] {
            NotchConstants.OpenWidth,
            NotchConstants.OpenWidthWithCalendar,
            NotchConstants.OpenWidthWithWebcam,
            NotchConstants.OpenWidthWithCalendarAndWebcam
        }.Max();
        _fixedWindowWidth = maxOpenWidth + padding * 2;
        double maxHeight = Math.Max(NotchConstants.OpenSettingsHeightWithShelf,
            Math.Max(NotchConstants.OpenHeightWithShelf, NotchConstants.OpenSettingsHeight));
        _fixedWindowHeight = maxHeight + padding + 10;

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
        Geometry geometry;

        if (_isDragging)
            geometry = NotchShape.CreateNotchGeometry(rect, topR, bottomR);
        else if (_dock == NotchDock.Left)
            geometry = NotchShape.CreateSideNotchGeometry(rect, topR, bottomR, isLeft: true);
        else if (_dock == NotchDock.Right)
            geometry = NotchShape.CreateSideNotchGeometry(rect, topR, bottomR, isLeft: false);
        else
            geometry = NotchShape.CreateNotchGeometry(rect, topR, bottomR);

        NotchPath.Data = geometry;
        ShadowPath.Data = geometry;

        double windowW = ActualWidth > 0 ? ActualWidth : Width;
        double windowH = ActualHeight > 0 ? ActualHeight : Height;

        double offsetX, offsetY;

        if (_isDragging)
        {
            // During drag: use fixed drag pill size to prevent window resize glitches
            double dragW = NotchConstants.ClosedWidth * 0.8;
            double dragH = NotchConstants.ClosedHeight * 0.8;
            Width = dragW;
            Height = dragH;
            windowW = dragW;
            windowH = dragH;
            offsetX = 0;
            offsetY = 0;

            // Position is already updated in OnNotchMouseMove to follow cursor smoothly
            // Don't recalculate here to avoid visual jitter
        }
        else if (_dock == NotchDock.Left)
        {
            offsetX = 0; // Anchored to left edge
            offsetY = (windowH - h) / 2;
        }
        else if (_dock == NotchDock.Right)
        {
            offsetX = windowW - w; // Anchored to right edge
            offsetY = (windowH - h) / 2;
        }
        else
        {
            // Top dock: centered horizontally at top
            offsetX = (windowW - w) / 2;
            offsetY = 0;
        }

        System.Windows.Controls.Canvas.SetLeft(NotchPath, offsetX);
        System.Windows.Controls.Canvas.SetTop(NotchPath, offsetY);
        System.Windows.Controls.Canvas.SetLeft(ShadowPath, offsetX);
        System.Windows.Controls.Canvas.SetTop(ShadowPath, offsetY);

        // Shadow opacity
        ShadowPath.Opacity = _currentShadowOpacity;

        // Content grid: positioned inside the notch shape (inset from corners)
        double contentLeft, contentTop, contentWidth, contentHeight;

        if (_dock == NotchDock.Left)
        {
            contentLeft = offsetX + topR;
            contentTop = offsetY + topR + bottomR;
            contentWidth = Math.Max(0, w - topR);
            contentHeight = Math.Max(0, h - 2 * (topR + bottomR));
        }
        else if (_dock == NotchDock.Right)
        {
            contentLeft = offsetX;
            contentTop = offsetY + topR + bottomR;
            contentWidth = Math.Max(0, w - topR);
            contentHeight = Math.Max(0, h - 2 * (topR + bottomR));
        }
        else
        {
            contentLeft = offsetX + topR + bottomR;
            contentTop = offsetY + topR;
            contentWidth = Math.Max(0, w - (topR + bottomR) * 2);
            contentHeight = Math.Max(0, h - topR - bottomR);
        }

        System.Windows.Controls.Canvas.SetLeft(ContentGrid, contentLeft);
        System.Windows.Controls.Canvas.SetTop(ContentGrid, contentTop);
        ContentGrid.Width = contentWidth;
        ContentGrid.Height = contentHeight;

        // Content opacity — switch between horizontal and vertical panels
        if (_dock != NotchDock.Top)
        {
            VerticalOpenContent.Opacity = _currentContentOpacity;
            VerticalClosedContent.Opacity = 1.0 - _currentContentOpacity;
            OpenContent.Opacity = 0;
            ClosedContent.Opacity = 0;
        }
        else
        {
            OpenContent.Opacity = _currentContentOpacity;
            ClosedContent.Opacity = 1.0 - _currentContentOpacity;
            VerticalOpenContent.Opacity = 0;
            VerticalClosedContent.Opacity = 0;
        }

        // Canvas covers the full window
        NotchCanvas.Width = windowW;
        NotchCanvas.Height = windowH;
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

        if (_dock == NotchDock.Top)
        {
            double openW = GetOpenWidth();
            double openH = _shelfService.HasItems
                ? NotchConstants.OpenHeightWithShelf
                : NotchConstants.OpenHeight;

            _widthSpring.AnimateTo(openW, _currentWidth);
            _heightSpring.AnimateTo(openH, _currentHeight);
        }
        else
        {
            // Side dock: width = protrusion from edge, height = tall axis
            double openW = NotchConstants.SideOpenWidth;
            double openH = NotchConstants.SideOpenHeight;

            _widthSpring.AnimateTo(openW, _currentWidth);
            _heightSpring.AnimateTo(openH, _currentHeight);
        }

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

        if (_dock == NotchDock.Top)
        {
            _widthSpring.AnimateTo(NotchConstants.PeekWidth, _currentWidth);
            _heightSpring.AnimateTo(NotchConstants.PeekHeight, _currentHeight);
        }
        else
        {
            _widthSpring.AnimateTo(NotchConstants.PeekHeight, _currentWidth);
            _heightSpring.AnimateTo(NotchConstants.PeekWidth, _currentHeight);
        }
    }

    private void TransitionToClose()
    {
        if (_vm.NotchState == NotchState.Closed) return;

        // Collapse settings if open
        if (_isSettingsExpanded)
        {
            _isSettingsExpanded = false;
            InlineSettings.Visibility = Visibility.Collapsed;
            VerticalSettingsPanel.Visibility = Visibility.Collapsed;
        }

        _vm.Close();

        _widthSpring.Response = 0.45;
        _widthSpring.DampingFraction = 1.0;
        _heightSpring.Response = 0.45;
        _heightSpring.DampingFraction = 1.0;

        if (_dock == NotchDock.Top)
        {
            _widthSpring.AnimateTo(NotchConstants.ClosedWidth, _currentWidth);
            _heightSpring.AnimateTo(NotchConstants.ClosedHeight, _currentHeight);
        }
        else
        {
            // Side: narrow width, tall height
            _widthSpring.AnimateTo(NotchConstants.ClosedHeight, _currentWidth);
            _heightSpring.AnimateTo(NotchConstants.ClosedWidth, _currentHeight);
        }

        _topRadiusSpring.AnimateTo(NotchConstants.ClosedTopRadius, _currentTopRadius);
        _bottomRadiusSpring.AnimateTo(NotchConstants.ClosedBottomRadius, _currentBottomRadius);
        _shadowOpacitySpring.AnimateTo(0.0, _currentShadowOpacity);
        _contentOpacitySpring.AnimateTo(0.0, _currentContentOpacity);
    }

    #endregion

    #region Hover Logic

    private void OnNotchMouseEnter(object sender, MouseEventArgs e)
    {
        if (_isDragging || _isMouseDownOnNotch) return;
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
                    if (_isDragging || _isMouseDownOnNotch) return;
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
                    if (_isDragging || _isMouseDownOnNotch) return;
                    if (_isMouseOverNotch)
                        TransitionToOpen();
                };
                _hoverOpenTimer.Start();
            }
        }
    }

    private void OnNotchMouseLeave(object sender, MouseEventArgs e)
    {
        if (_isDragging) return;
        _isMouseOverNotch = false;
        _hoverOpenTimer?.Stop();

        // Don't close while a context menu is open
        if (_isContextMenuOpen) return;

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
                if (!_isMouseOverNotch && !_isContextMenuOpen)
                    TransitionToClose();
            };
            _hoverCloseTimer.Start();
        }
    }

    private void OnNotchMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Don't capture for drag when open — let buttons/controls work normally
        if (_vm.NotchState == NotchState.Open && sender != NotchPath) return;

        var cursorPos = System.Windows.Forms.Cursor.Position;
        _dragStartScreen = new Point(cursorPos.X, cursorPos.Y);
        _dragLastScreen = _dragStartScreen;
        _dragLastTime = DateTime.UtcNow;
        _dragVelocityX = 0;
        _dragVelocityY = 0;
        _isDragging = false;
        _isMouseDownOnNotch = true;

        // Suppress hover timers while mouse is down
        _hoverOpenTimer?.Stop();

        NotchPath.CaptureMouse();
    }

    private void OnNotchMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isMouseDownOnNotch && !_isDragging) return;
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;

        // Use raw cursor position to avoid drift from window movement
        var cursorPos = System.Windows.Forms.Cursor.Position;
        var currentScreen = new Point(cursorPos.X, cursorPos.Y);

        // Check drag threshold
        if (_isMouseDownOnNotch && !_isDragging)
        {
            double dx = currentScreen.X - _dragStartScreen.X;
            double dy = currentScreen.Y - _dragStartScreen.Y;
            if (Math.Sqrt(dx * dx + dy * dy) > NotchConstants.DragThreshold)
            {
                BeginDrag(currentScreen);
            }
            else return;
        }

        if (!_isDragging) return;

        // Track velocity (exponential smoothing with clamped time delta)
        var now = DateTime.UtcNow;
        double dt = (now - _dragLastTime).TotalSeconds;
        // Clamp dt to prevent huge velocity spikes from timer irregularities
        dt = Math.Clamp(dt, 0.001, 0.1);

        if (dt > 0.001)
        {
            double vx = (currentScreen.X - _dragLastScreen.X) / dt;
            double vy = (currentScreen.Y - _dragLastScreen.Y) / dt;
            _dragVelocityX = _dragVelocityX * 0.6 + vx * 0.4;
            _dragVelocityY = _dragVelocityY * 0.6 + vy * 0.4;
        }
        _dragLastScreen = currentScreen;
        _dragLastTime = now;

        // Move window: use target drag pill size (not animated current size) to prevent drift
        var dpi = ScreenHelper.GetDpiScale(this);
        double pillW = NotchConstants.ClosedWidth * 0.8;
        double pillH = NotchConstants.ClosedHeight * 0.8;
        Left = currentScreen.X / dpi - pillW / 2;
        Top = currentScreen.Y / dpi - pillH / 2;
    }

    private void OnNotchMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isMouseDownOnNotch = false;

        // Always release mouse capture to prevent stuck states
        if (NotchPath.IsMouseCaptured)
            NotchPath.ReleaseMouseCapture();

        if (_isDragging)
        {
            EndDrag();
        }
        else
        {
            // Short click — normal behavior (no movement occurred)
            if (_vm.NotchState == NotchState.Closed || _vm.NotchState == NotchState.Peeking)
                TransitionToOpen();
        }
    }

    private void BeginDrag(Point currentScreenPos)
    {
        _isDragging = true;
        _isMouseDownOnNotch = false;

        // Target drag pill size
        double pillW = NotchConstants.ClosedWidth * 0.8;
        double pillH = NotchConstants.ClosedHeight * 0.8;

        // If coming from a side dock, immediately set dimensions to horizontal
        if (_dock == NotchDock.Left || _dock == NotchDock.Right)
        {
            _currentWidth = pillW;
            _currentHeight = pillH;
        }

        // Set window to drag pill size immediately to prevent resize glitches
        Width = pillW;
        Height = pillH;

        // Center window on cursor immediately
        var dpi = ScreenHelper.GetDpiScale(this);
        Left = currentScreenPos.X / dpi - pillW / 2;
        Top = currentScreenPos.Y / dpi - pillH / 2;

        // Switch to horizontal content during drag
        ClosedContent.Visibility = Visibility.Visible;
        OpenContent.Visibility = Visibility.Visible;
        VerticalClosedContent.Visibility = Visibility.Collapsed;
        VerticalOpenContent.Visibility = Visibility.Collapsed;

        // Close the notch to a small dragging pill
        if (_vm.NotchState != NotchState.Closed)
        {
            _vm.Close();
            _isSettingsExpanded = false;
            InlineSettings.Visibility = Visibility.Collapsed;
        }

        // Animate to compact drag pill size
        _widthSpring.Response = 0.25;
        _widthSpring.DampingFraction = 0.90;
        _heightSpring.Response = 0.25;
        _heightSpring.DampingFraction = 0.90;
        _widthSpring.AnimateTo(pillW, _currentWidth);
        _heightSpring.AnimateTo(pillH, _currentHeight);
        _shadowOpacitySpring.AnimateTo(0.5, _currentShadowOpacity);
        _contentOpacitySpring.AnimateTo(0.0, _currentContentOpacity);
    }

    private void EndDrag()
    {
        _isDragging = false;

        var screenBounds = ScreenHelper.GetPrimaryScreenBounds(this);
        var dpi = ScreenHelper.GetDpiScale(this);
        double cursorX = _dragLastScreen.X / dpi;
        double cursorY = _dragLastScreen.Y / dpi;

        // Determine target dock based on position + velocity
        NotchDock targetDock = NotchDock.Top;
        double edgeDist = NotchConstants.EdgeSnapDistance;
        double throwV = NotchConstants.ThrowVelocityThreshold;

        bool nearLeft = cursorX - screenBounds.Left < edgeDist || _dragVelocityX < -throwV;
        bool nearRight = screenBounds.Right - cursorX < edgeDist || _dragVelocityX > throwV;
        bool nearTop = cursorY - screenBounds.Top < edgeDist * 1.5;

        if (nearLeft && !nearTop)
            targetDock = NotchDock.Left;
        else if (nearRight && !nearTop)
            targetDock = NotchDock.Right;
        else
            targetDock = NotchDock.Top;

        DockTo(targetDock);
    }

    private void DockTo(NotchDock dock)
    {
        _dock = dock;

        // Reset state so subsequent TransitionToOpen() isn't skipped
        _vm.Close();

        RepositionForDock();

        // Update HUD overlay dock orientation
        VerticalHudOverlay.SetDock(dock);

        // Switch content panels based on dock orientation
        if (_dock == NotchDock.Top)
        {
            ClosedContent.Visibility = Visibility.Visible;
            OpenContent.Visibility = Visibility.Visible;
            VerticalClosedContent.Visibility = Visibility.Collapsed;
            VerticalOpenContent.Visibility = Visibility.Collapsed;
        }
        else
        {
            ClosedContent.Visibility = Visibility.Collapsed;
            OpenContent.Visibility = Visibility.Collapsed;
            VerticalClosedContent.Visibility = Visibility.Visible;
            VerticalOpenContent.Visibility = Visibility.Visible;
        }

        // Animate back to closed dimensions
        _widthSpring.Response = 0.40;
        _widthSpring.DampingFraction = 0.82;
        _heightSpring.Response = 0.40;
        _heightSpring.DampingFraction = 0.82;

        if (_dock == NotchDock.Top)
        {
            _widthSpring.AnimateTo(NotchConstants.ClosedWidth, _currentWidth);
            _heightSpring.AnimateTo(NotchConstants.ClosedHeight, _currentHeight);
        }
        else
        {
            // Side dock: swap width/height (tall & narrow)
            _widthSpring.AnimateTo(NotchConstants.ClosedHeight, _currentWidth);
            _heightSpring.AnimateTo(NotchConstants.ClosedWidth, _currentHeight);
        }

        _topRadiusSpring.AnimateTo(NotchConstants.ClosedTopRadius, _currentTopRadius);
        _bottomRadiusSpring.AnimateTo(NotchConstants.ClosedBottomRadius, _currentBottomRadius);
        _shadowOpacitySpring.AnimateTo(0.0, _currentShadowOpacity);
        _contentOpacitySpring.AnimateTo(0.0, _currentContentOpacity);
    }

    private void RepositionForDock()
    {
        var screenBounds = ScreenHelper.GetPrimaryScreenBounds(this);

        switch (_dock)
        {
            case NotchDock.Top:
                Width = _fixedWindowWidth;
                Height = _fixedWindowHeight;
                Left = screenBounds.Left + (screenBounds.Width - _fixedWindowWidth) / 2;
                Top = screenBounds.Top - 2;
                break;

            case NotchDock.Left:
                // Vertical window on the left edge
                Width = _fixedWindowHeight;
                Height = _fixedWindowWidth;
                Left = screenBounds.Left - 2;
                Top = screenBounds.Top + (screenBounds.Height - _fixedWindowWidth) / 2;
                break;

            case NotchDock.Right:
                // Vertical window on the right edge
                Width = _fixedWindowHeight;
                Height = _fixedWindowWidth;
                Left = screenBounds.Right - _fixedWindowHeight + 2;
                Top = screenBounds.Top + (screenBounds.Height - _fixedWindowWidth) / 2;
                break;
        }
    }

    private void OnContentGridMouseMove(object sender, MouseEventArgs e)
    {
        // Forward to drag handler
        OnNotchMouseMove(sender, e);
    }

    private void OnContentGridMouseUp(object sender, MouseButtonEventArgs e)
    {
        // Handle drag end or click-to-open
        OnNotchMouseUp(sender, e);
    }

    private void OnRootGridMouseLeave(object sender, MouseEventArgs e)
    {
        // If dragging and mouse leaves window (very fast movement), end drag safely
        if (_isDragging && e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            _isMouseDownOnNotch = false;
            if (NotchPath.IsMouseCaptured)
                NotchPath.ReleaseMouseCapture();
            EndDrag();
        }
    }

    private void OnNotchMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        // Only adjust volume in closed/peeking state (open state scrolls content normally)
        if (_vm.NotchState == NotchState.Open) return;

        if (_settings.ShowVolumeHud && _volumeService != null)
        {
            float current = _volumeService.Volume;
            // e.Delta is typically 120 per notch; scale to ~5% per scroll tick
            float delta = (e.Delta / 120f) * 0.05f;
            _volumeService.SetVolume(Math.Clamp(current + delta, 0f, 1f));
            e.Handled = true;
        }
    }

    private void OnNotchRightClick(object sender, MouseButtonEventArgs e)
    {
        var menu = CreateStyledContextMenu();

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings" };
        settingsItem.Click += (_, _) => OpenSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var quitItem = new System.Windows.Controls.MenuItem { Header = "Quit" };
        quitItem.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(quitItem);

        TrackContextMenu(menu);
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void OnGlobalHotkeyPressed()
    {
        Dispatcher.Invoke(() =>
        {
            if (_vm.NotchState == NotchState.Open)
                TransitionToClose();
            else
                TransitionToOpen();
        });
    }

    /// <summary>
    /// Tracks a context menu so the notch doesn't close while it's open.
    /// </summary>
    public void TrackContextMenu(System.Windows.Controls.ContextMenu menu)
    {
        _isContextMenuOpen = true;
        menu.Closed += (_, _) =>
        {
            _isContextMenuOpen = false;
            // Re-check if mouse has left while menu was open
            if (!_isMouseOverNotch && _vm.NotchState == NotchState.Open)
            {
                _hoverCloseTimer?.Stop();
                _hoverCloseTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(NotchConstants.HoverCloseDelayMs)
                };
                _hoverCloseTimer.Tick += (_, _) =>
                {
                    _hoverCloseTimer!.Stop();
                    if (!_isMouseOverNotch && !_isContextMenuOpen)
                        TransitionToClose();
                };
                _hoverCloseTimer.Start();
            }
        };
    }

    /// <summary>
    /// Creates a styled ContextMenu matching the app's dark/light theme with rounded corners.
    /// </summary>
    public System.Windows.Controls.ContextMenu CreateStyledContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        // Create a border template with rounded corners
        var template = new ControlTemplate(typeof(System.Windows.Controls.ContextMenu));
        var frameworkFactory = new FrameworkElementFactory(typeof(Border));
        frameworkFactory.SetValue(Border.BackgroundProperty, new DynamicResourceExtension("NotchFillBrush"));
        frameworkFactory.SetValue(Border.BorderBrushProperty, new DynamicResourceExtension("SeparatorBrush"));
        frameworkFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        frameworkFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
        frameworkFactory.SetValue(Border.PaddingProperty, new Thickness(6, 8, 6, 8));

        // Add drop shadow effect
        var shadowEffect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = System.Windows.Media.Colors.Black,
            Direction = 270,
            ShadowDepth = 2,
            BlurRadius = 12,
            Opacity = 0.3
        };
        frameworkFactory.SetValue(Border.EffectProperty, shadowEffect);

        var scrollFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.ScrollViewer));
        scrollFactory.SetValue(System.Windows.Controls.ScrollViewer.CanContentScrollProperty, true);
        scrollFactory.SetValue(System.Windows.Controls.ScrollViewer.HorizontalScrollBarVisibilityProperty,
            System.Windows.Controls.ScrollBarVisibility.Disabled);
        scrollFactory.SetValue(System.Windows.Controls.ScrollViewer.VerticalScrollBarVisibilityProperty,
            System.Windows.Controls.ScrollBarVisibility.Auto);

        var stackFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.StackPanel));
        stackFactory.SetValue(System.Windows.Controls.StackPanel.IsItemsHostProperty, true);

        scrollFactory.AppendChild(stackFactory);
        frameworkFactory.AppendChild(scrollFactory);
        template.VisualTree = frameworkFactory;
        menu.Template = template;

        // Style for MenuItems with rounded hover
        var menuItemStyle = new Style(typeof(System.Windows.Controls.MenuItem));
        menuItemStyle.Setters.Add(new Setter(System.Windows.Controls.MenuItem.ForegroundProperty,
            FindResource("TextSecondaryBrush")));
        menuItemStyle.Setters.Add(new Setter(System.Windows.Controls.MenuItem.FontSizeProperty, 12.0));
        menuItemStyle.Setters.Add(new Setter(System.Windows.Controls.MenuItem.PaddingProperty,
            new Thickness(12, 6, 24, 6)));
        menuItemStyle.Setters.Add(new Setter(System.Windows.Controls.MenuItem.MarginProperty,
            new Thickness(2, 1, 2, 1)));
        menuItemStyle.Setters.Add(new Setter(System.Windows.Controls.MenuItem.CursorProperty,
            System.Windows.Input.Cursors.Hand));

        // Create template for menu item with rounded background
        var itemTemplate = new ControlTemplate(typeof(System.Windows.Controls.MenuItem));
        var itemBorder = new FrameworkElementFactory(typeof(Border));
        itemBorder.Name = "ItemBorder";
        itemBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        itemBorder.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);

        var contentPresenter = new FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
        contentPresenter.SetValue(System.Windows.Controls.ContentPresenter.ContentSourceProperty, "Header");
        contentPresenter.SetValue(System.Windows.Controls.ContentPresenter.MarginProperty, new Thickness(0));

        itemBorder.AppendChild(contentPresenter);
        itemTemplate.VisualTree = itemBorder;

        // Hover trigger with rounded background
        var hoverTrigger = new Trigger { Property = System.Windows.Controls.MenuItem.IsHighlightedProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(System.Windows.Controls.MenuItem.BackgroundProperty,
            FindResource("HoverOverlayBrush"), "ItemBorder"));
        hoverTrigger.Setters.Add(new Setter(System.Windows.Controls.MenuItem.ForegroundProperty,
            FindResource("TextPrimaryBrush")));
        itemTemplate.Triggers.Add(hoverTrigger);

        menuItemStyle.Setters.Add(new Setter(System.Windows.Controls.MenuItem.TemplateProperty, itemTemplate));
        menu.Resources.Add(typeof(System.Windows.Controls.MenuItem), menuItemStyle);

        // Style for Separator
        var separatorStyle = new Style(typeof(System.Windows.Controls.Separator));
        separatorStyle.Setters.Add(new Setter(System.Windows.Controls.Separator.BackgroundProperty,
            FindResource("SeparatorBrush")));
        separatorStyle.Setters.Add(new Setter(System.Windows.Controls.Separator.MarginProperty,
            new Thickness(8, 4, 8, 4)));
        separatorStyle.Setters.Add(new Setter(System.Windows.Controls.Separator.HeightProperty, 1.0));
        menu.Resources.Add(typeof(System.Windows.Controls.Separator), separatorStyle);

        return menu;
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
        _fullscreenService.Dispose();
        _webcamService.Dispose();
        _globalHotkey.Dispose();
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

        // Webcam
        _webcamService.SetTargetFps(settings.WebcamFps);
        if (settings.ShowWebcam)
        {
            WebcamPanel.Visibility = Visibility.Visible;
            WebcamMirror.Bind(_webcamService);
            if (_webcamService.MaxCameraFps > 0)
            {
                InlineSettings.UpdateWebcamFpsMax(_webcamService.MaxCameraFps);
                VerticalInlineSettings.UpdateWebcamFpsMax(_webcamService.MaxCameraFps);
            }
        }
        else
        {
            WebcamPanel.Visibility = Visibility.Collapsed;
            WebcamMirror.StopCamera();
        }

        // Re-adjust notch size if currently open
        if (_vm.NotchState == NotchState.Open)
        {
            _widthSpring.AnimateTo(GetOpenWidth(), _currentWidth);

            if (!_isSettingsExpanded)
            {
                double targetH = _shelfService.HasItems
                    ? NotchConstants.OpenHeightWithShelf
                    : NotchConstants.OpenHeight;
                _heightSpring.AnimateTo(targetH, _currentHeight);
            }
        }
    }

    private double GetOpenWidth()
    {
        bool cal = _settings.ShowCalendar;
        bool cam = _settings.ShowWebcam;
        if (cal && cam) return NotchConstants.OpenWidthWithCalendarAndWebcam;
        if (cal) return NotchConstants.OpenWidthWithCalendar;
        if (cam) return NotchConstants.OpenWidthWithWebcam;
        return NotchConstants.OpenWidth;
    }

    private void CompactVisualizer_SetVisible(bool visible)
    {
        // The visualizer is inside LiveActivity; toggle via its property
        if (LiveActivity.FindName("CompactVisualizer") is UIElement viz)
            viz.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    #region File Shelf / Drag-Drop

    private void UpdateShelfVisibility()
    {
        bool hasItems = _shelfService.HasItems;
        ShelfPanel.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;

        // Adjust open height if notch is open
        if (_vm.NotchState == NotchState.Open && !_isSettingsExpanded)
        {
            double targetHeight = hasItems
                ? NotchConstants.OpenHeightWithShelf
                : NotchConstants.OpenHeight;
            _heightSpring.AnimateTo(targetHeight, _currentHeight);
        }
    }

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;

            // Open the notch if closed/peeking to show drop zone
            if (_vm.NotchState != NotchState.Open)
                TransitionToOpen();

            // Show drop zone and expand for shelf
            ShelfPanel.Visibility = Visibility.Visible;
            ShelfPanel.ShowDropZone();

            if (!_isSettingsExpanded)
            {
                _heightSpring.AnimateTo(NotchConstants.OpenHeightWithShelf, _currentHeight);
            }

            e.Handled = true;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        ShelfPanel.HideDropZone();

        // If no items, collapse shelf and shrink back
        if (!_shelfService.HasItems)
        {
            ShelfPanel.Visibility = Visibility.Collapsed;
            if (_vm.NotchState == NotchState.Open && !_isSettingsExpanded)
            {
                _heightSpring.AnimateTo(NotchConstants.OpenHeight, _currentHeight);
            }
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        ShelfPanel.HideDropZone();

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
            {
                _shelfService.AddFiles(files);
            }
            e.Handled = true;
        }
    }

    #endregion
}
