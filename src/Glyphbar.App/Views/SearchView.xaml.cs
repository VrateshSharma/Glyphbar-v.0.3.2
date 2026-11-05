using Glyphbar.App.Services;
using Glyphbar.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Glyphbar.App.Views;

public sealed partial class SearchView : UserControl, IFloatingContent
{
    public SearchView(SearchViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ThemeApplier.Track(this, viewModel.Settings);
    }

    public SearchViewModel ViewModel { get; }

    public void OnShown()
    {
        ViewModel.Open();
        Input.Focus(FocusState.Programmatic);
        Input.SelectAll();
    }

    public void OnHidden() => ViewModel.Close();

    /// <summary>Focus stays in the box; arrows drive the list from here.</summary>
    private void OnInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Down:
                ViewModel.MoveSelection(1);
                Reveal();
                e.Handled = true;
                break;
            case VirtualKey.Up:
                ViewModel.MoveSelection(-1);
                Reveal();
                e.Handled = true;
                break;
            case VirtualKey.Enter:
                _ = ViewModel.OpenSelectedAsync();
                e.Handled = true;
                break;
        }
    }

    private void Reveal()
    {
        if (Results.SelectedItem is { } item) Results.ScrollIntoView(item);
    }

    private void OnResultClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchResultViewModel item) _ = ViewModel.OpenAsync(item);
    }
}
