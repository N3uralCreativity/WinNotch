using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinNotch.Models;
using WinNotch.Plugins;
using WinNotch.Services;

namespace PetWidgetPlugin;

[WinNotchPlugin("com.winnotch.petwidget", "Pet Widget", "1.0.0", "WinNotch Team")]
public class PetWidgetPlugin : PluginBase, IUIPlugin
{
    public override string Id => "com.winnotch.petwidget";
    public override string Name => "Pet Widget";
    public override string Version => "1.0.0";
    public override string Author => "WinNotch Team";
    public override string Description => "A cute animated pet that lives in your notch! It reacts to your activity and has different moods.";
    public override string MinimumWinNotchVersion => "0.3.0";

    // Services
    private ThemeService? _themeService;
    private BatteryService? _batteryService;

    // Closed UI
    private TextBlock? _closedEmoji;

    // Open UI
    private Border? _openContainer;
    private Canvas? _petCanvas;
    private TextBlock? _petBody;
    private TextBlock? _moodLabel;
    private TextBlock? _statusText;
    private ProgressBar? _happinessBar;

    // State
    private DispatcherTimer? _animTimer;
    private DispatcherTimer? _moodTimer;
    private readonly Random _rng = new();
    private PetMood _mood = PetMood.Happy;
    private PetAction _action = PetAction.Idle;
    private int _happiness = 80;
    private int _petX = 60;
    private int _direction = 1;
    private int _idleTicks;
    private int _sleepZCount;
    private bool _isLight;
    private DateTime _lastPetTime = DateTime.MinValue;

    public override async Task InitializeAsync(IPluginContext context)
    {
        await base.InitializeAsync(context);

        _themeService = context.ThemeService;
        _batteryService = context.BatteryService;
        _isLight = _themeService.IsLight;
        _themeService.ThemeChanged += OnThemeChanged;

        // Animation timer — ~12fps for smooth movement
        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _animTimer.Tick += OnAnimTick;
        _animTimer.Start();

        // Mood decay timer — every 30s
        _moodTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _moodTimer.Tick += OnMoodTick;
        _moodTimer.Start();
    }

    public UIElement? GetUIElement(UIPluginLocation location)
    {
        return location switch
        {
            UIPluginLocation.ClosedContent => BuildClosedUI(),
            UIPluginLocation.OpenContent => BuildOpenUI(),
            _ => null
        };
    }

    public void OnNotchStateChanged(NotchState newState)
    {
        if (newState == NotchState.Open)
            UpdateOpenUI();
    }

    // ── Closed UI ──────────────────────────────────────────────
    private UIElement BuildClosedUI()
    {
        _closedEmoji = new TextBlock
        {
            Text = GetMoodEmoji(),
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0),
            ToolTip = $"Pet is {_mood}"
        };
        return _closedEmoji;
    }

    // ── Open UI ────────────────────────────────────────────────
    private UIElement BuildOpenUI()
    {
        _openContainer = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12),
            Margin = new Thickness(8, 4, 8, 4),
            Background = GetCardBackground(),
            MinHeight = 140
        };

        var root = new StackPanel();

        // Header row
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _moodLabel = new TextBlock
        {
            Text = $"{GetMoodEmoji()} {_mood}",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = GetPrimaryForeground(),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_moodLabel, 0);
        header.Children.Add(_moodLabel);

        var petButton = new Button
        {
            Content = "Pet 🤚",
            FontSize = 11,
            Padding = new Thickness(8, 3, 8, 3),
            Background = new SolidColorBrush(_isLight ? Color.FromRgb(230, 230, 235) : Color.FromRgb(60, 60, 65)),
            Foreground = GetPrimaryForeground(),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        petButton.Click += OnPetClicked;
        Grid.SetColumn(petButton, 1);
        header.Children.Add(petButton);

        root.Children.Add(header);

        // Pet canvas — where the pet walks around
        _petCanvas = new Canvas
        {
            Height = 50,
            Margin = new Thickness(0, 6, 0, 6),
            ClipToBounds = true
        };

        // Floor line
        var floor = new Line
        {
            X1 = 0, X2 = 300, Y1 = 48, Y2 = 48,
            Stroke = new SolidColorBrush(_isLight ? Color.FromRgb(200, 200, 200) : Color.FromRgb(70, 70, 70)),
            StrokeThickness = 1
        };
        _petCanvas.Children.Add(floor);

        _petBody = new TextBlock
        {
            Text = GetPetSprite(),
            FontSize = 26,
            RenderTransformOrigin = new Point(0.5, 1)
        };
        Canvas.SetLeft(_petBody, _petX);
        Canvas.SetTop(_petBody, 12);
        _petCanvas.Children.Add(_petBody);

        root.Children.Add(_petCanvas);

        // Happiness bar
        var barRow = new Grid { Margin = new Thickness(0, 2, 0, 4) };
        barRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        barRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var barLabel = new TextBlock
        {
            Text = "❤️ ",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = GetSecondaryForeground()
        };
        Grid.SetColumn(barLabel, 0);
        barRow.Children.Add(barLabel);

        _happinessBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = _happiness,
            Height = 6,
            Background = new SolidColorBrush(_isLight ? Color.FromRgb(220, 220, 225) : Color.FromRgb(50, 50, 55)),
            Foreground = GetHappinessColor(),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(_happinessBar, 1);
        barRow.Children.Add(_happinessBar);

        root.Children.Add(barRow);

        // Status text
        _statusText = new TextBlock
        {
            Text = GetStatusText(),
            FontSize = 11,
            Foreground = GetSecondaryForeground(),
            TextWrapping = TextWrapping.Wrap
        };
        root.Children.Add(_statusText);

        _openContainer.Child = root;
        UpdateOpenUI();
        return _openContainer;
    }

    // ── Animation ──────────────────────────────────────────────
    private void OnAnimTick(object? sender, EventArgs e)
    {
        switch (_action)
        {
            case PetAction.Walking:
                _petX += _direction * 4;
                if (_petX > 160) { _direction = -1; PickNextAction(); }
                if (_petX < 10) { _direction = 1; PickNextAction(); }
                break;

            case PetAction.Idle:
                _idleTicks++;
                if (_idleTicks > 25) PickNextAction();
                break;

            case PetAction.Sleeping:
                _sleepZCount++;
                if (_sleepZCount > 60) { _action = PetAction.Idle; _idleTicks = 0; }
                break;

            case PetAction.Playing:
                _petX += (_rng.Next(2) == 0 ? 1 : -1) * 3;
                _petX = Math.Clamp(_petX, 10, 160);
                _idleTicks++;
                if (_idleTicks > 20) PickNextAction();
                break;
        }

        // Update pet sprite position
        if (_petBody != null)
        {
            Canvas.SetLeft(_petBody, _petX);
            _petBody.Text = GetPetSprite();
            _petBody.RenderTransform = _direction < 0 ? new ScaleTransform(-1, 1) : null;
        }

        // Update closed emoji occasionally
        if (_closedEmoji != null)
        {
            _closedEmoji.Text = GetMoodEmoji();
            _closedEmoji.ToolTip = $"Pet is {_mood}";
        }
    }

    private void PickNextAction()
    {
        _idleTicks = 0;
        _sleepZCount = 0;

        if (_happiness < 20)
        {
            _action = _rng.Next(3) == 0 ? PetAction.Sleeping : PetAction.Idle;
        }
        else if (_happiness > 70)
        {
            var roll = _rng.Next(100);
            _action = roll < 40 ? PetAction.Walking : roll < 70 ? PetAction.Playing : PetAction.Idle;
        }
        else
        {
            _action = _rng.Next(2) == 0 ? PetAction.Walking : PetAction.Idle;
        }

        if (_action == PetAction.Walking)
            _direction = _rng.Next(2) == 0 ? 1 : -1;
    }

    // ── Mood ───────────────────────────────────────────────────
    private void OnMoodTick(object? sender, EventArgs e)
    {
        // Happiness decays slowly
        _happiness = Math.Max(0, _happiness - 2);

        // Battery low makes pet worried
        if (_batteryService != null)
        {
            try
            {
                var pct = _batteryService.ChargePercent;
                if (pct < 15 && pct >= 0) _happiness = Math.Max(0, _happiness - 3);
            }
            catch { /* battery not available */ }
        }

        _mood = _happiness switch
        {
            >= 80 => PetMood.Ecstatic,
            >= 60 => PetMood.Happy,
            >= 40 => PetMood.Content,
            >= 20 => PetMood.Sad,
            _ => PetMood.Sleepy
        };

        UpdateOpenUI();
    }

    private void OnPetClicked(object sender, RoutedEventArgs e)
    {
        // Petting boosts happiness (cooldown 3s)
        if ((DateTime.Now - _lastPetTime).TotalSeconds < 3) return;
        _lastPetTime = DateTime.Now;

        _happiness = Math.Min(100, _happiness + 15);
        _action = PetAction.Playing;
        _idleTicks = 0;

        _mood = _happiness switch
        {
            >= 80 => PetMood.Ecstatic,
            >= 60 => PetMood.Happy,
            >= 40 => PetMood.Content,
            >= 20 => PetMood.Sad,
            _ => PetMood.Sleepy
        };

        UpdateOpenUI();
    }

    // ── UI Updates ─────────────────────────────────────────────
    private void UpdateOpenUI()
    {
        if (_moodLabel != null)
            _moodLabel.Text = $"{GetMoodEmoji()} {_mood}";

        if (_happinessBar != null)
        {
            _happinessBar.Value = _happiness;
            _happinessBar.Foreground = GetHappinessColor();
        }

        if (_statusText != null)
            _statusText.Text = GetStatusText();
    }

    private void OnThemeChanged()
    {
        _isLight = _themeService?.IsLight ?? false;

        if (_openContainer != null)
            _openContainer.Background = GetCardBackground();

        if (_moodLabel != null)
            _moodLabel.Foreground = GetPrimaryForeground();

        if (_statusText != null)
            _statusText.Foreground = GetSecondaryForeground();
    }

    // ── Sprites & Text ─────────────────────────────────────────
    private string GetPetSprite()
    {
        return _action switch
        {
            PetAction.Sleeping => "😴",
            PetAction.Playing => _idleTicks % 4 < 2 ? "😺" : "🙀",
            PetAction.Walking => _idleTicks % 2 == 0 ? "🐱" : "😺",
            _ => _mood switch
            {
                PetMood.Ecstatic => "😸",
                PetMood.Happy => "😺",
                PetMood.Content => "🐱",
                PetMood.Sad => "😿",
                PetMood.Sleepy => "😾",
                _ => "🐱"
            }
        };
    }

    private string GetMoodEmoji()
    {
        return _mood switch
        {
            PetMood.Ecstatic => "😸",
            PetMood.Happy => "😺",
            PetMood.Content => "🐱",
            PetMood.Sad => "😿",
            PetMood.Sleepy => "😴",
            _ => "🐱"
        };
    }

    private string GetStatusText()
    {
        var actionText = _action switch
        {
            PetAction.Walking => "Walking around...",
            PetAction.Playing => "Playing! 🎉",
            PetAction.Sleeping => "Zzz" + new string('z', Math.Min(_sleepZCount / 10, 5)) + "...",
            _ => _mood switch
            {
                PetMood.Ecstatic => "Purring loudly! ✨",
                PetMood.Happy => "Feeling good~",
                PetMood.Content => "Just hanging out.",
                PetMood.Sad => "Could use some attention...",
                PetMood.Sleepy => "So tired...",
                _ => "..."
            }
        };
        return actionText;
    }

    // ── Theme-aware colors ─────────────────────────────────────
    private Brush GetCardBackground() =>
        new SolidColorBrush(_isLight ? Color.FromArgb(180, 240, 240, 245) : Color.FromArgb(180, 35, 35, 38));

    private Brush GetPrimaryForeground() =>
        new SolidColorBrush(_isLight ? Color.FromRgb(30, 30, 30) : Color.FromRgb(240, 240, 240));

    private Brush GetSecondaryForeground() =>
        new SolidColorBrush(_isLight ? Color.FromRgb(100, 100, 100) : Color.FromRgb(170, 170, 170));

    private Brush GetHappinessColor()
    {
        if (_happiness >= 60) return new SolidColorBrush(Color.FromRgb(80, 200, 120));
        if (_happiness >= 30) return new SolidColorBrush(Color.FromRgb(240, 180, 50));
        return new SolidColorBrush(Color.FromRgb(220, 70, 70));
    }

    // ── Cleanup ────────────────────────────────────────────────
    public override Task ShutdownAsync()
    {
        _animTimer?.Stop();
        _moodTimer?.Stop();
        if (_themeService != null) _themeService.ThemeChanged -= OnThemeChanged;
        return base.ShutdownAsync();
    }

    public override void Dispose()
    {
        _animTimer?.Stop();
        _moodTimer?.Stop();
        _animTimer = null;
        _moodTimer = null;
        base.Dispose();
    }
}

internal enum PetMood
{
    Ecstatic,
    Happy,
    Content,
    Sad,
    Sleepy
}

internal enum PetAction
{
    Idle,
    Walking,
    Playing,
    Sleeping
}
