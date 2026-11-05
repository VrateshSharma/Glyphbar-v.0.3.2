using Glyphbar.Core.Interfaces;
using Glyphbar.Core.Models;

namespace Glyphbar.Core.Tests;

internal sealed class TestSettings : ISettingsStore
{
    public AppSettings Current { get; private set; } = new();
    public event EventHandler? Changed;

    public void Update(Action<AppSettings> change)
    {
        change(Current);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Task FlushAsync() => Task.CompletedTask;
    public void ResetToDefaults() => Current = new AppSettings();
}
