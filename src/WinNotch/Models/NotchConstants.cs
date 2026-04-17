using System.Windows;

namespace WinNotch.Models;

public static class NotchConstants
{
    // Closed dimensions (before DPI scaling)
    public const double ClosedWidth = 200;
    public const double ClosedHeight = 32;

    // Open dimensions
    public const double OpenWidth = 620;
    public const double OpenHeight = 340;

    // Corner radii
    public const double ClosedTopRadius = 6;
    public const double ClosedBottomRadius = 14;
    public const double OpenTopRadius = 12;
    public const double OpenBottomRadius = 24;

    // Window padding around notch for shadow space
    public const double WindowPadding = 20;

    // Animation
    public const double SpringResponse = 0.40;
    public const double SpringDamping = 0.82;

    // Hover
    public const int HoverOpenDelayMs = 200;
    public const int HoverCloseDelayMs = 150;

    // Shadow
    public static readonly System.Windows.Media.Color ShadowColor = System.Windows.Media.Color.FromArgb(180, 0, 0, 0);
    public const double ShadowBlurRadius = 12;
    public const double ShadowDepth = 2;
}
