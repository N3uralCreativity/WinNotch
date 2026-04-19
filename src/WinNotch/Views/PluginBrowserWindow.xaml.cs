using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinNotch.Plugins;

namespace WinNotch.Views;

public partial class PluginBrowserWindow : Window
{
    private readonly PluginLibraryService _libraryService;
    private readonly PluginManager? _pluginManager;

    public PluginBrowserWindow(PluginLibraryService libraryService, PluginManager? pluginManager)
    {
        InitializeComponent();

        _libraryService = libraryService;
        _pluginManager = pluginManager;

        SearchButton.Click += (s, e) => SearchPlugins();
        SearchBox.KeyDown += (s, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                SearchPlugins();
        };

        Loaded += async (s, e) =>
        {
            // Refresh library on open
            var success = await _libraryService.RefreshLibraryAsync();
            if (success)
            {
                DisplayPlugins(_libraryService.AvailablePlugins);
            }
            else
            {
                var detail = _libraryService.LastError ?? "Unknown error";
                PluginListPanel.Children.Clear();
                var errorText = new TextBlock
                {
                    Text = $"Failed to load plugin library.\n{detail}",
                    FontSize = 14,
                    Foreground = Brushes.OrangeRed,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 50, 0, 0)
                };
                PluginListPanel.Children.Add(errorText);
            }
        };
    }

    private void SearchPlugins()
    {
        var query = SearchBox.Text;
        var results = _libraryService.SearchPlugins(query);
        DisplayPlugins(results);
    }

    private void DisplayPlugins(System.Collections.Generic.IEnumerable<PluginManifest> plugins)
    {
        PluginListPanel.Children.Clear();

        foreach (var plugin in plugins)
        {
            var card = CreatePluginCard(plugin);
            PluginListPanel.Children.Add(card);
        }

        if (!plugins.Any())
        {
            var emptyText = new TextBlock
            {
                Text = "No plugins found.",
                FontSize = 14,
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 50, 0, 0)
            };
            PluginListPanel.Children.Add(emptyText);
        }
    }

    private Border CreatePluginCard(PluginManifest manifest)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(44, 44, 46)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(15),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left: Info
        var infoStack = new StackPanel();

        var nameBlock = new TextBlock
        {
            Text = manifest.Name + (manifest.IsVerified ? " ✓" : ""),
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = manifest.IsVerified ? new SolidColorBrush(Color.FromRgb(100, 210, 255)) : Brushes.White
        };
        infoStack.Children.Add(nameBlock);

        var versionAuthor = new TextBlock
        {
            Text = $"v{manifest.Version} by {manifest.Author}",
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
            Margin = new Thickness(0, 2, 0, 5)
        };
        infoStack.Children.Add(versionAuthor);

        var descBlock = new TextBlock
        {
            Text = manifest.Description,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 10, 0)
        };
        infoStack.Children.Add(descBlock);

        var categoryBlock = new TextBlock
        {
            Text = $"Category: {manifest.Category}",
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 130)),
            Margin = new Thickness(0, 5, 0, 0)
        };
        infoStack.Children.Add(categoryBlock);

        if (manifest.Permissions.Any())
        {
            var permissionsBlock = new TextBlock
            {
                Text = $"Permissions: {string.Join(", ", manifest.Permissions)}",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 200, 100)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            infoStack.Children.Add(permissionsBlock);
        }

        Grid.SetColumn(infoStack, 0);
        grid.Children.Add(infoStack);

        // Right: Install button
        var isInstalled = IsPluginInstalled(manifest.Id);
        var installButton = new Button
        {
            Content = isInstalled ? "Installed" : "Install",
            Width = 90,
            Height = 32,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = !isInstalled
        };

        installButton.Click += async (s, e) =>
        {
            await InstallPlugin(manifest, installButton);
        };

        Grid.SetColumn(installButton, 1);
        grid.Children.Add(installButton);

        card.Child = grid;
        return card;
    }

    private bool IsPluginInstalled(string pluginId)
    {
        return _pluginManager?.LoadedPlugins.Any(p => p.Id == pluginId) ?? false;
    }

    private async System.Threading.Tasks.Task InstallPlugin(PluginManifest manifest, System.Windows.Controls.Button button)
    {
        button.IsEnabled = false;
        button.Content = "Installing...";

        try
        {
            var targetDir = _pluginManager?.GetPluginsDirectory();
            if (targetDir == null)
            {
                MessageBox.Show("Plugin manager not available.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var progress = new Progress<double>(p =>
            {
                button.Content = $"Installing {p:F0}%";
            });

            var filePath = await _libraryService.DownloadPluginAsync(manifest.Id, targetDir, progress);

            if (filePath != null)
            {
                button.Content = "Restart Now";
                button.IsEnabled = true;
                button.Click += (s2, e2) => RestartApp();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to install plugin: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            button.IsEnabled = true;
            button.Content = "Install";
        }
    }

    private static void RestartApp()
    {
        var exePath = Environment.ProcessPath;
        if (exePath != null)
        {
            System.Diagnostics.Process.Start(exePath);
        }
        System.Windows.Application.Current.Shutdown();
    }
}
