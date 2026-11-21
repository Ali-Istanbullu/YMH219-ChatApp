using ChatApp.Server.Abstractions;

namespace ChatApp.UnitTests.Fakes;

// Elle ilerletilen saat.
public sealed class SahteSaat : ISaat
{
    public DateTimeOffset SimdiUtc { get; set; } = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    public void Ilerlet(TimeSpan sure) => SimdiUtc += sure;
}
