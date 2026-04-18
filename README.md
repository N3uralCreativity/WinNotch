# WinNotch

A Dynamic Island–inspired notch overlay for Windows, replicating the look and feel of [boring.notch](https://github.com/TheBoredTeam/boring.notch) on macOS.

Built with **WPF / .NET 8 / C#**.

![Windows 10/11](https://img.shields.io/badge/Windows-10%2F11-blue) ![.NET 8](https://img.shields.io/badge/.NET-8.0-purple)

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

## Installation

### Pre-built Release
1. Download `WinNotch.exe` from the [Releases](https://github.com/N3uralCreativity/WinNotch/releases) page
2. Run it — no installation needed (self-contained, single file)

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

## Tech Stack

- WPF (.NET 8) with custom spring animations
- NAudio for audio capture & volume control
- Windows SMTC for media session integration
- WinRT MediaCapture for webcam
- Win32 interop for overlay behavior

## License

GPL-3.0
