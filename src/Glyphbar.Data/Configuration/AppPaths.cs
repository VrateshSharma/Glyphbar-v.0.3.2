namespace Glyphbar.Data.Configuration;

/// <summary>All Glyphbar state lives under one per-user directory.</summary>
public sealed class AppPaths
{
    public AppPaths(string? root = null)
    {
        Root = root
            ?? Environment.GetEnvironmentVariable("GLYPHBAR_DATA_DIR")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Glyphbar");
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Logs);
    }

    public string Root { get; }
    public string Database => Path.Combine(Root, "glyphbar.db");
    public string Settings => Path.Combine(Root, "settings.json");
    public string Logs => Path.Combine(Root, "logs");
}
