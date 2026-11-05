using Glyphbar.App.Services;
using Glyphbar.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Glyphbar.App.Views;

public sealed partial class TaskbarView : UserControl
{
    public TaskbarView(TaskbarViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public TaskbarViewModel ViewModel { get; }

    private void OnDockItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not DockItemViewModel item) return;
        if (Dock.ContainerFromItem(item) is FrameworkElement container) Motion.Pulse(container);
        _ = ViewModel.ActivateAsync(item);
    }

    private void OnDockReordered(ListViewBase sender, DragItemsCompletedEventArgs args) => _ = ViewModel.SaveOrderAsync();

    private void OnDockItemPointerEntered(object sender, PointerRoutedEventArgs e) => Motion.Scale((FrameworkElement)sender, 1.1f);

    private void OnDockItemPointerExited(object sender, PointerRoutedEventArgs e) => Motion.Scale((FrameworkElement)sender, 1f);

    private void OnDockItemRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: DockItemViewModel item } element) return;
        var menu = ViewModel.BuildContextMenu(item, this);
        ViewModel.TrackMenu(menu);
        menu.ShowAt(element, e.GetPosition(element));
        e.Handled = true;
    }
}
