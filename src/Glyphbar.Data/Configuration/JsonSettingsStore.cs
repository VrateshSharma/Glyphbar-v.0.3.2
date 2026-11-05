using System.Text.Json;
using System.Text.Json.Serialization;
using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;
using Glyphbar.Core.Services;
using Microsoft.Extensions.Logging;

namespace Glyphbar.Data.Configuration;

/// <summary>
/// settings.json with crash-safe writes: serialise to a temp file, then atomically swap it in,
/// keeping the previous version as settings.json.bak. A corrupt file falls back to the backup.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore, IDisposable
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;
    private readonly ILogger<JsonSettingsStore> _logger;
    private readonly Timer _saveTimer;
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly TimeSpan _debounce;
    private int _dirty;

    public JsonSettingsStore(AppPaths paths, ILogger<JsonSettingsStore> logger, TimeSpan? debounce = null)
    {
        _path = paths.Settings;
        _logger = logger;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(400);
        _saveTimer = new Timer(_ => _ = FlushAsync());
        Current = Load();
    }

    public AppSettings Current { get; private set; }
    public event EventHandler? Changed;

    private AppSettings Load()
    {
        foreach (var candidate in new[] { _path, _path + ".bak" })
        {
            if (!File.Exists(candidate)) continue;
            try
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(candidate), Options);
                if (loaded is not null) return SettingsSanitizer.Sanitize(loaded);
            }
            catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(e, "Could not read settings from {Path}", candidate);
                TryPreserveCorrupt(candidate);
            }
        }
        return new AppSettings();
    }

    private void TryPreserveCorrupt(string path)
    {
        try { File.Copy(path, path + ".corrupt", overwrite: true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(e, "Could not keep a copy of the corrupt settings file");
        }
    }

    public void Update(Action<AppSettings> change)
    {
        change(Current);
        SettingsSanitizer.Sanitize(Current);
        Interlocked.Exchange(ref _dirty, 1);
        _saveTimer.Change(_debounce, Timeout.InfiniteTimeSpan);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ResetToDefaults()
    {
        // Search history and first-run state are not appearance preferences, but a reset means a clean slate.
        Current = new AppSettings { System = { FirstRunGuideShown = true } };
        Interlocked.Exchange(ref _dirty, 1);
        _saveTimer.Change(_debounce, Timeout.InfiniteTimeSpan);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public async Task FlushAsync()
    {
        if (Interlocked.Exchange(ref _dirty, 0) == 0) return;
        await _ioLock.WaitAsync().ConfigureAwait(false);
        try
        {
            string json = JsonSerializer.Serialize(Current, Options);
            string temp = _path + ".tmp";
            await File.WriteAllTextAsync(temp, json).ConfigureAwait(false);
            if (File.Exists(_path))
                File.Replace(temp, _path, _path + ".bak", ignoreMetadataErrors: true);
            else
                File.Move(temp, _path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Interlocked.Exchange(ref _dirty, 1);
            _logger.LogError(e, "Could not save settings");
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public void Dispose()
    {
        _saveTimer.Dispose();
        FlushAsync().GetAwaiter().GetResult();
        _ioLock.Dispose();
    }
}
