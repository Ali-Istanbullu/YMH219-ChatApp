using System.Collections.Concurrent;
using ChatApp.Client.Abstractions;

namespace ChatApp.UnitTests.Fakes;

public sealed class SahteIstemciGunlukcu : IGunlukcu
{
    public ConcurrentQueue<string> Satirlar { get; } = new();

    public void Bilgi(string mesaj) => Satirlar.Enqueue("BILGI " + mesaj);
    public void Uyari(string mesaj) => Satirlar.Enqueue("UYARI " + mesaj);
    public void Hata(Exception hata, string mesaj) => Satirlar.Enqueue("HATA " + mesaj);
}
