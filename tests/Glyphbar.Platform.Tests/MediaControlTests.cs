using Glyphbar.Core.Models;
using Glyphbar.Platform.Media;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Glyphbar.Platform.Tests;

public class MediaControlTests(ITestOutputHelper output)
{
    /// <summary>
    /// Toggles play/pause on one named session and checks Windows reports the change. It only runs when
    /// GLYPHBAR_MEDIA_TEST_SOURCE names a session, so it can never touch whatever else is playing on the machine.
    /// </summary>
    [Fact]
    public async Task Play_pause_changes_the_state_of_an_owned_session()
    {
        string? source = Environment.GetEnvironmentVariable("GLYPHBAR_MEDIA_TEST_SOURCE");
        using var media = new MediaService(NullLogger<MediaService>.Instance);
        await media.StartAsync();
        await Task.Delay(1500);

        foreach (var s in media.Sessions)
            output.WriteLine($"session: {s.SessionId} | {s.SourceName} | {s.Title} | {s.Playback} | play={s.CanPlay} pause={s.CanPause}");
        if (string.IsNullOrEmpty(source)) return;

        MediaSessionInfo? Find() => media.Sessions.FirstOrDefault(s => s.SessionId.Contains(source, StringComparison.OrdinalIgnoreCase));
        var before = Find();
        Assert.NotNull(before);
        Assert.False(before.SessionId.Contains("chrome", StringComparison.OrdinalIgnoreCase));

        await media.PlayPauseAsync(before.SessionId);
        await Task.Delay(2000);
        var after = Find();
        Assert.NotNull(after);
        output.WriteLine($"{before.Playback} -> {after.Playback}");
        Assert.NotEqual(before.Playback, after.Playback);

        await media.PlayPauseAsync(after.SessionId);
        await Task.Delay(1500);
        Assert.Equal(before.Playback, Find()!.Playback);
    }
}
