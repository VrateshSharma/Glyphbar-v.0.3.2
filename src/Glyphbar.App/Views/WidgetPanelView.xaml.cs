using Glyphbar.App.Services;
using Glyphbar.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace Glyphbar.App.Views;

public sealed partial class WidgetPanelView : UserControl, IFloatingContent
{
    public WidgetPanelView(WidgetLayoutService layout, IServiceProvider services)
    {
        InitializeComponent();
        Board.Initialize(layout, services, maxColumns: 2);
        ThemeApplier.Track(this, services.GetRequiredService<ISettingsStore>());
    }

    public void OnShown() => Board.SetActive(true);
    public void OnHidden() => Board.SetActive(false);
}
