using Glyphbar.App.ViewModels;
using Glyphbar.Core.Models;
using Microsoft.UI.Xaml.Controls;

namespace Glyphbar.App.Components.Widgets;

public sealed partial class ClockWidget : UserControl, IWidgetView
{
    public ClockWidget(ClockWidgetViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public ClockWidgetViewModel ViewModel { get; }
    public void Activate() => ViewModel.Activate();
    public void Deactivate() => ViewModel.Deactivate();
    public void ApplySize(WidgetSize size) => ViewModel.SetSize(size);
}
