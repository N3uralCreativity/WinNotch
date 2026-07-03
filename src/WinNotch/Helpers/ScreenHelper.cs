using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using WinNotch.Models;

namespace WinNotch.Helpers;

public static class ScreenHelper
{
    /// <summary>
    /// Gets the full screen bounds of the primary monitor in WPF device-independent pixels.
    /// Pass the actual window to get an accurate DPI reading.
    /// </summary>
    public static Rect GetPrimaryScreenBounds(Visual visual)
    {
        return GetScreenBounds(Screen.PrimaryScreen!, visual);
    }

    /// <summary>
    /// Converts a monitor's physical-pixel bounds to WPF device-independent pixels.
    /// The app is system-DPI-aware, so one uniform scale maps the whole virtual desktop.
    /// </summary>
    public static Rect GetScreenBounds(Screen screen, Visual visual)
    {
        var dpiScale = GetDpiScale(visual);
        return new Rect(
            screen.Bounds.Left / dpiScale,
            screen.Bounds.Top / dpiScale,
            screen.Bounds.Width / dpiScale,
            screen.Bounds.Height / dpiScale
        );
    }

    /// <summary>Gets the screen containing a physical-pixel point (e.g. Cursor.Position).</summary>
    public static Screen ScreenFromPhysicalPoint(System.Drawing.Point physicalPoint)
    {
        return Screen.FromPoint(physicalPoint);
    }

    /// <summary>
    /// Finds a screen by its device name; falls back to the primary screen when the
    /// device is missing (monitor unplugged) or the name is null.
    /// </summary>
    public static Screen FindScreenByDevice(string? deviceName)
    {
        if (!string.IsNullOrEmpty(deviceName))
        {
            var match = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == deviceName);
            if (match != null)
                return match;
        }
        return Screen.PrimaryScreen!;
    }

    /// <summary>
    /// Whether a screen's left/right edge borders another monitor. Docking the notch
    /// into an interior seam of the virtual desktop looks broken, so such edges are
    /// excluded as dock targets.
    /// </summary>
    public static bool IsEdgeSharedWithAnotherScreen(Screen screen, NotchDock side)
    {
        const int tolerance = 4; // physical px

        var b = screen.Bounds;
        foreach (var other in Screen.AllScreens)
        {
            if (other.DeviceName == screen.DeviceName) continue;

            var o = other.Bounds;
            bool verticalOverlap = o.Top < b.Bottom && o.Bottom > b.Top;
            if (!verticalOverlap) continue;

            if (side == NotchDock.Left && Math.Abs(o.Right - b.Left) <= tolerance) return true;
            if (side == NotchDock.Right && Math.Abs(o.Left - b.Right) <= tolerance) return true;
        }
        return false;
    }

    /// <summary>Gets the native monitor handle the window currently sits on.</summary>
    public static IntPtr GetMonitorHandle(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        return hwnd == IntPtr.Zero ? IntPtr.Zero : MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
    }

    /// <summary>
    /// Gets the DPI scale factor from a specific visual element.
    /// </summary>
    public static double GetDpiScale(Visual visual)
    {
        var source = PresentationSource.FromVisual(visual);
        if (source?.CompositionTarget != null)
            return source.CompositionTarget.TransformToDevice.M11;

        // Fallback: use system DPI
        using var g = System.Drawing.Graphics.FromHwnd(nint.Zero);
        return g.DpiX / 96.0;
    }

    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
}
