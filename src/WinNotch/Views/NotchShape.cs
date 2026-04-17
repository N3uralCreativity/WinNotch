using System.Windows;
using System.Windows.Media;

namespace WinNotch.Views;

/// <summary>
/// Custom WPF shape that draws the notch outline.
/// Matches boring.notch's NotchShape.swift — a rectangle with:
///   - Top edges: small inward quad-curves (the "ears")
///   - Bottom edges: larger outward quad-curves (rounded bottom)
/// </summary>
public class NotchShape : System.Windows.Shapes.Shape
{
    public static readonly DependencyProperty TopCornerRadiusProperty =
        DependencyProperty.Register(nameof(TopCornerRadius), typeof(double), typeof(NotchShape),
            new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BottomCornerRadiusProperty =
        DependencyProperty.Register(nameof(BottomCornerRadius), typeof(double), typeof(NotchShape),
            new FrameworkPropertyMetadata(14.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double TopCornerRadius
    {
        get => (double)GetValue(TopCornerRadiusProperty);
        set => SetValue(TopCornerRadiusProperty, value);
    }

    public double BottomCornerRadius
    {
        get => (double)GetValue(BottomCornerRadiusProperty);
        set => SetValue(BottomCornerRadiusProperty, value);
    }

    protected override Geometry DefiningGeometry
    {
        get
        {
            var rect = new Rect(0, 0, ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height);
            if (rect.Width <= 0 || rect.Height <= 0)
                return Geometry.Empty;

            return CreateNotchGeometry(rect, TopCornerRadius, BottomCornerRadius);
        }
    }

    /// <summary>
    /// Creates the notch path geometry matching boring.notch's shape exactly.
    /// 
    /// The shape is:
    ///   - Top-left: straight to (minX, minY), then quad-curve inward to (minX + topR, minY + topR)
    ///   - Left side: straight down to (minX + topR, maxY - bottomR)
    ///   - Bottom-left: quad-curve outward to (minX + topR + bottomR, maxY)
    ///   - Bottom: straight across to (maxX - topR - bottomR, maxY)
    ///   - Bottom-right: quad-curve outward to (maxX - topR, maxY - bottomR)
    ///   - Right side: straight up to (maxX - topR, minY + topR)
    ///   - Top-right: quad-curve inward to (maxX, minY)
    ///   - Top: close back to start
    /// </summary>
    public static Geometry CreateNotchGeometry(Rect rect, double topR, double bottomR)
    {
        double minX = rect.Left;
        double minY = rect.Top;
        double maxX = rect.Right;
        double maxY = rect.Bottom;

        var fig = new PathFigure
        {
            StartPoint = new Point(minX, minY),
            IsClosed = true,
            IsFilled = true
        };

        // Top-left ear: quad curve inward
        fig.Segments.Add(new QuadraticBezierSegment(
            new Point(minX + topR, minY),               // control
            new Point(minX + topR, minY + topR),         // end
            true));

        // Left side down
        fig.Segments.Add(new LineSegment(
            new Point(minX + topR, maxY - bottomR), true));

        // Bottom-left curve outward
        fig.Segments.Add(new QuadraticBezierSegment(
            new Point(minX + topR, maxY),                // control
            new Point(minX + topR + bottomR, maxY),      // end
            true));

        // Bottom straight across
        fig.Segments.Add(new LineSegment(
            new Point(maxX - topR - bottomR, maxY), true));

        // Bottom-right curve outward
        fig.Segments.Add(new QuadraticBezierSegment(
            new Point(maxX - topR, maxY),                // control
            new Point(maxX - topR, maxY - bottomR),      // end
            true));

        // Right side up
        fig.Segments.Add(new LineSegment(
            new Point(maxX - topR, minY + topR), true));

        // Top-right ear: quad curve inward
        fig.Segments.Add(new QuadraticBezierSegment(
            new Point(maxX - topR, minY),                // control
            new Point(maxX, minY),                       // end
            true));

        // Close (top across)
        fig.Segments.Add(new LineSegment(
            new Point(minX, minY), true));

        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        geo.Freeze();
        return geo;
    }
}
