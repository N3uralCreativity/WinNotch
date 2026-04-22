# WinNotch

A Dynamic Island–inspired notch overlay for Windows, replicating the look and feel of [boring.notch](https://github.com/TheBoredTeam/boring.notch) on macOS.

Built with **WPF / .NET 8 / C#**.

![Windows 10/11](https://img.shields.io/badge/Windows-10%2F11-blue) ![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)

## Showcase

<img width="800" height="566" alt="01" src="https://github.com/user-attachments/assets/63ed8f8a-9488-4bfc-a3d3-ef9e098aa9c4" />


<img width="800" height="478" alt="02" src="https://github.com/user-attachments/assets/971bb745-7711-4bae-870c-fd852a2ca1de" />


<img width="800" height="42" alt="03" src="https://github.com/user-attachments/assets/495c3fa3-b2b6-4a98-a8fc-7810c3dab325" />


<img width="800" height="496" alt="04" src="https://github.com/user-attachments/assets/2ff26c30-a5e8-4c65-93cd-dfaac204d90e" />

## Features

- **Dynamic Notch** — sits at the top-center of your screen with smooth spring animations
- **Music Controls** — album art, title/artist, transport controls with live audio visualizer
- **Audio Visualizer** — real-time spectrum bars in the compact notch (auto-gain, center-grow pills)
- **Calendar** — today's events via Outlook integration
- **File Shelf** — drag & drop files onto the notch for quick staging, drag them out later
- **Volume & Brightness HUD** — scroll wheel on the notch to adjust, visual feedback
- **Battery Indicator** — shows charge level and status
- **Webcam Mirror** — optional circular webcam preview in the expanded notch
- **Fullscreen Detection** — automatically hides when apps go fullscreen
- **System Tray** — lives in your notification area, right-click to quit
- **Themes** — Dark, Light, and Auto (follows Windows) with smooth crossfade transitions
- **Settings** — inline settings panel with toggles for every feature
- **Global Hotkey** — Ctrl+Alt+N to toggle the notch open/closed
- **Plugin System** — extremely modular architecture with support for UI, service, and animation plugins
- **Plugin Library** — discover and install community plugins from the online library

## Installation

### Pre-built Release
1. Download `WinNotch.exe` from the [Releases](https://github.com/N3uralCreativity/WinNotch/releases) page
2. Run it / Install it (recommanded)
3. That's it !

### Build from Source

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), Windows 10 (1903+) or Windows 11

```bash
git clone https://github.com/N3uralCreativity/WinNotch.git
cd WinNotch
dotnet run --project src/WinNotch/WinNotch.csproj
```

To build a release executable:
```bash
dotnet publish src/WinNotch/WinNotch.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

## Usage

- **Hover** over the notch to peek, **click** to open
- **Scroll wheel** on the closed notch to adjust volume
- **Drag files** onto the notch to shelve them
- **Right-click** items in the shelf for options
- **Double-click** shelf items to open them
- **Ctrl+Alt+N** to toggle from anywhere
- **Right-click** the system tray icon to quit

## Plugin System

WinNotch features a powerful plugin architecture that allows you to extend functionality with custom features:

- **UI Plugins** — Add custom controls, widgets, or entire tabs to the notch
- **Service Plugins** — Run background services for integrations (ChatGPT, Discord, etc.)
- **Animation Plugins** — Replace or enhance notch animations with custom effects
- **Plugin Library** — Browse and install plugins from the online repository

### Creating Plugins

See the [Plugin Development Guide](PLUGIN_DEVELOPMENT.md) for detailed instructions on creating your own plugins.

### Example Plugins

Check out the `Examples/Plugins` directory for working examples:
- **BetterAnimation** — Enhanced animations with elastic easing and fade effects
- **ChatGPT Add-on** — Voice and text ChatGPT integration in the notch

### Installing Plugins

1. Browse plugins from Settings → Plugin Manager
2. Or manually place `.dll` files in `%AppData%\WinNotch\Plugins\<plugin-id>\`
3. Restart WinNotch to load new plugins

## Tech Stack

- WPF (.NET 8) with custom spring animations
- NAudio for audio capture & volume control
- Windows SMTC for media session integration
- WinRT MediaCapture for webcam
- Win32 interop for overlay behavior
- Dynamic plugin loading via reflection

## Contributing

Contributions are welcome! Whether it's bug fixes, features, or plugins:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

For plugin contributions, see the [WinNotch-Plugins](https://github.com/N3uralCreativity/WinNotch-Plugins) repository.

## License

GPL-3.0
