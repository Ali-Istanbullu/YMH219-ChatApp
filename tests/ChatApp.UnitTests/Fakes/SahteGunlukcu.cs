using System.Collections.Concurrent;
using ChatApp.Server.Abstractions;

namespace ChatApp.UnitTests.Fakes;

public sealed class SahteGunlukcu : IGunlukcu
{
    public ConcurrentQueue<string> Satirlar { get; } = new();
    public ConcurrentQueue<Exception> Hatalar { get; } = new();

    public void Bilgi(string mesaj) => Satirlar.Enqueue("BILGI " + mesaj);
    public void Uyari(string mesaj) => Satirlar.Enqueue("UYARI " + mesaj);

    public void Hata(Exception hata, string mesaj)
    {
        Hatalar.Enqueue(hata);
        Satirlar.Enqueue("HATA " + mesaj);
    }
}
