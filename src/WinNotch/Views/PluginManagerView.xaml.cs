using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
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

        RefreshLibraryButton.Click += async (s, e) => await RefreshLibrary();
        BrowsePluginsButton.Click += (s, e) => BrowsePlugins();
        OpenPluginsFolderButton.Click += (s, e) => OpenPluginsFolder();

        LoadPlugins();
    }

    private void LoadPlugins()
    {
        PluginListPanel.Children.Clear();

        if (_pluginManager == null) return;

        foreach (var plugin in _pluginManager.LoadedPlugins)
        {
            var pluginCard = CreatePluginCard(plugin);
            PluginListPanel.Children.Add(pluginCard);
        }

        if (_pluginManager.LoadedPlugins.Count == 0)
        {
            var emptyText = new TextBlock
            {
                Text = "No plugins installed. Click 'Browse Plugins' to discover available plugins.",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 170)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            PluginListPanel.Children.Add(emptyText);
        }
    }

    private Border CreatePluginCard(IPlugin plugin)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(40, 40, 42)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(15),
            Margin = new Thickness(0, 0, 0, 10)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left side: Plugin info
        var infoStack = new StackPanel();

        var nameBlock = new TextBlock
        {
            Text = plugin.Name,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        };
        infoStack.Children.Add(nameBlock);

        var versionAuthor = new TextBlock
        {
            Text = $"v{plugin.Version} by {plugin.Author}",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
            Margin = new Thickness(0, 2, 0, 5)
        };
        infoStack.Children.Add(versionAuthor);

        var descBlock = new TextBlock
        {
            Text = plugin.Description,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 10, 0)
        };
        infoStack.Children.Add(descBlock);

        var typeIndicator = new TextBlock
        {
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 150, 255)),
            Margin = new Thickness(0, 5, 0, 0)
        };

        if (plugin is IUIPlugin) typeIndicator.Text += "UI • ";
        if (plugin is IServicePlugin) typeIndicator.Text += "Service • ";
        if (plugin is IAnimationPlugin) typeIndicator.Text += "Animation • ";
        typeIndicator.Text = typeIndicator.Text.TrimEnd(' ', '•');

        infoStack.Children.Add(typeIndicator);

        Grid.SetColumn(infoStack, 0);
        grid.Children.Add(infoStack);

        // Right side: Toggle switch
        var toggleButton = new CheckBox
        {
            IsChecked = _pluginManager?.IsPluginEnabled(plugin.Id) ?? false,
            VerticalAlignment = VerticalAlignment.Center,
            Content = "Enabled"
        };

        toggleButton.Checked += async (s, e) =>
        {
            if (_pluginManager != null)
            {
                await _pluginManager.EnablePluginAsync(plugin.Id);
            }
        };

        toggleButton.Unchecked += async (s, e) =>
        {
            if (_pluginManager != null)
            {
                await _pluginManager.DisablePluginAsync(plugin.Id);
            }
        };

        Grid.SetColumn(toggleButton, 1);
        grid.Children.Add(toggleButton);

        card.Child = grid;
        return card;
    }

    private async System.Threading.Tasks.Task RefreshLibrary()
    {
        if (_libraryService == null) return;

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
                MessageBox.Show("Failed to refresh plugin library. Please check your internet connection.", "Plugin Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        if (_libraryService == null) return;

        var browseWindow = new PluginBrowserWindow(_libraryService, _pluginManager);
        browseWindow.Owner = Window.GetWindow(this);
        browseWindow.ShowDialog();

        // Reload plugins after browsing
        LoadPlugins();
    }

    private void OpenPluginsFolder()
    {
        if (_pluginManager == null) return;

        var path = _pluginManager.GetPluginsDirectory();
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
