using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Microsoft.Extensions.Logging;
using Windows.Media.Control;
using Windows.Storage.Streams;
using WinPlaybackStatus = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus;

namespace Glyphbar.Platform.Media;

/// <summary>
/// Reads and controls the sessions other apps publish to Windows. It does not play anything itself.
/// Everything is event-driven; there is no polling.
/// </summary>
public sealed class MediaService(ILogger<MediaService> logger) : IMediaService, IDisposable
{
    private const long MaxArtworkBytes = 8 * 1024 * 1024;

    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> _sessions = [];
    private readonly HashSet<GlobalSystemMediaTransportControlsSession> _subscribed = [];
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private volatile IReadOnlyList<MediaSessionInfo> _snapshot = [];
    private int _refreshQueued;

    public IReadOnlyList<MediaSessionInfo> Sessions => _snapshot;
    public string? CurrentSessionId { get; private set; }
    public event EventHandler? Changed;

    public async Task StartAsync()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.SessionsChanged += (_, _) => QueueRefresh();
            _manager.CurrentSessionChanged += (_, _) => QueueRefresh();
            QueueRefresh();
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Media session manager unavailable");
        }
    }

    private void QueueRefresh()
    {
        // Coalesce bursts: a track change raises several events at once.
        if (Interlocked.Exchange(ref _refreshQueued, 1) == 1) return;
        _ = Task.Run(async () =>
        {
            await Task.Delay(120).ConfigureAwait(false);
            Interlocked.Exchange(ref _refreshQueued, 0);
            await RefreshAsync().ConfigureAwait(false);
        });
    }

    private async Task RefreshAsync()
    {
        if (_manager is null) return;
        await _refreshGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var sessions = _manager.GetSessions();
            var infos = new List<MediaSessionInfo>(sessions.Count);
            var ids = new Dictionary<string, int>();
            _sessions.Clear();

            foreach (var session in sessions)
            {
                if (_subscribed.Add(session))
                {
                    session.MediaPropertiesChanged += (_, _) => QueueRefresh();
                    session.PlaybackInfoChanged += (_, _) => QueueRefresh();
                }

                // Two windows of one app share a source id, so make the key unique.
                string source = session.SourceAppUserModelId;
                ids[source] = ids.GetValueOrDefault(source) + 1;
                string id = ids[source] == 1 ? source : $"{source}#{ids[source]}";
                _sessions[id] = session;
                infos.Add(await DescribeAsync(id, session).ConfigureAwait(false));
            }

            var current = _manager.GetCurrentSession();
            CurrentSessionId = current is null
                ? null
                : _sessions.FirstOrDefault(p => p.Value.SourceAppUserModelId == current.SourceAppUserModelId).Key;
            _snapshot = infos;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not refresh media sessions");
        }
        finally
        {
            _refreshGate.Release();
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task<MediaSessionInfo> DescribeAsync(string id, GlobalSystemMediaTransportControlsSession session)
    {
        string? title = null, artist = null, album = null;
        byte[]? art = null;
        try
        {
            var props = await session.TryGetMediaPropertiesAsync();
            if (props is not null)
            {
                title = NullIfEmpty(props.Title);
                artist = NullIfEmpty(props.Artist) ?? NullIfEmpty(props.AlbumArtist);
                album = NullIfEmpty(props.AlbumTitle);
                art = await ReadArtworkAsync(props.Thumbnail).ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            // Some apps fail this call while switching tracks; show what we have.
            logger.LogDebug(e, "Media properties unavailable for {Session}", id);
        }

        var info = session.GetPlaybackInfo();
        var controls = info?.Controls;
        return new MediaSessionInfo(
            id,
            FriendlySource(session.SourceAppUserModelId),
            title, artist, album, art,
            MapStatus(info?.PlaybackStatus),
            controls?.IsPlayEnabled ?? false,
            controls?.IsPauseEnabled ?? false,
            controls?.IsNextEnabled ?? false,
            controls?.IsPreviousEnabled ?? false);
    }

    private static async Task<byte[]?> ReadArtworkAsync(IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null) return null;
        using var stream = await thumbnail.OpenReadAsync();
        if (stream.Size == 0 || (long)stream.Size > MaxArtworkBytes) return null;
        var bytes = new byte[stream.Size];
        using var reader = new DataReader(stream);
        await reader.LoadAsync((uint)stream.Size);
        reader.ReadBytes(bytes);
        return bytes;
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    private static MediaPlayback MapStatus(WinPlaybackStatus? status) => status switch
    {
        WinPlaybackStatus.Closed => MediaPlayback.Closed,
        WinPlaybackStatus.Stopped => MediaPlayback.Stopped,
        WinPlaybackStatus.Paused => MediaPlayback.Paused,
        WinPlaybackStatus.Playing => MediaPlayback.Playing,
        WinPlaybackStatus.Changing => MediaPlayback.Changing,
        _ => MediaPlayback.Unknown,
    };

    /// <summary>Turns "Spotify.exe" or "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify" into a short name.</summary>
    public static string FriendlySource(string aumid)
    {
        if (string.IsNullOrEmpty(aumid)) return "Media";
        string name = aumid;
        int bang = name.IndexOf('!');
        if (bang > 0) name = name[(bang + 1)..];
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return name.Equals("App", StringComparison.OrdinalIgnoreCase) ? aumid.Split('.', '_')[0] : name;
    }

    public async Task PlayPauseAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var s)) await Try(s.TryTogglePlayPauseAsync());
    }

    public async Task NextAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var s)) await Try(s.TrySkipNextAsync());
    }

    public async Task PreviousAsync(string sessionId)
    {
        if (_sessions.TryGetValue(sessionId, out var s)) await Try(s.TrySkipPreviousAsync());
    }

    private async Task Try(Windows.Foundation.IAsyncOperation<bool> op)
    {
        try { await op; }
        catch (Exception e) { logger.LogWarning(e, "Media command failed"); }
    }

    public void Dispose() => _refreshGate.Dispose();
}
