using System;
using System.Windows;
using System.Windows.Media;

namespace WinNotch.Views.Components;

/// <summary>
/// Renders audio spectrum as thin pill-shaped bars that grow from center.
/// Evenly distributes input bands, uses auto-gain normalization so bars
/// never max out but always show clear movement.
/// </summary>
public class AudioVisualizer : FrameworkElement
{
    public static readonly DependencyProperty BarColorProperty =
        DependencyProperty.Register(nameof(BarColor), typeof(Color), typeof(AudioVisualizer),
            new FrameworkPropertyMetadata(Colors.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarCountProperty =
        DependencyProperty.Register(nameof(BarCount), typeof(int), typeof(AudioVisualizer),
            new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarWidthProperty =
        DependencyProperty.Register(nameof(BarWidth), typeof(double), typeof(AudioVisualizer),
            new FrameworkPropertyMetadata(3.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BarGapProperty =
        DependencyProperty.Register(nameof(BarGap), typeof(double), typeof(AudioVisualizer),
            new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

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

    public double BarWidth
    {
        get => (double)GetValue(BarWidthProperty);
        set => SetValue(BarWidthProperty, value);
    }

    public double BarGap
    {
        get => (double)GetValue(BarGapProperty);
        set => SetValue(BarGapProperty, value);
    }

    // Display-ready levels (one per visual bar)
    private float[] _display = new float[4];
    // Auto-gain: tracks recent peak per bar for normalization
    private float[] _peak = new float[4];

    public void UpdateSpectrum(float[] data)
    {
        int barCount = BarCount;
        if (_display.Length != barCount)
        {
            _display = new float[barCount];
            _peak = new float[barCount];
            for (int i = 0; i < barCount; i++) _peak[i] = 0.3f;
        }

        int bandsPerBar = Math.Max(1, data.Length / barCount);

        for (int i = 0; i < barCount; i++)
        {
            // Average a spread of input bands for this visual bar
            int start = i * bandsPerBar;
            int end = Math.Min(start + bandsPerBar, data.Length);
            float sum = 0;
            int count = 0;
            for (int j = start; j < end; j++)
            {
                sum += data[j];
                count++;
            }
            float avg = count > 0 ? sum / count : 0;

            // Auto-gain: slowly adapt peak so bars never stay maxed out
            if (avg > _peak[i])
                _peak[i] = _peak[i] * 0.5f + avg * 0.5f;   // rise toward new peak
            else
                _peak[i] = _peak[i] * 0.998f;               // slowly decay peak
            _peak[i] = Math.Max(_peak[i], 0.15f);            // floor so quiet audio still shows

            // Normalize against recent peak, cap at 0.75 so we never hit max
            float normalized = Math.Min(0.75f, avg / _peak[i] * 0.75f);

            // Smooth: fast rise, slow fall
            if (normalized > _display[i])
                _display[i] = _display[i] * 0.2f + normalized * 0.8f;
            else
                _display[i] = _display[i] * 0.85f + normalized * 0.15f;
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double h = ActualHeight;
        int count = BarCount;
        double bw = BarWidth;
        double gap = BarGap;

        if (count <= 0 || h <= 0) return;

        double pillRadius = bw / 2.0;
        double minH = bw;           // min = a circle
        double maxH = h;

        double totalW = count * bw + (count - 1) * gap;
        double startX = (ActualWidth - totalW) / 2.0;
        double centerY = h / 2.0;

        var brush = new SolidColorBrush(BarColor);
        brush.Freeze();

        for (int i = 0; i < count; i++)
        {
            float level = (i < _display.Length) ? _display[i] : 0;
            double barH = minH + level * (maxH - minH);

            double x = startX + i * (bw + gap);
            double y = centerY - barH / 2.0;

            dc.DrawRoundedRectangle(brush, null, new Rect(x, y, bw, barH), pillRadius, pillRadius);
        }
    }
}
