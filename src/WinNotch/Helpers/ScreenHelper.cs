using System.Windows;
using System.Windows.Forms;

namespace WinNotch.Helpers;

public static class ScreenHelper
{
    /// <summary>
    /// Gets the full screen bounds of the primary monitor in device-independent pixels.
    /// </summary>
    public static Rect GetPrimaryScreenWorkArea()
    {
        var screen = Screen.PrimaryScreen!;
        // Screen.Bounds is in physical pixels; convert to WPF DIPs
        var dpiScale = GetDpiScale();
        return new Rect(
            screen.Bounds.Left / dpiScale,
            screen.Bounds.Top / dpiScale,
            screen.Bounds.Width / dpiScale,
            screen.Bounds.Height / dpiScale
        );
    }

    /// <summary>
    /// Gets the DPI scale factor (e.g., 1.0 for 100%, 1.25 for 125%, 1.5 for 150%).
    /// </summary>
    public static double GetDpiScale()
    {
        var source = PresentationSource.FromVisual(System.Windows.Application.Current.MainWindow);
        if (source?.CompositionTarget != null)
            return source.CompositionTarget.TransformToDevice.M11;

        // Fallback: use system DPI from Forms
        using var g = System.Drawing.Graphics.FromHwnd(nint.Zero);
        return g.DpiX / 96.0;
    }
}
