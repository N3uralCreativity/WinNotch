using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WinNotch.Helpers;

public static class WindowHelper
{
    // Extended window styles
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;   // Hide from Alt+Tab and taskbar
    private const int WS_EX_NOACTIVATE = 0x08000000;    // Don't steal focus on click
    private const int WS_EX_TRANSPARENT = 0x00000020;   // Click-through

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// <summary>
    /// Makes the window invisible in Alt+Tab and taskbar, and prevents it from stealing focus.
    /// Call after the window is loaded (SourceInitialized event).
    /// </summary>
    public static void MakeOverlayWindow(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW;
        exStyle |= WS_EX_NOACTIVATE;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// Enables click-through on the transparent areas of the window.
    /// </summary>
    public static void SetClickThrough(Window window, bool clickThrough)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        if (clickThrough)
            exStyle |= WS_EX_TRANSPARENT;
        else
            exStyle &= ~WS_EX_TRANSPARENT;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// Excludes the window from screen capture APIs (BitBlt, CopyFromScreen, etc.).
    /// The window remains visible on the actual display but invisible to capture.
    /// Requires Windows 10 2004+ (build 19041).
    /// </summary>
    public static void SetExcludeFromCapture(Window window, bool exclude)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        SetWindowDisplayAffinity(hwnd, exclude ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE);
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    private const uint WDA_NONE = 0x00000000;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;
}
