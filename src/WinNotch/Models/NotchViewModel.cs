using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;

namespace WinNotch.Models;

public partial class NotchViewModel : ObservableObject
{
    [ObservableProperty]
    private NotchState _notchState = NotchState.Closed;

    [ObservableProperty]
    private double _notchWidth = NotchConstants.ClosedWidth;

    [ObservableProperty]
    private double _notchHeight = NotchConstants.ClosedHeight;

    [ObservableProperty]
    private double _topCornerRadius = NotchConstants.ClosedTopRadius;

    [ObservableProperty]
    private double _bottomCornerRadius = NotchConstants.ClosedBottomRadius;

    [ObservableProperty]
    private double _shadowOpacity = 0.0;

    [ObservableProperty]
    private double _contentOpacity = 0.0;

    [ObservableProperty]
    private bool _isHovering;

    // Window position (top-left of the transparent host window)
    [ObservableProperty]
    private double _windowLeft;

    [ObservableProperty]
    private double _windowTop;

    // Total window size (notch + padding for shadow)
    public double WindowWidth => NotchWidth + NotchConstants.WindowPadding * 2;
    public double WindowHeight => NotchHeight + NotchConstants.WindowPadding + 8;

    public NotchViewModel()
    {
        UpdateWindowPosition();
    }

    partial void OnNotchWidthChanged(double value)
    {
        OnPropertyChanged(nameof(WindowWidth));
        UpdateWindowPosition();
    }

    partial void OnNotchHeightChanged(double value)
    {
        OnPropertyChanged(nameof(WindowHeight));
    }

    [RelayCommand]
    public void Open()
    {
        NotchState = NotchState.Open;
    }

    [RelayCommand]
    public void Close()
    {
        NotchState = NotchState.Closed;
    }

    [RelayCommand]
    public void Toggle()
    {
        if (NotchState == NotchState.Closed)
            Open();
        else
            Close();
    }

    public void UpdateWindowPosition()
    {
        var screen = Helpers.ScreenHelper.GetPrimaryScreenWorkArea();
        WindowLeft = screen.Left + (screen.Width - WindowWidth) / 2;
        WindowTop = screen.Top - 2; // Slightly above to cover any gap
    }
}
