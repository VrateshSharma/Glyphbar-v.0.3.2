using CommunityToolkit.Mvvm.ComponentModel;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Glyphbar.App.ViewModels;

public sealed partial class DockItemViewModel(DockSlot slot) : ObservableObject
{
    public string AppId { get; } = slot.AppId;

    [ObservableProperty] public partial DockSlot Slot { get; set; } = slot;
    [ObservableProperty] public partial ImageSource? Icon { get; set; }
    [ObservableProperty] public partial double IconSize { get; set; }
    [ObservableProperty] public partial double SlotSize { get; set; }
    [ObservableProperty] public partial bool ShowIndicator { get; set; } = true;

    public string Tooltip => Slot.Windows.Count switch
    {
        0 => Slot.Name,
        1 => Slot.Windows[0].Title,
        var n => $"{Slot.Name} ({n} windows)",
    };

    public bool IsRunning => Slot.Windows.Count > 0;
    public bool IsActive => Slot.Windows.Any(w => w.IsForeground);
    public double IndicatorWidth => IsActive ? 14 : 5;
    public Visibility IndicatorVisibility => IsRunning && ShowIndicator ? Visibility.Visible : Visibility.Collapsed;

    public override string ToString() => Tooltip;

    partial void OnSlotChanged(DockSlot value) => Refresh();

    partial void OnShowIndicatorChanged(bool value) => Refresh();

    private void Refresh()
    {
        OnPropertyChanged(nameof(Tooltip));
        OnPropertyChanged(nameof(IndicatorWidth));
        OnPropertyChanged(nameof(IndicatorVisibility));
    }
}
