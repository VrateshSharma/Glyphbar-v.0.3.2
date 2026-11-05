using Glyphbar.App.ViewModels;
using Glyphbar.Core.Models;
using Microsoft.UI.Xaml.Controls;

namespace Glyphbar.App.Components.Widgets;

public sealed partial class StorageWidget : UserControl, IWidgetView
{
    public StorageWidget(StorageWidgetViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    public StorageWidgetViewModel ViewModel { get; }
    public void Activate() => ViewModel.Activate();
    public void Deactivate() => ViewModel.Deactivate();
    public void ApplySize(WidgetSize size) { }
}
