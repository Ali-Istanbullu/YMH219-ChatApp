using ChatApp.Protocol.Iletisim;
using ChatApp.Protocol.Paketler;
using ChatApp.Server.Abstractions;

namespace ChatApp.Server.Network;

// Bir istemcinin TLS baglantisi. Yazmalar sirayla yapilir (ayni anda iki paket karismasin).
// Baglanti kopmussa gonderme sessizce kapanir: kopan baglanti ag hayatinda BEKLENEN bir durumdur.
public sealed class IstemciBaglantisi : IIstemciOturumu
{
    private static readonly TimeSpan YazmaZamanAsimi = TimeSpan.FromSeconds(10);

    private readonly Stream _akis;
    private readonly SemaphoreSlim _yazmaKilidi = new(1, 1);
    private int _kapandi;

    public IstemciBaglantisi(Stream akis, string ipAdresi, DateTimeOffset baglanmaZamani)
    {
        ArgumentNullException.ThrowIfNull(akis);
        _akis = akis;
        IpAdresi = ipAdresi;
        BaglanmaZamani = baglanmaZamani;
    }

    public Guid BaglantiId { get; } = Guid.NewGuid();
    public string IpAdresi { get; }
    public DateTimeOffset BaglanmaZamani { get; }
    public int? KullaniciId { get; private set; }
    public string? GorunenAd { get; private set; }
    public bool KapandiMi => Volatile.Read(ref _kapandi) == 1;

    public void GirisYapildi(int kullaniciId, string gorunenAd)
    {
        KullaniciId = kullaniciId;
        GorunenAd = gorunenAd;
    }

    public async Task GonderAsync(Paket paket)
    {
        if (KapandiMi)
        {
            return;
        }

        byte[] veri = PaketYazici.Serilestir(paket);
        await _yazmaKilidi.WaitAsync();
        try
        {
            // Yavas ya da donmus bir istemci sunucuyu bekletmesin.
            using var zamanAsimi = new CancellationTokenSource(YazmaZamanAsimi);
            await _akis.WriteAsync(veri, zamanAsimi.Token);
            await _akis.FlushAsync(zamanAsimi.Token);
        }
        catch (Exception hata) when (hata is IOException or ObjectDisposedException or OperationCanceledException)
        {
            Kapat();
        }
        finally
        {
            _yazmaKilidi.Release();
        }
    }

    public async Task KapatAsync(OturumSonlandi? sebep)
    {
        if (sebep is not null)
        {
            await GonderAsync(sebep);
        }
        Kapat();
    }

    // Akisi kapatir; okuma dongusu bunu "baglanti kapandi" olarak gorur ve temizligi yapar.
    private void Kapat()
    {
        if (Interlocked.Exchange(ref _kapandi, 1) == 0)
        {
            _akis.Dispose();
        }
    }
}
