using ChatApp.Server.Data;

namespace ChatApp.UnitTests.Fakes;

// Veritabani yerine bellekte calisan hesap deposu. Davet kodlarini da bilir (kayit ayni islemde kodu harcar).
public sealed class BellekKullaniciDeposu : IKullaniciDeposu
{
    private readonly Dictionary<int, KullaniciKaydi> _kullanicilar = new();
    private readonly Lock _kilit = new();

    public BellekKullaniciDeposu(BellekDavetDeposu davetler) => Davetler = davetler;

    public BellekDavetDeposu Davetler { get; }

    // true: siradaki cagri veritabani kopmus gibi DepoHatasi firlatir.
    public bool Kopuk { get; set; }

    // true: kodda hata (bug) varmis gibi InvalidOperationException firlatir.
    public bool BugOlsun { get; set; }

    public Task<KayitDurumu> OlusturAsync(YeniKullanici k, string davetKodu)
    {
        KopuksaFirlat();
        lock (_kilit)
        {
            if (_kullanicilar.ContainsKey(k.Id))
            {
                return Task.FromResult(KayitDurumu.IdCakisti);
            }
            if (_kullanicilar.Values.Any(x => x.KullaniciAdi == k.KullaniciAdi))
            {
                return Task.FromResult(KayitDurumu.KullaniciAdiAlinmis);
            }
            if (!Davetler.Harca(davetKodu, k.Id, k.KayitZamani))
            {
                return Task.FromResult(KayitDurumu.DavetKoduGecersiz);
            }
            _kullanicilar[k.Id] = new KullaniciKaydi(k.Id, k.KullaniciAdi, k.GorunenAd, k.SifreHash, k.Tuz,
                k.KayitZamani, null, false, null, null);
            return Task.FromResult(KayitDurumu.Basarili);
        }
    }

    public Task<KullaniciKaydi?> KullaniciAdiylaBulAsync(string kullaniciAdi)
    {
        KopuksaFirlat();
        lock (_kilit)
        {
            return Task.FromResult(_kullanicilar.Values.FirstOrDefault(k => k.KullaniciAdi == kullaniciAdi));
        }
    }

    public Task<KullaniciKaydi?> IdIleBulAsync(int id)
    {
        KopuksaFirlat();
        lock (_kilit)
        {
            return Task.FromResult(_kullanicilar.GetValueOrDefault(id));
        }
    }

    public Task SonGirisiYazAsync(int id, DateTimeOffset zaman) => Guncelle(id, k => k with { SonGiris = zaman });

    public Task SifreyiGuncelleAsync(int id, byte[] sifreHash, byte[] tuz) =>
        Guncelle(id, k => k with { SifreHash = sifreHash, Tuz = tuz });

    public Task YasakDurumunuYazAsync(int id, bool yasakli, string? sebep, DateTimeOffset? zaman) =>
        Guncelle(id, k => k with { Yasakli = yasakli, YasakSebebi = sebep, YasakZamani = zaman });

    public Task<IReadOnlyList<KullaniciKaydi>> YasaklilariGetirAsync()
    {
        lock (_kilit)
        {
            return Task.FromResult<IReadOnlyList<KullaniciKaydi>>(_kullanicilar.Values.Where(k => k.Yasakli).ToList());
        }
    }

    public Task<int> HesapSayisiAsync()
    {
        lock (_kilit)
        {
            return Task.FromResult(_kullanicilar.Count);
        }
    }

    private Task Guncelle(int id, Func<KullaniciKaydi, KullaniciKaydi> degisiklik)
    {
        KopuksaFirlat();
        lock (_kilit)
        {
            if (_kullanicilar.TryGetValue(id, out KullaniciKaydi? k))
            {
                _kullanicilar[id] = degisiklik(k);
            }
        }
        return Task.CompletedTask;
    }

    private void KopuksaFirlat()
    {
        if (BugOlsun)
        {
            throw new InvalidOperationException("Sahte bug");
        }
        if (Kopuk)
        {
            throw new DepoHatasi("Sahte: veritabani kopuk", new IOException("baglanti yok"));
        }
    }
}
