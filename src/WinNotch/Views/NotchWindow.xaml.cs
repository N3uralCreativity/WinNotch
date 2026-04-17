using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WinNotch.Helpers;
using WinNotch.Models;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Canvas = System.Windows.Controls.Canvas;

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

    // Hover debounce
    private DispatcherTimer? _hoverOpenTimer;
    private DispatcherTimer? _hoverCloseTimer;
    private bool _isMouseOverNotch;

    public NotchWindow()
    {
        InitializeComponent();

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

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowHelper.MakeOverlayWindow(this);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _vm.UpdateWindowPosition();
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
            _vm.NotchWidth = _currentWidth;
            _vm.NotchHeight = _currentHeight;
            _vm.TopCornerRadius = _currentTopRadius;
            _vm.BottomCornerRadius = _currentBottomRadius;
            _vm.ShadowOpacity = _currentShadowOpacity;
            _vm.ContentOpacity = _currentContentOpacity;

            UpdateNotchVisuals();
        }
    }

    private void UpdateNotchVisuals()
    {
        double w = _currentWidth;
        double h = _currentHeight;
        double topR = _currentTopRadius;
        double bottomR = _currentBottomRadius;
        double padding = NotchConstants.WindowPadding;

        // Generate notch geometry
        var rect = new Rect(0, 0, w, h);
        var geometry = NotchShape.CreateNotchGeometry(rect, topR, bottomR);

        NotchPath.Data = geometry;
        ShadowPath.Data = geometry;

        // Position paths centered in canvas with padding offset
        double offsetX = padding;
        double offsetY = 0; // Notch sits at top

        System.Windows.Controls.Canvas.SetLeft(NotchPath, offsetX);
        System.Windows.Controls.Canvas.SetTop(NotchPath, offsetY);
        System.Windows.Controls.Canvas.SetLeft(ShadowPath, offsetX);
        System.Windows.Controls.Canvas.SetTop(ShadowPath, offsetY);

        // Shadow opacity
        ShadowPath.Opacity = _currentShadowOpacity;

        // Content grid: positioned inside the notch
        System.Windows.Controls.Canvas.SetLeft(ContentGrid, offsetX + topR);
        System.Windows.Controls.Canvas.SetTop(ContentGrid, offsetY + topR);
        ContentGrid.Width = Math.Max(0, w - topR * 2);
        ContentGrid.Height = Math.Max(0, h - topR);

        // Content opacity
        OpenContent.Opacity = _currentContentOpacity;
        ClosedContent.Opacity = 1.0 - _currentContentOpacity;

        // Canvas sizing
        NotchCanvas.Width = w + padding * 2;
        NotchCanvas.Height = h + padding;

        // Update window position (centered on screen)
        _vm.UpdateWindowPosition();
    }

    #endregion

    #region State Transitions

    private void TransitionToOpen()
    {
        if (_vm.NotchState == NotchState.Open) return;
        _vm.Open();

        // Animate to open dimensions with opening spring (slightly bouncy)
        _widthSpring.Response = 0.42;
        _widthSpring.DampingFraction = 0.80;
        _heightSpring.Response = 0.42;
        _heightSpring.DampingFraction = 0.80;

        _widthSpring.AnimateTo(NotchConstants.OpenWidth, _currentWidth);
        _heightSpring.AnimateTo(NotchConstants.OpenHeight, _currentHeight);
        _topRadiusSpring.AnimateTo(NotchConstants.OpenTopRadius, _currentTopRadius);
        _bottomRadiusSpring.AnimateTo(NotchConstants.OpenBottomRadius, _currentBottomRadius);
        _shadowOpacitySpring.AnimateTo(0.7, _currentShadowOpacity);
        _contentOpacitySpring.AnimateTo(1.0, _currentContentOpacity);
    }

    private void TransitionToClose()
    {
        if (_vm.NotchState == NotchState.Closed) return;
        _vm.Close();

        // Animate to closed dimensions with closing spring (less bouncy, smoother)
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
            // Debounced open
            _hoverOpenTimer?.Stop();
            _hoverOpenTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(NotchConstants.HoverOpenDelayMs)
            };
            _hoverOpenTimer.Tick += (_, _) =>
            {
                _hoverOpenTimer.Stop();
                if (_isMouseOverNotch)
                    TransitionToOpen();
            };
            _hoverOpenTimer.Start();
        }
    }

    private void OnNotchMouseLeave(object sender, MouseEventArgs e)
    {
        _isMouseOverNotch = false;
        _hoverOpenTimer?.Stop();

        if (_vm.NotchState == NotchState.Open)
        {
            // Debounced close
            _hoverCloseTimer?.Stop();
            _hoverCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(NotchConstants.HoverCloseDelayMs)
            };
            _hoverCloseTimer.Tick += (_, _) =>
            {
                _hoverCloseTimer.Stop();
                if (!_isMouseOverNotch)
                    TransitionToClose();
            };
            _hoverCloseTimer.Start();
        }
    }

    private void OnNotchClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.NotchState == NotchState.Closed)
            TransitionToOpen();
    }

    private void OnNotchRightClick(object sender, MouseButtonEventArgs e)
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var settingsItem = new System.Windows.Controls.MenuItem { Header = "Settings" };
        settingsItem.Click += (_, _) => { /* TODO: open settings */ };
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
        base.OnClosed(e);
    }
}
