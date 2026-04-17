using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WinNotch.Views.Components;

/// <summary>
/// Renders audio spectrum bars. Set SpectrumData (float[]) and call InvalidateVisual().
/// </summary>
public class AudioVisualizer : FrameworkElement
{
    public static readonly DependencyProperty BarColorProperty =
        DependencyProperty.Register(nameof(BarColor), typeof(Color), typeof(AudioVisualizer),
            new FrameworkPropertyMetadata(Colors.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarCountProperty =
        DependencyProperty.Register(nameof(BarCount), typeof(int), typeof(AudioVisualizer),
            new FrameworkPropertyMetadata(12, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarGapProperty =
        DependencyProperty.Register(nameof(BarGap), typeof(double), typeof(AudioVisualizer),
            new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarRadiusProperty =
        DependencyProperty.Register(nameof(BarRadius), typeof(double), typeof(AudioVisualizer),
            new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsRender));

    public Color BarColor
    {
        get => (Color)GetValue(BarColorProperty);
        set => SetValue(BarColorProperty, value);
    }

    public int BarCount
    {
        get => (int)GetValue(BarCountProperty);
        set => SetValue(BarCountProperty, value);
    }

    public double BarGap
    {
        get => (double)GetValue(BarGapProperty);
        set => SetValue(BarGapProperty, value);
    }

    public double BarRadius
    {
        get => (double)GetValue(BarRadiusProperty);
        set => SetValue(BarRadiusProperty, value);
    }

    // Set this from code-behind, then call InvalidateVisual()
    public float[]? SpectrumData { get; set; }

    // Smoothed values for rendering
    private float[]? _smoothed;

    public void UpdateSpectrum(float[] data)
    {
        if (_smoothed == null || _smoothed.Length != data.Length)
            _smoothed = new float[data.Length];

        for (int i = 0; i < data.Length; i++)
        {
            // Smooth: fast rise, slow fall
            if (data[i] > _smoothed[i])
                _smoothed[i] = _smoothed[i] * 0.2f + data[i] * 0.8f;
            else
                _smoothed[i] = _smoothed[i] * 0.85f + data[i] * 0.15f;
        }

        SpectrumData = _smoothed;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        int count = BarCount;
        double gap = BarGap;
        double radius = BarRadius;

        if (count <= 0 || w <= 0 || h <= 0) return;

        double barWidth = (w - gap * (count - 1)) / count;
        if (barWidth < 1) barWidth = 1;

        var brush = new SolidColorBrush(BarColor);
        brush.Freeze();

        double minBarHeight = 2;

        for (int i = 0; i < count; i++)
        {
            float level = (SpectrumData != null && i < SpectrumData.Length)
                ? SpectrumData[i] : 0;

            double barHeight = Math.Max(minBarHeight, level * h);
            double x = i * (barWidth + gap);
            double y = h - barHeight;

            var rect = new Rect(x, y, barWidth, barHeight);
            dc.DrawRoundedRectangle(brush, null, rect, radius, radius);
        }
    }
}
