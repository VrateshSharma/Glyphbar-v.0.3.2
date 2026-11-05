using Glyphbar.App.ViewModels;
using Glyphbar.Core.Models;
using Microsoft.UI.Xaml.Controls;

namespace Glyphbar.App.Components.Widgets;

public sealed partial class BatteryWidget : UserControl, IWidgetView
{
    public BatteryWidget(BatteryWidgetViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public BatteryWidgetViewModel ViewModel { get; }
    public void Activate() => ViewModel.Activate();
    public void Deactivate() => ViewModel.Deactivate();
    public void ApplySize(WidgetSize size) { }
}
