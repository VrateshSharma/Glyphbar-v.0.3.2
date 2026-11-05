using Glyphbar.Core.Models;

namespace Glyphbar.App.Components.Widgets;

/// <summary>What the board needs from a widget. Each widget is independent of the others.</summary>
public interface IWidgetView
{
    void Activate();
    void Deactivate();
    void ApplySize(WidgetSize size);
}
