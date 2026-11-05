using Glyphbar.App.Services;
using Glyphbar.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glyphbar.App.Views;

public sealed partial class PowerMenuView : UserControl, IFloatingContent
{
    public PowerMenuView(PowerMenuViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        ThemeApplier.Track(this, viewModel.Settings);
        // Cancel takes focus, so a stray Enter or Space can't confirm a shutdown.
        ViewModel.ConfirmationShown += () => CancelButton.Focus(FocusState.Programmatic);
    }

    public PowerMenuViewModel ViewModel { get; }

    public void OnShown() => ViewModel.Reset();
    public void OnHidden() => ViewModel.Reset();
}
