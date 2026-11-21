using ChatApp.Server.Abstractions;

namespace ChatApp.Server.Security;

// Ayni hesaba 5 kez ust uste yanlis sifre girilirse hesap 1 dakika kilitlenir.
// Amac: sifreyi deneme-yanilma ile bulmayi engellemek. Dogru giris sayaci sifirlar.
// Bilgi bellekte tutulur; sunucu yeniden baslayinca sifirlanir (1 dakikalik kilit icin yeterli).
public sealed class GirisKilidi
{
    public const int DenemeHakki = 5;
    public static readonly TimeSpan KilitSuresi = TimeSpan.FromMinutes(1);
    private const int TemizlikSiniri = 10_000;

    private readonly ISaat _saat;
    private readonly Dictionary<string, (int Hata, DateTimeOffset? KilitBitis)> _durumlar = new();
    private readonly Lock _kilit = new();

    public GirisKilidi(ISaat saat)
    {
        ArgumentNullException.ThrowIfNull(saat);
        _saat = saat;
    }

    public bool KilitliMi(string kullaniciAdi)
    {
        lock (_kilit)
        {
            return _durumlar.TryGetValue(kullaniciAdi, out var durum)
                && durum.KilitBitis is { } bitis
                && bitis > _saat.SimdiUtc;
        }
    }

    public void BasarisizDeneme(string kullaniciAdi)
    {
        lock (_kilit)
        {
            if (_durumlar.Count > TemizlikSiniri)
            {
                SuresiDolanlariSil();
            }

            _durumlar.TryGetValue(kullaniciAdi, out var durum);
            if (durum.KilitBitis is { } eskiBitis && eskiBitis <= _saat.SimdiUtc)
            {
                durum = default;   // kilit suresi bitmis: sayac bastan
            }

            int hata = durum.Hata + 1;
            _durumlar[kullaniciAdi] = hata >= DenemeHakki
                ? (0, _saat.SimdiUtc + KilitSuresi)
                : (hata, durum.KilitBitis);
        }
    }

    public void BasariliGiris(string kullaniciAdi)
    {
        lock (_kilit)
        {
            _durumlar.Remove(kullaniciAdi);
        }
    }

    private void SuresiDolanlariSil()
    {
        DateTimeOffset simdi = _saat.SimdiUtc;
        foreach (string ad in _durumlar.Where(d => d.Value.KilitBitis is { } b && b <= simdi).Select(d => d.Key).ToList())
        {
            _durumlar.Remove(ad);
        }
    }
}
