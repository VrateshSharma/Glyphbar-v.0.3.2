using Glyphbar.Core.Interfaces;

namespace Glyphbar.Core.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
