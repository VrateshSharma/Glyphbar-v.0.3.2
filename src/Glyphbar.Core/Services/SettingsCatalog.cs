namespace Glyphbar.Core.Services;

public sealed record SettingsEntry(string Title, string Uri, string[] Keywords);

/// <summary>Fixed allow-list of Windows Settings pages. Only these ms-settings URIs may be opened.</summary>
public static class SettingsCatalog
{
    public static IReadOnlyList<SettingsEntry> Entries { get; } =
    [
        new("Display", "ms-settings:display", ["screen", "resolution", "scale", "brightness", "monitor"]),
        new("Sound", "ms-settings:sound", ["audio", "volume", "speaker", "microphone"]),
        new("Notifications", "ms-settings:notifications", ["alerts", "focus"]),
        new("Power & battery", "ms-settings:powersleep", ["sleep", "battery", "energy"]),
        new("Storage", "ms-settings:storagesense", ["disk", "drive", "space", "cleanup"]),
        new("Bluetooth & devices", "ms-settings:bluetooth", ["pair", "mouse", "keyboard", "printer"]),
        new("Wi-Fi", "ms-settings:network-wifi", ["wireless", "internet", "network"]),
        new("Network & internet", "ms-settings:network", ["ethernet", "vpn", "proxy"]),
        new("Personalisation", "ms-settings:personalization", ["theme", "wallpaper", "background", "colors"]),
        new("Taskbar", "ms-settings:taskbar", ["auto-hide", "autohide", "tray", "start"]),
        new("Installed apps", "ms-settings:appsfeatures", ["uninstall", "programs", "remove"]),
        new("Default apps", "ms-settings:defaultapps", ["browser", "associations"]),
        new("Startup apps", "ms-settings:startupapps", ["login", "boot"]),
        new("Accounts", "ms-settings:yourinfo", ["user", "profile", "sign-in"]),
        new("Date & time", "ms-settings:dateandtime", ["clock", "timezone"]),
        new("Language & region", "ms-settings:regionlanguage", ["locale", "format", "keyboard layout"]),
        new("Accessibility", "ms-settings:easeofaccess", ["contrast", "narrator", "magnifier"]),
        new("Privacy & security", "ms-settings:privacy", ["permissions", "camera", "location"]),
        new("Windows Update", "ms-settings:windowsupdate", ["updates", "patch"]),
        new("About this PC", "ms-settings:about", ["system", "specs", "device name"]),
    ];

    public static bool IsAllowed(string uri) =>
        Entries.Any(e => e.Uri.Equals(uri, StringComparison.OrdinalIgnoreCase));
}
