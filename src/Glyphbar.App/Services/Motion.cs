using System.Numerics;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI.ViewManagement;

namespace Glyphbar.App.Services;

/// <summary>
/// Composition animations, which run off the UI thread. Windows' "Animation effects" setting always wins,
/// so turning that off removes all motion here too. Nothing here loops; idle means no animation.
/// </summary>
public static class Motion
{
    private static readonly UISettings UiSettings = new();

    public static ISettingsStore? Settings { get; set; }

    private static double Factor
    {
        get
        {
            if (!UiSettings.AnimationsEnabled) return 0;
            return Settings?.Current.Appearance.Animation switch
            {
                AnimationIntensity.Off => 0,
                AnimationIntensity.Full => 1.4,
                _ => 1,
            };
        }
    }

    public static bool Enabled => Factor > 0;

    private static TimeSpan Duration(double ms) => TimeSpan.FromMilliseconds(ms * Math.Max(Factor, 0.01));

    private static Visual Prepare(UIElement element)
    {
        ElementCompositionPreview.SetIsTranslationEnabled(element, true);
        return ElementCompositionPreview.GetElementVisual(element);
    }

    private static CubicBezierEasingFunction Ease(Compositor c) =>
        c.CreateCubicBezierEasingFunction(new Vector2(0.2f, 0f), new Vector2(0f, 1f));

    /// <summary>Scales about the element's centre, e.g. on hover.</summary>
    public static void Scale(FrameworkElement element, float to, double ms = 140)
    {
        var visual = Prepare(element);
        visual.CenterPoint = new Vector3((float)element.ActualWidth / 2, (float)element.ActualHeight / 2, 0);
        if (!Enabled)
        {
            visual.Scale = Vector3.One;
            return;
        }
        var c = visual.Compositor;
        var a = c.CreateVector3KeyFrameAnimation();
        a.InsertKeyFrame(1f, new Vector3(to, to, 1), Ease(c));
        a.Duration = Duration(ms);
        visual.StartAnimation("Scale", a);
    }

    /// <summary>A short press-and-release pulse for app launches.</summary>
    public static void Pulse(FrameworkElement element)
    {
        if (!Enabled) return;
        var visual = Prepare(element);
        visual.CenterPoint = new Vector3((float)element.ActualWidth / 2, (float)element.ActualHeight / 2, 0);
        var c = visual.Compositor;
        var a = c.CreateVector3KeyFrameAnimation();
        a.InsertKeyFrame(0.35f, new Vector3(0.82f, 0.82f, 1), Ease(c));
        a.InsertKeyFrame(1f, Vector3.One, Ease(c));
        a.Duration = Duration(280);
        visual.StartAnimation("Scale", a);
    }

    /// <summary>Fades and rises in; used for panels, the search overlay and month changes.</summary>
    public static void Enter(UIElement element, float offsetY = 14, double ms = 200)
    {
        var visual = Prepare(element);
        if (!Enabled)
        {
            visual.Opacity = 1;
            return;
        }
        var c = visual.Compositor;
        var fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0f, 0f);
        fade.InsertKeyFrame(1f, 1f, Ease(c));
        fade.Duration = Duration(ms);
        var rise = c.CreateVector3KeyFrameAnimation();
        rise.InsertKeyFrame(0f, new Vector3(0, offsetY, 0));
        rise.InsertKeyFrame(1f, Vector3.Zero, Ease(c));
        rise.Duration = Duration(ms);
        visual.StartAnimation("Opacity", fade);
        visual.StartAnimation("Translation", rise);
    }

    public static Task ExitAsync(UIElement element, double ms = 120)
    {
        var visual = Prepare(element);
        if (!Enabled) return Task.CompletedTask;
        var c = visual.Compositor;
        var batch = c.CreateScopedBatch(CompositionBatchTypes.Animation);
        var done = new TaskCompletionSource();
        batch.Completed += (_, _) => done.TrySetResult();
        var fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(1f, 0f);
        fade.Duration = Duration(ms);
        visual.StartAnimation("Opacity", fade);
        batch.End();
        return done.Task;
    }
}
