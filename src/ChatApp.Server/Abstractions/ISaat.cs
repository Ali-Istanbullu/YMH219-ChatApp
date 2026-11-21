namespace ChatApp.Server.Abstractions;

// Simdiki zaman (UTC). Testlerde zamani elle ilerletebilmek icin arayuz.
public interface ISaat
{
    DateTimeOffset SimdiUtc { get; }
}
