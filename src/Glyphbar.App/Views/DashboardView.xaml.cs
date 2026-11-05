using Glyphbar.App.Services;
using Glyphbar.App.ViewModels;
using Glyphbar.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Glyphbar.App.Views;

public sealed partial class DashboardView : UserControl
{
    public DashboardView(DashboardViewModel viewModel, WidgetLayoutService layout, IServiceProvider services)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Board.Initialize(layout, services, maxColumns: 6);
        ThemeApplier.Track(this, services.GetRequiredService<ISettingsStore>());
    }

    public DashboardViewModel ViewModel { get; }
    public UIElement DragRegion => TitleArea;

    public void SetActive(bool active) => Board.SetActive(active);
    public void Detach() => Board.Detach();
}
