using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;

namespace WinNotch.Helpers;

public static class ScreenHelper
{
    /// <summary>
    /// Gets the full screen bounds of the primary monitor in WPF device-independent pixels.
    /// Pass the actual window to get an accurate DPI reading.
    /// </summary>
    public static Rect GetPrimaryScreenBounds(Visual visual)
    {
        var screen = Screen.PrimaryScreen!;
        var dpiScale = GetDpiScale(visual);
        return new Rect(
            screen.Bounds.Left / dpiScale,
            screen.Bounds.Top / dpiScale,
            screen.Bounds.Width / dpiScale,
            screen.Bounds.Height / dpiScale
        );
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
}
