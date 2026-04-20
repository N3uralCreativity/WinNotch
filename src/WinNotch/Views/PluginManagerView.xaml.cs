using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinNotch.Plugins;

namespace WinNotch.Views;

public partial class PluginManagerView : UserControl
{
    private readonly PluginManager? _pluginManager;
    private readonly PluginLibraryService? _libraryService;

    public PluginManagerView()
    {
        InitializeComponent();
    }

    public PluginManagerView(PluginManager pluginManager, PluginLibraryService libraryService) : this()
    {
        _pluginManager = pluginManager;
        _libraryService = libraryService;

        CloseButton.Click += (_, _) => Window.GetWindow(this)?.Close();
        RefreshLibraryButton.Click += async (_, _) => await RefreshLibrary();
        BrowsePluginsButton.Click += (_, _) => BrowsePlugins();
        OpenPluginsFolderButton.Click += (_, _) => OpenPluginsFolder();

        LoadPlugins();
    }

    private void LoadPlugins()
    {
        PluginListPanel.Children.Clear();

        if (_pluginManager == null)
            return;

        InstalledCountText.Text = $"{_pluginManager.LoadedPlugins.Count} plugin{(_pluginManager.LoadedPlugins.Count == 1 ? string.Empty : "s")} installed";

        foreach (var plugin in _pluginManager.LoadedPlugins.OrderBy(plugin => plugin.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            PluginListPanel.Children.Add(CreatePluginCard(plugin));
        }

        if (_pluginManager.LoadedPlugins.Count == 0)
        {
            PluginListPanel.Children.Add(CreateEmptyState(
                "No plugins installed yet.",
                "Browse the library to start building your notch setup."));
        }
    }

    private Border CreatePluginCard(IPlugin plugin)
    {
        var card = new Border
        {
            Background = GetSurfaceBrush(),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(18),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var infoStack = new StackPanel();

        var topRow = new Grid();
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = plugin.Name,
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Foreground = GetPrimaryBrush()
        };
        Grid.SetColumn(title, 0);
        topRow.Children.Add(title);

        var version = new TextBlock
        {
            Text = $"v{plugin.Version}",
            FontSize = 11,
            Foreground = GetSecondaryBrush(),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(version, 1);
        topRow.Children.Add(version);

        infoStack.Children.Add(topRow);

        infoStack.Children.Add(new TextBlock
        {
            Text = $"by {plugin.Author}",
            FontSize = 11,
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = GetMutedBrush()
        });

        infoStack.Children.Add(new TextBlock
        {
            Text = plugin.Description,
            FontSize = 12,
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            Foreground = GetSecondaryBrush()
        });

        var tags = new WrapPanel
        {
            Margin = new Thickness(0, 12, 0, 0)
        };
        AddPluginTag(tags, plugin is IUIPlugin ? "UI" : null);
        AddPluginTag(tags, plugin is IServicePlugin ? "Service" : null);
        AddPluginTag(tags, plugin is IAnimationPlugin ? "Animation" : null);
        AddPluginTag(tags, $"ID {plugin.Id}");
        infoStack.Children.Add(tags);

        Grid.SetColumn(infoStack, 0);
        layout.Children.Add(infoStack);

        var controlStack = new StackPanel
        {
            Margin = new Thickness(18, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var enabledState = _pluginManager?.IsPluginEnabled(plugin.Id) ?? false;
        var stateLabel = new TextBlock
        {
            Text = enabledState ? "On" : "Off",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8),
            Foreground = enabledState ? GetAccentBrush() : GetMutedBrush(),
            FontWeight = FontWeights.SemiBold
        };
        controlStack.Children.Add(stateLabel);

        var switchToggle = new CheckBox
        {
            IsChecked = enabledState,
            Style = TryGetStyle("PluginSwitchStyle")
        };

        switchToggle.Checked += async (_, _) =>
        {
            stateLabel.Text = "On";
            stateLabel.Foreground = GetAccentBrush();
            if (_pluginManager != null)
            {
                await _pluginManager.EnablePluginAsync(plugin.Id);
                PluginBrowserWindow.ShowRestartPopup($"{plugin.Name} has been enabled.");
            }
        };

        switchToggle.Unchecked += async (_, _) =>
        {
            stateLabel.Text = "Off";
            stateLabel.Foreground = GetMutedBrush();
            if (_pluginManager != null)
            {
                await _pluginManager.DisablePluginAsync(plugin.Id);
                PluginBrowserWindow.ShowRestartPopup($"{plugin.Name} has been disabled.");
            }
        };

        controlStack.Children.Add(switchToggle);

        Grid.SetColumn(controlStack, 1);
        layout.Children.Add(controlStack);

        card.Child = layout;
        return card;
    }

    private Border CreateEmptyState(string title, string body)
    {
        var card = new Border
        {
            Background = GetSurfaceBrush(),
            CornerRadius = new CornerRadius(22),
            Padding = new Thickness(24),
            Margin = new Thickness(0, 2, 0, 0)
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = GetPrimaryBrush(),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        stack.Children.Add(new TextBlock
        {
            Text = body,
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = GetSecondaryBrush(),
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center
        });

        card.Child = stack;
        return card;
    }

    private void AddPluginTag(System.Windows.Controls.Panel host, string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return;

        var pill = new Border
        {
            Background = GetTagBrush(),
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(0, 0, 8, 8)
        };
        pill.Child = new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = GetSecondaryBrush()
        };
        host.Children.Add(pill);
    }

    private async System.Threading.Tasks.Task RefreshLibrary()
    {
        if (_libraryService == null)
            return;

        RefreshLibraryButton.IsEnabled = false;
        RefreshLibraryButton.Content = "Refreshing...";

        try
        {
            var success = await _libraryService.RefreshLibraryAsync();
            if (success)
            {
                MessageBox.Show("Plugin library refreshed successfully!", "Plugin Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var detail = _libraryService.LastError ?? "Unknown error";
                MessageBox.Show($"Failed to refresh plugin library.\n\n{detail}", "Plugin Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        finally
        {
            RefreshLibraryButton.IsEnabled = true;
            RefreshLibraryButton.Content = "Refresh Library";
        }
    }

    private void BrowsePlugins()
    {
        if (_libraryService == null)
            return;

        var browseWindow = new PluginBrowserWindow(_libraryService, _pluginManager)
        {
            Owner = Window.GetWindow(this)
        };
        browseWindow.ShowDialog();
        LoadPlugins();
    }

    private void OpenPluginsFolder()
    {
        if (_pluginManager == null)
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = _pluginManager.GetPluginsDirectory(),
            UseShellExecute = true
        });
    }

    private void OnHeaderMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
            return;

        Window.GetWindow(this)?.DragMove();
    }

    private System.Windows.Media.Brush GetPrimaryBrush() => GetBrush("TextPrimaryBrush", Color.FromRgb(255, 255, 255));
    private System.Windows.Media.Brush GetSecondaryBrush() => GetBrush("TextSecondaryBrush", Color.FromRgb(204, 204, 204));
    private System.Windows.Media.Brush GetMutedBrush() => GetBrush("TextMutedBrush", Color.FromRgb(136, 136, 136));
    private System.Windows.Media.Brush GetSurfaceBrush() => GetBrush("SegmentedBgBrush", Color.FromRgb(26, 26, 28));
    private System.Windows.Media.Brush GetTagBrush() => GetBrush("HoverOverlayBrush", Color.FromArgb(24, 255, 255, 255));
    private System.Windows.Media.Brush GetAccentBrush() => GetBrush("PluginAccentBrush", Color.FromRgb(10, 132, 255));

    private System.Windows.Media.Brush GetBrush(string resourceKey, Color fallback)
    {
        if (TryFindResource(resourceKey) is System.Windows.Media.Brush localBrush)
            return localBrush;

        if (Application.Current?.TryFindResource(resourceKey) is System.Windows.Media.Brush applicationBrush)
            return applicationBrush;

        return new SolidColorBrush(fallback);
    }

    private Style? TryGetStyle(string resourceKey)
    {
        return TryFindResource(resourceKey) as Style
            ?? Application.Current?.TryFindResource(resourceKey) as Style;
    }
}
