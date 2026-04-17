using System;
using System.Windows;
using Microsoft.Win32;
using WinNotch.Models;

namespace WinNotch.Services;

public class ThemeService
{
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegistryValueName = "AppsUseLightTheme";

    private AppTheme _currentSetting = AppTheme.Dark;
    private bool _isLightResolved;

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

        var dictUri = _isLightResolved
            ? new Uri("Views/Components/LightTheme.xaml", UriKind.Relative)
            : new Uri("Views/Components/DarkTheme.xaml", UriKind.Relative);

        var app = Application.Current;
        var mergedDicts = app.Resources.MergedDictionaries;

        // Remove previous theme dictionary (always the first one we added)
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
        ThemeChanged?.Invoke();
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
