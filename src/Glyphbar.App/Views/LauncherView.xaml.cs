using Glyphbar.App.Services;
using Glyphbar.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Glyphbar.App.Views;

public sealed partial class LauncherView : UserControl, IFloatingContent
{
    public LauncherView(LauncherViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ThemeApplier.Track(this, viewModel.Settings);
    }

    public LauncherViewModel ViewModel { get; }

    public void OnShown()
    {
        _ = ViewModel.OpenAsync();
        Filter.Focus(FocusState.Programmatic);
    }

    public void OnHidden() => ViewModel.Filter = "";

    private void OnTileClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is LauncherTileViewModel tile) _ = ViewModel.LaunchAsync(tile);
    }

    /// <summary>Icons load only for tiles actually realised, so a long list stays cheap.</summary>
    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (!args.InRecycleQueue && args.Item is LauncherTileViewModel tile) _ = ViewModel.EnsureIconAsync(tile);
    }

    private void OnFavouritesReordered(ListViewBase sender, DragItemsCompletedEventArgs args) => _ = ViewModel.SaveFavouriteOrderAsync();

    private void OnTileRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LauncherTileViewModel tile } element) return;
        ViewModel.BuildContextMenu(tile).ShowAt(element, e.GetPosition(element));
        e.Handled = true;
    }
}
