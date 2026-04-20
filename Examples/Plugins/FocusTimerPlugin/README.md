# Focus Timer

`Focus Timer` adds a clean Pomodoro-style timer to the expanded WinNotch view.

## What it does

- Runs focus, short-break, and long-break sessions
- Tracks how many focus sessions you completed today
- Rotates into a long break after a configurable number of focus blocks
- Supports light and dark themes
- Includes a compact vertical-friendly layout for side docking

## Setup

The plugin creates:

`%AppData%\WinNotch\Plugins\com.winnotch.focustimer\settings.json`

Default file:

```json
{
  "focusMinutes": 25,
  "shortBreakMinutes": 5,
  "longBreakMinutes": 15,
  "longBreakEvery": 4
}
```
