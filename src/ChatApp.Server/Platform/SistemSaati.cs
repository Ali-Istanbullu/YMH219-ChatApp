using ChatApp.Server.Abstractions;

namespace ChatApp.Server.Platform;

public sealed class SistemSaati : ISaat
{
    public DateTimeOffset SimdiUtc => DateTimeOffset.UtcNow;
}
