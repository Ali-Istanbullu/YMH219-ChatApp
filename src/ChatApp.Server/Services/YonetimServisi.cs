using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;
using ChatApp.Server.Abstractions;
using ChatApp.Server.Data;
using ChatApp.Server.Security;
using ChatApp.Server.Sessions;

namespace ChatApp.Server.Services;

// Sunucu sahibinin (yonetici) islemleri: at, yasakla, yasagi kaldir, davet kodu uret.
// Sadece ChatServer penceresinden cagrilir; ag uzerinden bu islemlere ulasan bir paket YOK.
public sealed class YonetimServisi
{
    private readonly OturumYoneticisi _oturumlar;
    private readonly IKullaniciDeposu _kullanicilar;
    private readonly IDavetDeposu _davetler;
    private readonly IMesajDeposu _mesajlar;
    private readonly ISaat _saat;
    private readonly IGunlukcu _gunlukcu;

    public YonetimServisi(OturumYoneticisi oturumlar, IKullaniciDeposu kullanicilar, IDavetDeposu davetler,
        IMesajDeposu mesajlar, ISaat saat, IGunlukcu gunlukcu)
    {
        ArgumentNullException.ThrowIfNull(oturumlar);
        ArgumentNullException.ThrowIfNull(kullanicilar);
        ArgumentNullException.ThrowIfNull(davetler);
        ArgumentNullException.ThrowIfNull(mesajlar);
        ArgumentNullException.ThrowIfNull(saat);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        _oturumlar = oturumlar;
        _kullanicilar = kullanicilar;
        _davetler = davetler;
        _mesajlar = mesajlar;
        _saat = saat;
        _gunlukcu = gunlukcu;
    }

    // Baglantisini keser; hesap durur, tekrar girebilir. Cevrimici degilse false.
    public async Task<bool> AtAsync(int kullaniciId)
    {
        if (_oturumlar.Bul(kullaniciId) is not { } oturum)
        {
            return false;
        }
        await oturum.KapatAsync(new OturumSonlandi(OturumSonuSebebi.Atildi, null));
        _gunlukcu.Bilgi($"Yonetici: {kullaniciId} atildi.");
        return true;
    }

    // Hesabi kapatir ve cevrimiciyse hemen cikarir. Hesap yoksa false.
    public async Task<bool> YasaklaAsync(int kullaniciId, string? sebep)
    {
        if (await _kullanicilar.IdIleBulAsync(kullaniciId) is null)
        {
            return false;
        }

        string? duzenliSebep = sebep?.Trim();
        if (string.IsNullOrEmpty(duzenliSebep))
        {
            duzenliSebep = null;
        }
        else if (duzenliSebep.Length > 200)
        {
            duzenliSebep = duzenliSebep[..200];
        }
        await _kullanicilar.YasakDurumunuYazAsync(kullaniciId, true, duzenliSebep, _saat.SimdiUtc);
        if (_oturumlar.Bul(kullaniciId) is { } oturum)
        {
            await oturum.KapatAsync(new OturumSonlandi(OturumSonuSebebi.Yasaklandi, duzenliSebep));
        }
        _gunlukcu.Bilgi($"Yonetici: {kullaniciId} yasaklandi. Sebep: {duzenliSebep ?? "-"}");
        return true;
    }

    public async Task<bool> YasagiKaldirAsync(int kullaniciId)
    {
        if (await _kullanicilar.IdIleBulAsync(kullaniciId) is null)
        {
            return false;
        }
        await _kullanicilar.YasakDurumunuYazAsync(kullaniciId, false, null, null);
        _gunlukcu.Bilgi($"Yonetici: {kullaniciId} yasagi kaldirildi.");
        return true;
    }

    public async Task<string> DavetKoduUretAsync()
    {
        while (true)
        {
            string kod = DavetKoduUreteci.Uret();
            if (await _davetler.EkleAsync(kod, _saat.SimdiUtc))
            {
                _gunlukcu.Bilgi("Yonetici: yeni davet kodu uretildi.");
                return kod;
            }
        }
    }

    // Numara (6 hane) ya da kullanici adiyla arar.
    public async Task<KullaniciKaydi?> KullaniciBulAsync(string numaraVeyaAd)
    {
        string aranan = numaraVeyaAd.Trim();
        return int.TryParse(aranan, out int id)
            ? await _kullanicilar.IdIleBulAsync(id)
            : await _kullanicilar.KullaniciAdiylaBulAsync(aranan.ToLowerInvariant());
    }

    public Task<IReadOnlyList<DavetKoduKaydi>> DavetKodlariAsync() => _davetler.HepsiniGetirAsync();

    public Task<IReadOnlyList<KullaniciKaydi>> YasaklilarAsync() => _kullanicilar.YasaklilariGetirAsync();

    // ---- Mesaj kayitlari (silinen ve gizlenen mesajlar dahil; salt okunur) ----
    public const int KayitSayfaBoyutu = 200;

    public Task<IReadOnlyList<YonetimSohbetOzeti>> KullaniciSohbetleriAsync(int kullaniciId) =>
        _mesajlar.YonetimSohbetleriAsync(kullaniciId);

    // Eskiden yeniye. oncekiMesajId verilirse ondan eski sayfa.
    public async Task<IReadOnlyList<YonetimMesajKaydi>> OzelKayitlarAsync(int kullaniciId, int karsiId, long? oncekiMesajId) =>
        [.. (await _mesajlar.YonetimOzelKayitlarAsync(kullaniciId, karsiId, oncekiMesajId, KayitSayfaBoyutu)).Reverse()];

    public async Task<IReadOnlyList<YonetimMesajKaydi>> GenelKayitlarAsync(long? oncekiMesajId) =>
        [.. (await _mesajlar.YonetimGenelKayitlarAsync(oncekiMesajId, KayitSayfaBoyutu)).Reverse()];

    public async Task<SunucuIstatistigi> IstatistikAsync() => new(
        await _kullanicilar.HesapSayisiAsync(),
        await _mesajlar.MesajSayisiAsync(),
        await _mesajlar.TabloBoyutuAsync());
}
