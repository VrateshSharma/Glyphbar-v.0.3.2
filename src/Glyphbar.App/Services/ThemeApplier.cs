using Glyphbar.Core.Interfaces;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Glyphbar.App.Services;

/// <summary>Applies the chosen font at a view's root, so every text element under it inherits the change live.</summary>
public static class ThemeApplier
{
    public static void Track(Control root, ISettingsStore settings)
    {
        void Apply() => root.FontFamily = new FontFamily(settings.Current.Appearance.FontFamily);
        Apply();
        EventHandler handler = (_, _) => Apply();
        settings.Changed += handler;
        root.Unloaded += (_, _) => settings.Changed -= handler;
        root.Loaded += (_, _) =>
        {
            settings.Changed -= handler;
            settings.Changed += handler;
            Apply();
        };
    }
}
