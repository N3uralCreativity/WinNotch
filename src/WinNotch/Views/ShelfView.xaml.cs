using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WinNotch.Models;
using WinNotch.Services;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using DataObject = System.Windows.DataObject;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;

namespace WinNotch.Views;

public partial class ShelfView : UserControl
{
    private ShelfService? _service;

    public ShelfView()
    {
        InitializeComponent();
    }

    public void Bind(ShelfService service)
    {
        _service = service;
        ShelfItemsControl.ItemsSource = service.Items;
        service.ShelfChanged += OnShelfChanged;
        UpdateVisibility();
    }

    private void OnShelfChanged()
    {
        Dispatcher.Invoke(UpdateVisibility);
    }

    private void UpdateVisibility()
    {
        if (_service == null) return;

        bool hasItems = _service.HasItems;
        ShelfItemsControl.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        ClearAllButton.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        ItemCountText.Text = hasItems ? $"{_service.Items.Count} file{(_service.Items.Count > 1 ? "s" : "")}" : "";
        EmptyState.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
    }

    public void ShowDropZone()
    {
        DropZoneOverlay.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;
    }

    public void HideDropZone()
    {
        DropZoneOverlay.Visibility = Visibility.Collapsed;
        UpdateVisibility();
    }

    private void OnClearAllClick(object sender, RoutedEventArgs e)
    {
        _service?.Clear();
    }

    private void OnItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ShelfItem item)
        {
            try
            {
                Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true });
            }
            catch { }
        }
    }

    private void OnItemRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ShelfItem item)
        {
            var menu = new ContextMenu();

            var openItem = new MenuItem { Header = "Open" };
            openItem.Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(item.FilePath) { UseShellExecute = true }); }
                catch { }
            };
            menu.Items.Add(openItem);

            var openFolderItem = new MenuItem { Header = "Show in Explorer" };
            openFolderItem.Click += (_, _) =>
            {
                try
                {
                    if (item.IsFolder)
                        Process.Start(new ProcessStartInfo("explorer.exe", item.FilePath));
                    else
                        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{item.FilePath}\""));
                }
                catch { }
            };
            menu.Items.Add(openFolderItem);

            menu.Items.Add(new Separator());

            var removeItem = new MenuItem { Header = "Remove" };
            removeItem.Click += (_, _) => _service?.Remove(item);
            menu.Items.Add(removeItem);

            menu.IsOpen = true;
            e.Handled = true;
        }
    }

    /// <summary>
    /// Initiate drag-out so users can drag shelf items to other applications.
    /// </summary>
    private void OnItemMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed &&
            sender is FrameworkElement fe &&
            fe.DataContext is ShelfItem item)
        {
            var data = new DataObject(DataFormats.FileDrop, new[] { item.FilePath });
            DragDrop.DoDragDrop(fe, data, DragDropEffects.Copy | DragDropEffects.Move);
        }
    }
}
