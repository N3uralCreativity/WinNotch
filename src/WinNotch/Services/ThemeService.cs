using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using WinNotch.Models;

namespace WinNotch.Services;

public class ThemeService
{
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegistryValueName = "AppsUseLightTheme";
    private const double FadeDurationMs = 150;

    private AppTheme _currentSetting = AppTheme.Dark;
    private bool _isLightResolved;
    private bool _isTransitioning;

    public event Action? ThemeChanged;

    public bool IsLight => _isLightResolved;

    public void Apply(AppTheme theme)
    {
        _currentSetting = theme;
        _isLightResolved = theme switch
        {
            AppTheme.Light => true,
            AppTheme.Dark => false,
            _ => DetectWindowsThemeIsLight()
        };

        SwapThemeDictionary();
        ThemeChanged?.Invoke();
    }

    public async Task ApplyWithTransition(AppTheme theme, UIElement target)
    {
        if (_isTransitioning) return;
        _isTransitioning = true;

        _currentSetting = theme;
        _isLightResolved = theme switch
        {
            AppTheme.Light => true,
            AppTheme.Dark => false,
            _ => DetectWindowsThemeIsLight()
        };

        // Fade out
        var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(FadeDurationMs))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        var tcs = new TaskCompletionSource();
        fadeOut.Completed += (_, _) => tcs.SetResult();
        target.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        await tcs.Task;

        // Swap resources
        SwapThemeDictionary();
        ThemeChanged?.Invoke();

        // Fade in
        var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(FadeDurationMs))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var tcs2 = new TaskCompletionSource();
        fadeIn.Completed += (_, _) => tcs2.SetResult();
        target.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        await tcs2.Task;

        _isTransitioning = false;
    }

    private void SwapThemeDictionary()
    {
        var dictUri = _isLightResolved
            ? new Uri("Views/Components/LightTheme.xaml", UriKind.Relative)
            : new Uri("Views/Components/DarkTheme.xaml", UriKind.Relative);

        var app = Application.Current;
        var mergedDicts = app.Resources.MergedDictionaries;

        for (int i = mergedDicts.Count - 1; i >= 0; i--)
        {
            if (mergedDicts[i].Source != null &&
                (mergedDicts[i].Source.OriginalString.Contains("DarkTheme") ||
                 mergedDicts[i].Source.OriginalString.Contains("LightTheme")))
            {
                mergedDicts.RemoveAt(i);
            }
        }

        mergedDicts.Insert(0, new ResourceDictionary { Source = dictUri });
    }

    public void StartWatchingSystemTheme()
    {
        SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;
    }

    public void StopWatchingSystemTheme()
    {
        SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged;
    }

    private void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General && _currentSetting == AppTheme.Auto)
        {
            Application.Current.Dispatcher.Invoke(() => Apply(AppTheme.Auto));
        }
    }

    private static bool DetectWindowsThemeIsLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            var value = key?.GetValue(RegistryValueName);
            return value is int i && i == 1;
        }
        catch
        {
            return false; // Default to dark
        }
    }
}
