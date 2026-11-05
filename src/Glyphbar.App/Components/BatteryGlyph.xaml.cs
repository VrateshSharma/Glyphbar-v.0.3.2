using Glyphbar.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Glyphbar.App.Components;

public sealed partial class BatteryGlyph : UserControl
{
    public static readonly DependencyProperty PercentProperty = DependencyProperty.Register(
        nameof(Percent), typeof(double), typeof(BatteryGlyph), new PropertyMetadata(0d, (d, _) => ((BatteryGlyph)d).Update()));
    public static readonly DependencyProperty ChargingProperty = DependencyProperty.Register(
        nameof(Charging), typeof(bool), typeof(BatteryGlyph), new PropertyMetadata(false, (d, e) => ((BatteryGlyph)d).OnCharging((bool)e.NewValue)));
    public static readonly DependencyProperty WarningProperty = DependencyProperty.Register(
        nameof(Warning), typeof(bool), typeof(BatteryGlyph), new PropertyMetadata(false, (d, _) => ((BatteryGlyph)d).Update()));

    public BatteryGlyph()
    {
        InitializeComponent();
        Track.SizeChanged += (_, _) => Update();
    }

    public double Percent { get => (double)GetValue(PercentProperty); set => SetValue(PercentProperty, value); }
    public bool Charging { get => (bool)GetValue(ChargingProperty); set => SetValue(ChargingProperty, value); }
    public bool Warning { get => (bool)GetValue(WarningProperty); set => SetValue(WarningProperty, value); }

    private void OnCharging(bool charging)
    {
        Update();
        // One pulse on plug-in, not a loop: nothing animates while the UI is idle.
        if (charging) Motion.Pulse(this);
    }

    private void Update()
    {
        Fill.Width = Math.Max(0, Track.ActualWidth * Math.Clamp(Percent, 0, 100) / 100);
        Fill.Height = Math.Max(0, Track.ActualHeight);
        Bolt.Visibility = Charging ? Visibility.Visible : Visibility.Collapsed;
        // Monochrome design: a warning is shown by the shape going hollow and blinking nothing.
        Fill.Opacity = Warning ? 0.55 : 1;
        Shell.BorderThickness = new Thickness(Warning ? 1.8 : 1.2);
    }
}
