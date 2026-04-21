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
    private bool _hasTriedLibraryRefresh;

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
        Loaded += async (_, _) => await EnsureLibraryLoadedAsync();

        LoadPlugins();
    }

    private void LoadPlugins()
    {
        PluginListPanel.Children.Clear();

        if (_pluginManager == null)
            return;

        var availableUpdateCount = _pluginManager.LoadedPlugins.Count(HasAvailableUpdate) + _pluginManager.LoadedPlugins.Count(HasQueuedUpdate);
        InstalledCountText.Text = availableUpdateCount > 0
            ? $"{_pluginManager.LoadedPlugins.Count} plugin{(_pluginManager.LoadedPlugins.Count == 1 ? string.Empty : "s")} installed - {availableUpdateCount} update{(availableUpdateCount == 1 ? string.Empty : "s")} ready"
            : $"{_pluginManager.LoadedPlugins.Count} plugin{(_pluginManager.LoadedPlugins.Count == 1 ? string.Empty : "s")} installed";

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
        var queuedUpdate = GetQueuedManifest(plugin);
        var availableUpdate = queuedUpdate == null ? GetAvailableUpdateManifest(plugin) : null;

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
            Text = queuedUpdate != null
                ? $"v{plugin.Version} -> v{queuedUpdate.Version}"
                : availableUpdate != null
                    ? $"v{plugin.Version} -> v{availableUpdate.Version}"
                    : $"v{plugin.Version}",
            FontSize = 11,
            Foreground = queuedUpdate != null || availableUpdate != null ? GetAccentBrush() : GetSecondaryBrush(),
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

        if (queuedUpdate != null)
        {
            controlStack.Children.Add(new TextBlock
            {
                Text = "Restart needed",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = GetAccentBrush()
            });

            var restartButton = new Button
            {
                Content = "Restart",
                MinWidth = 86,
                Margin = new Thickness(0, 0, 0, 12),
                Style = TryGetStyle("PluginSecondaryButton")
            };
            restartButton.Click += (_, _) => PluginBrowserWindow.RestartApp();
            controlStack.Children.Add(restartButton);
        }
        else if (availableUpdate != null)
        {
            controlStack.Children.Add(new TextBlock
            {
                Text = "Update available",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8),
                Foreground = GetAccentBrush()
            });

            var updateButton = new Button
            {
                Content = "Update",
                MinWidth = 86,
                Margin = new Thickness(0, 0, 0, 12),
                Style = TryGetStyle("PluginSecondaryButton")
            };
            updateButton.Click += async (_, _) => await UpdatePluginAsync(plugin, availableUpdate, updateButton);
            controlStack.Children.Add(updateButton);
        }

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

    private async System.Threading.Tasks.Task<bool> EnsureLibraryLoadedAsync(bool forceRefresh = false, bool showErrors = false)
    {
        if (_libraryService == null)
            return false;

        if (!forceRefresh && (_hasTriedLibraryRefresh || _libraryService.AvailablePlugins.Count > 0))
        {
            LoadPlugins();
            return _libraryService.AvailablePlugins.Count > 0;
        }

        _hasTriedLibraryRefresh = true;

        var originalContent = RefreshLibraryButton.Content;
        RefreshLibraryButton.IsEnabled = false;
        RefreshLibraryButton.Content = "Refreshing...";

        try
        {
            var success = await _libraryService.RefreshLibraryAsync();
            if (success)
            {
                LoadPlugins();
            }
            else if (showErrors)
            {
                var detail = _libraryService.LastError ?? "Unknown error";
                MessageBox.Show($"Failed to refresh plugin library.\n\n{detail}", "Plugin Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            return success;
        }
        finally
        {
            RefreshLibraryButton.IsEnabled = true;
            RefreshLibraryButton.Content = originalContent;
        }
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
        var success = await EnsureLibraryLoadedAsync(forceRefresh: true, showErrors: true);
        if (success)
        {
            MessageBox.Show("Plugin library refreshed successfully!", "Plugin Manager", MessageBoxButton.OK, MessageBoxImage.Information);
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

    private async System.Threading.Tasks.Task UpdatePluginAsync(IPlugin plugin, PluginManifest manifest, Button updateButton)
    {
        if (_libraryService == null || _pluginManager == null)
            return;

        updateButton.IsEnabled = false;
        var originalContent = updateButton.Content;

        try
        {
            var targetDir = _pluginManager.GetPluginsDirectory();
            var progress = new Progress<double>(value =>
            {
                updateButton.Content = $"{Math.Round(value):0}%";
            });

            var filePath = await _libraryService.DownloadPluginAsync(plugin.Id, targetDir, progress);
            if (filePath == null)
            {
                throw new InvalidOperationException($"Failed to update {plugin.Name}.");
            }

            updateButton.Content = "Queued";
            LoadPlugins();
            PluginBrowserWindow.ShowRestartPopup($"{plugin.Name} will update to v{manifest.Version}.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to update plugin: {ex.Message}", "Plugin Manager", MessageBoxButton.OK, MessageBoxImage.Error);
            updateButton.IsEnabled = true;
            updateButton.Content = originalContent;
        }
    }

    private bool HasAvailableUpdate(IPlugin plugin)
    {
        return GetAvailableUpdateManifest(plugin) != null;
    }

    private bool HasQueuedUpdate(IPlugin plugin)
    {
        return GetQueuedManifest(plugin) != null;
    }

    private PluginManifest? GetQueuedManifest(IPlugin plugin)
    {
        if (_pluginManager == null || _libraryService == null)
            return null;

        var installedManifest = _libraryService.ReadInstalledManifest(plugin.Id, _pluginManager.GetPluginsDirectory());
        if (installedManifest == null)
            return null;

        return CompareVersions(installedManifest.Version, plugin.Version) > 0
            ? installedManifest
            : null;
    }

    private PluginManifest? GetAvailableManifest(string pluginId)
    {
        return _libraryService?.GetAvailablePlugin(pluginId);
    }

    private PluginManifest? GetAvailableUpdateManifest(IPlugin plugin)
    {
        var availableManifest = GetAvailableManifest(plugin.Id);
        if (availableManifest == null)
            return null;

        if (CompareVersions(availableManifest.Version, plugin.Version) <= 0)
            return null;

        var queuedManifest = GetQueuedManifest(plugin);
        if (queuedManifest != null && CompareVersions(queuedManifest.Version, availableManifest.Version) >= 0)
            return null;

        return availableManifest;
    }

    private static int CompareVersions(string left, string right)
    {
        if (Version.TryParse(left, out var leftVersion) && Version.TryParse(right, out var rightVersion))
            return leftVersion.CompareTo(rightVersion);

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
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
