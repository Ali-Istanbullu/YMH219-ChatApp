using System.Collections.Concurrent;
using ChatApp.Protocol.Paketler;
using ChatApp.Server.Abstractions;

namespace ChatApp.UnitTests.Fakes;

// Gonderilen paketleri biriktiren sahte istemci oturumu.
public sealed class SahteOturum : IIstemciOturumu
{
    public SahteOturum(string ipAdresi = "10.0.0.1") => IpAdresi = ipAdresi;

    public Guid BaglantiId { get; } = Guid.NewGuid();
    public string IpAdresi { get; }
    public DateTimeOffset BaglanmaZamani { get; } = DateTimeOffset.UnixEpoch;
    public int? KullaniciId { get; private set; }
    public string? GorunenAd { get; private set; }

    public ConcurrentQueue<Paket> Gelenler { get; } = new();
    public bool Kapatildi { get; private set; }
    public OturumSonlandi? KapanisSebebi { get; private set; }

    public void GirisYapildi(int kullaniciId, string gorunenAd)
    {
        KullaniciId = kullaniciId;
        GorunenAd = gorunenAd;
    }

    public Task GonderAsync(Paket paket)
    {
        if (!Kapatildi)
        {
            Gelenler.Enqueue(paket);
        }
        return Task.CompletedTask;
    }

    public Task KapatAsync(OturumSonlandi? sebep)
    {
        if (!Kapatildi)
        {
            KapanisSebebi = sebep;
        }
        Kapatildi = true;
        return Task.CompletedTask;
    }

    public IReadOnlyList<T> Paketler<T>() where T : Paket => Gelenler.OfType<T>().ToList();

    public void Temizle() => Gelenler.Clear();
}
