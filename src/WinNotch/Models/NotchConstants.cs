using System.Windows;

namespace WinNotch.Models;

public static class NotchConstants
{
    // Closed dimensions (before DPI scaling)
    public const double ClosedWidth = 200;
    public const double ClosedHeight = 32;

    // Peek dimensions (slightly expanded on hover)
    public const double PeekWidth = 220;
    public const double PeekHeight = 34;

    // HUD dimensions (wider for volume/brightness slider)
    public const double HudWidth = 280;

    // Open dimensions (sized to fit music player content)
    public const double OpenWidth = 460;
    public const double OpenWidthWithCalendar = 640;
    public const double OpenHeight = 160;

    // Corner radii
    public const double ClosedTopRadius = 6;
    public const double ClosedBottomRadius = 14;
    public const double OpenTopRadius = 10;
    public const double OpenBottomRadius = 20;

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
