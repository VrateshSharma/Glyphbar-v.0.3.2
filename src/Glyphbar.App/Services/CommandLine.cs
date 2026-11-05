using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace Glyphbar.App.Services;

/// <summary>Command-line verbs. These double as a recovery route if the bar can't be reached.</summary>
public sealed record CommandLine(bool Exit, string? Show, bool Startup)
{
    public static CommandLine From(AppActivationArguments activation)
    {
        if (activation.Kind == ExtendedActivationKind.StartupTask) return new CommandLine(false, null, true);
        string text = activation.Data is ILaunchActivatedEventArgs launch ? launch.Arguments ?? "" : "";
        return Parse(text);
    }

    public static CommandLine Parse(string text)
    {
        bool exit = false, startup = false;
        string? show = null;
        foreach (var token in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Equals("--exit", StringComparison.OrdinalIgnoreCase)) exit = true;
            else if (token.Equals("--startup", StringComparison.OrdinalIgnoreCase)) startup = true;
            else if (token.StartsWith("--show:", StringComparison.OrdinalIgnoreCase)) show = token[7..].ToLowerInvariant();
        }
        return new CommandLine(exit, show, startup);
    }
}
