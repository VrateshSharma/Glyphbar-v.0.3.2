using Glyphbar.Core.Services;

namespace Glyphbar.Core.Tests;

public class HotkeyTests
{
    [Theory]
    [InlineData("Alt+Space", HotkeyModifiers.Alt, 0x20u)]
    [InlineData("Win+Shift+G", HotkeyModifiers.Win | HotkeyModifiers.Shift, 0x47u)]
    [InlineData("ctrl + alt + f12", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x7Bu)]
    [InlineData("Win+Alt+5", HotkeyModifiers.Win | HotkeyModifiers.Alt, 0x35u)]
    public void Parses(string text, HotkeyModifiers mods, uint key)
    {
        Assert.True(HotkeyGesture.TryParse(text, out var g));
        Assert.Equal(mods, g.Modifiers);
        Assert.Equal(key, g.VirtualKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("G")]
    [InlineData("Alt")]
    [InlineData("Alt+Banana")]
    [InlineData("Alt+A+B")]
    [InlineData("Win+F25")]
    public void Rejects(string text) => Assert.False(HotkeyGesture.TryParse(text, out _));

    [Theory]
    [InlineData("Win+L")]
    [InlineData("win+l")]
    [InlineData("Alt+F4")]
    [InlineData("Alt+Tab")]
    public void Flags_windows_owned_gestures(string text)
    {
        Assert.True(HotkeyGesture.TryParse(text, out var g));
        Assert.True(g.IsReserved);
    }

    [Theory]
    [InlineData("Alt+Space")]
    [InlineData("Win+Shift+G")]
    [InlineData("Win+Shift+W")]
    [InlineData("Win+Shift+P")]
    public void Prd_defaults_are_not_reserved(string text)
    {
        Assert.True(HotkeyGesture.TryParse(text, out var g));
        Assert.False(g.IsReserved);
    }

    [Fact]
    public void Round_trips_through_text()
    {
        Assert.True(HotkeyGesture.TryParse("shift+win+g", out var g));
        Assert.Equal("Win+Shift+G", g.ToString());
        Assert.True(HotkeyGesture.TryParse(g.ToString(), out var again));
        Assert.Equal(g, again);
    }
}
