# WinNotch Plugin Development Guide

Welcome to the WinNotch Plugin Development Guide! This comprehensive guide will teach you how to create powerful plugins for WinNotch.

## Table of Contents

1. [Introduction](#introduction)
2. [Plugin Types](#plugin-types)
3. [Getting Started](#getting-started)
4. [Creating Your First Plugin](#creating-your-first-plugin)
5. [Plugin Interfaces](#plugin-interfaces)
6. [The Plugin Context](#the-plugin-context)
7. [Advanced Topics](#advanced-topics)
8. [Publishing Your Plugin](#publishing-your-plugin)
9. [Best Practices](#best-practices)
10. [Example Plugins](#example-plugins)

---

## Introduction

WinNotch's plugin system allows you to extend the notch with custom features, animations, integrations, and UI components. Plugins are loaded dynamically from DLL files and have full access to WinNotch's services and APIs.

### What Can Plugins Do?

- **Add UI Components**: Inject custom controls into the notch (closed or expanded state)
- **Background Services**: Run background tasks, integrations, or monitoring
- **Custom Animations**: Replace or enhance notch animations with custom effects
- **System Integration**: Connect to external APIs, databases, or services
- **Theme Modifications**: Customize appearance and behavior
- **New Features**: Add entirely new capabilities to WinNotch

---

## Plugin Types

WinNotch supports several plugin types:

### 1. **Basic Plugin** (`IPlugin`)
The foundation for all plugins. Provides lifecycle hooks.

### 2. **UI Plugin** (`IUIPlugin`)
Plugins that provide visual components. Can inject UI into:
- Closed notch content
- Expanded notch content
- Custom tabs
- Settings panel
- Overlay layer

### 3. **Service Plugin** (`IServicePlugin`)
Background plugins that run services without UI (e.g., API integrations, system monitoring).

### 4. **Animation Plugin** (`IAnimationPlugin`)
Plugins that customize or replace WinNotch animations.

---

## Getting Started

### Prerequisites

- Visual Studio 2022 or VS Code
- .NET 8 SDK
- Basic C# and WPF knowledge

### Setup Your Development Environment

1. Create a new **Class Library** project:
   ```bash
   dotnet new classlib -n MyAwesomePlugin -f net8.0-windows
   ```

2. Add WinNotch as a reference:
   ```xml
   <ItemGroup>
     <Reference Include="WinNotch">
       <HintPath>path\to\WinNotch.exe</HintPath>
     </Reference>
   </ItemGroup>
   ```

3. Add required NuGet packages:
   ```xml
   <ItemGroup>
     <PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
   </ItemGroup>
   ```

4. Enable WPF support:
   ```xml
   <PropertyGroup>
     <UseWPF>true</UseWPF>
     <TargetFramework>net8.0-windows</TargetFramework>
   </PropertyGroup>
   ```

---

## Creating Your First Plugin

Let's create a simple "Hello World" plugin that displays a message in the notch.

### Step 1: Create the Plugin Class

```csharp
using System;
using System.Threading.Tasks;
using WinNotch.Plugins;

namespace MyAwesomePlugin;

[WinNotchPlugin("com.example.helloworld", "Hello World", "1.0.0", "Your Name")]
public class HelloWorldPlugin : PluginBase
{
    public override string Id => "com.example.helloworld";
    public override string Name => "Hello World";
    public override string Version => "1.0.0";
    public override string Author => "Your Name";
    public override string Description => "A simple example plugin";

    public override async Task InitializeAsync(IPluginContext context)
    {
        await base.InitializeAsync(context);
        Context?.Log("Hello World plugin initialized!");
    }

    public override Task OnEnableAsync()
    {
        Context?.Log("Hello World plugin enabled!");
        return Task.CompletedTask;
    }
}
```

### Step 2: Build and Deploy

```bash
dotnet build -c Release
```

Copy the generated `MyAwesomePlugin.dll` to:
```
%AppData%\WinNotch\Plugins\com.example.helloworld\
```

### Step 3: Restart WinNotch

Your plugin will be automatically discovered and loaded!

---

## Plugin Interfaces

### IPlugin

All plugins must implement `IPlugin`:

```csharp
public interface IPlugin : IDisposable
{
    string Id { get; }                  // Unique identifier
    string Name { get; }                // Display name
    string Version { get; }             // Semantic version
    string Author { get; }              // Author name
    string Description { get; }         // Brief description
    string MinimumWinNotchVersion { get; } // Required WinNotch version

    Task InitializeAsync(IPluginContext context);
    Task OnEnableAsync();
    Task OnDisableAsync();
    Task ShutdownAsync();
}
```

### IUIPlugin

For plugins that provide UI:

```csharp
public interface IUIPlugin : IPlugin
{
    UIElement? GetUIElement(UIPluginLocation location);
    void OnNotchStateChanged(NotchState newState);
}
```

**Example UI Plugin:**

```csharp
using System.Windows;
using System.Windows.Controls;
using WinNotch.Plugins;

[WinNotchPlugin("com.example.customwidget", "Custom Widget", "1.0.0", "You")]
public class CustomWidgetPlugin : PluginBase, IUIPlugin
{
    private TextBlock? _textBlock;

    public override string Id => "com.example.customwidget";
    public override string Name => "Custom Widget";
    public override string Version => "1.0.0";
    public override string Author => "You";
    public override string Description => "Displays custom text in the notch";

    public UIElement? GetUIElement(UIPluginLocation location)
    {
        if (location == UIPluginLocation.ClosedContent)
        {
            _textBlock = new TextBlock
            {
                Text = "Custom!",
                Foreground = Brushes.White,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            return _textBlock;
        }
        return null;
    }

    public void OnNotchStateChanged(NotchState newState)
    {
        if (_textBlock != null)
        {
            _textBlock.Text = $"State: {newState}";
        }
    }
}
```

### IServicePlugin

For background services:

```csharp
public interface IServicePlugin : IPlugin
{
    Task StartServiceAsync();
    Task StopServiceAsync();
}
```

**Example Service Plugin:**

```csharp
[WinNotchPlugin("com.example.weatherservice", "Weather Service", "1.0.0", "You")]
public class WeatherServicePlugin : PluginBase, IServicePlugin
{
    private System.Threading.Timer? _timer;

    public override string Id => "com.example.weatherservice";
    public override string Name => "Weather Service";
    public override string Version => "1.0.0";
    public override string Author => "You";
    public override string Description => "Fetches weather data every hour";

    public Task StartServiceAsync()
    {
        _timer = new System.Threading.Timer(async _ => await FetchWeather(),
            null, TimeSpan.Zero, TimeSpan.FromHours(1));
        return Task.CompletedTask;
    }

    public Task StopServiceAsync()
    {
        _timer?.Dispose();
        return Task.CompletedTask;
    }

    private async Task FetchWeather()
    {
        // Fetch weather from API
        Context?.Log("Fetching weather data...");
    }
}
```

### IAnimationPlugin

For custom animations:

```csharp
public interface IAnimationPlugin : IPlugin
{
    Storyboard? CreateExpandAnimation(FrameworkElement target,
        double fromWidth, double toWidth, double fromHeight, double toHeight);
    Storyboard? CreateCollapseAnimation(FrameworkElement target,
        double fromWidth, double toWidth, double fromHeight, double toHeight);
    Storyboard? CreatePeekAnimation(FrameworkElement target);
    IEasingFunction? GetCustomEasingFunction();
    double AnimationDurationMs { get; }
    bool ReplaceDefaultAnimations { get; }
}
```

---

## The Plugin Context

Every plugin receives an `IPluginContext` that provides access to WinNotch's services:

```csharp
public interface IPluginContext
{
    Window MainWindow { get; }                    // Main notch window
    NotchViewModel NotchViewModel { get; }        // Notch state
    MediaService MediaService { get; }            // Music controls
    ThemeService ThemeService { get; }            // Theme management
    VolumeService VolumeService { get; }          // Volume control
    BrightnessService BrightnessService { get; }  // Brightness control
    BatteryService BatteryService { get; }        // Battery info
    CalendarService CalendarService { get; }      // Calendar events
    AudioCaptureService AudioCaptureService { get; } // Audio visualizer
    ShelfService ShelfService { get; }            // File shelf
    WebcamService WebcamService { get; }          // Webcam
    FullscreenService FullscreenService { get; }  // Fullscreen detection
    AppSettings Settings { get; }                 // User settings

    string GetPluginDataPath(string pluginId);    // Get plugin data directory
    void RegisterService<T>(T service);           // Register custom service
    T? GetService<T>();                           // Get registered service
    void Log(string message, PluginLogLevel level); // Log messages
}
```

### Accessing Services

```csharp
public override async Task InitializeAsync(IPluginContext context)
{
    await base.InitializeAsync(context);

    // Access media service
    context.MediaService.SessionChanged += () =>
    {
        var media = context.MediaService.MediaInfo;
        context.Log($"Now playing: {media.Title}");
    };

    // Access theme service
    context.ThemeService.ThemeChanged += () =>
    {
        bool isDark = !context.ThemeService.IsLight;
        context.Log($"Theme changed to: {(isDark ? "Dark" : "Light")}");
    };

    // Get plugin data path
    var dataPath = context.GetPluginDataPath(Id);
    var configFile = Path.Combine(dataPath, "config.json");
}
```

---

## Advanced Topics

### Plugin Settings

Store plugin-specific settings:

```csharp
public class MyPluginSettings
{
    public bool EnableFeature { get; set; } = true;
    public int RefreshInterval { get; set; } = 60;
}

public class MyPlugin : PluginBase
{
    private MyPluginSettings? _settings;

    public override async Task InitializeAsync(IPluginContext context)
    {
        await base.InitializeAsync(context);
        _settings = LoadSettings();
    }

    private MyPluginSettings LoadSettings()
    {
        var dataPath = Context!.GetPluginDataPath(Id);
        var settingsFile = Path.Combine(dataPath, "settings.json");

        if (File.Exists(settingsFile))
        {
            var json = File.ReadAllText(settingsFile);
            return JsonSerializer.Deserialize<MyPluginSettings>(json)
                   ?? new MyPluginSettings();
        }
        return new MyPluginSettings();
    }

    private void SaveSettings()
    {
        var dataPath = Context!.GetPluginDataPath(Id);
        var settingsFile = Path.Combine(dataPath, "settings.json");
        var json = JsonSerializer.Serialize(_settings,
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsFile, json);
    }
}
```

### Custom Services

Register services that other plugins can use:

```csharp
public interface IWeatherService
{
    Task<WeatherData> GetCurrentWeatherAsync();
}

public class WeatherPlugin : PluginBase, IServicePlugin
{
    private WeatherService? _weatherService;

    public override async Task InitializeAsync(IPluginContext context)
    {
        await base.InitializeAsync(context);

        _weatherService = new WeatherService();
        context.RegisterService<IWeatherService>(_weatherService);
    }

    // Other plugins can now access:
    // var weatherService = context.GetService<IWeatherService>();
}
```

### Dependencies

Declare plugin dependencies:

```csharp
[WinNotchPlugin("com.example.myplugin", "My Plugin", "1.0.0", "You")]
[PluginDependency("com.example.weatherservice", "1.0.0")]
public class MyPlugin : PluginBase
{
    // This plugin requires WeatherService plugin
}
```

### Permissions

Declare required permissions:

```csharp
[WinNotchPlugin("com.example.myplugin", "My Plugin", "1.0.0", "You")]
[PluginPermission("network", "Access weather APIs")]
[PluginPermission("filesystem", "Store cached data")]
public class MyPlugin : PluginBase
{
}
```

---

## Publishing Your Plugin

### 1. Create a Plugin Manifest

Create a `manifest.json` file:

```json
{
  "id": "com.example.myplugin",
  "name": "My Awesome Plugin",
  "version": "1.0.0",
  "author": "Your Name",
  "description": "Does amazing things",
  "minimumWinNotchVersion": "0.2.3",
  "downloadUrl": "https://github.com/you/myplugin/releases/download/v1.0.0/MyPlugin.dll",
  "homepage": "https://github.com/you/myplugin",
  "category": "Integration",
  "permissions": ["network"],
  "dependencies": [],
  "sha256": "abc123...",
  "releaseDate": "2026-04-19T00:00:00Z",
  "isVerified": false
}
```

### 2. Host Your Plugin

Upload your DLL to a public location (GitHub Releases recommended).

### 3. Submit to Plugin Library

Create a pull request to the [WinNotch-Plugins](https://github.com/N3uralCreativity/WinNotch-Plugins) repository adding your manifest to `library.json`.

---

## Best Practices

### ✅ DO

- Use semantic versioning (1.0.0, 1.1.0, 2.0.0)
- Provide clear descriptions and documentation
- Handle errors gracefully with try-catch
- Clean up resources in `Dispose()` and `ShutdownAsync()`
- Test your plugin thoroughly before publishing
- Use the logging API for diagnostics
- Follow C# naming conventions
- Respect user settings and preferences

### ❌ DON'T

- Don't block the UI thread with long-running operations
- Don't access files outside your plugin data directory without permission
- Don't throw unhandled exceptions
- Don't leak memory or resources
- Don't make breaking changes in minor versions
- Don't collect user data without explicit consent
- Don't interfere with other plugins

---

## Example Plugins

See the `/Examples` directory for complete plugin implementations:

1. **BetterAnimation Plugin** - Custom fade and slide animations
2. **ChatGPT Add-on Plugin** - Voice and text ChatGPT integration
3. **Weather Widget Plugin** - Displays current weather in the notch
4. **Spotify Lyrics Plugin** - Shows synchronized lyrics for Spotify

---

## API Reference

Full API documentation available at: [API Docs](https://github.com/N3uralCreativity/WinNotch/wiki/Plugin-API)

## Support

- GitHub Issues: https://github.com/N3uralCreativity/WinNotch/issues
- Discussions: https://github.com/N3uralCreativity/WinNotch/discussions

---

Happy plugin development! 🎉
