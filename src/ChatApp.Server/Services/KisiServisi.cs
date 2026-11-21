using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;
using ChatApp.Server.Abstractions;
using ChatApp.Server.Data;
using ChatApp.Server.Sessions;

namespace ChatApp.Server.Services;

// Kisilerle ilgili isler: sohbet listesi, ID ile kisi sorgulama, engelleme, cevrimici durumu.
//
// Cevrimici kurali: X, Y'yi cevrimici gorur  <=>  Y cevrimici VE Y, X'i engellememis.
// (Engellenen kisi, engelleyeni hep cevrimdisi gorur; engellendigini anlamaz.)
public sealed class KisiServisi
{
    private readonly OturumYoneticisi _oturumlar;
    private readonly IKullaniciDeposu _kullanicilar;
    private readonly IMesajDeposu _mesajlar;
    private readonly IEngelDeposu _engeller;
    private readonly ISaat _saat;
    private readonly IGunlukcu _gunlukcu;

    public KisiServisi(OturumYoneticisi oturumlar, IKullaniciDeposu kullanicilar, IMesajDeposu mesajlar,
        IEngelDeposu engeller, ISaat saat, IGunlukcu gunlukcu)
    {
        ArgumentNullException.ThrowIfNull(oturumlar);
        ArgumentNullException.ThrowIfNull(kullanicilar);
        ArgumentNullException.ThrowIfNull(mesajlar);
        ArgumentNullException.ThrowIfNull(engeller);
        ArgumentNullException.ThrowIfNull(saat);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        _oturumlar = oturumlar;
        _kullanicilar = kullanicilar;
        _mesajlar = mesajlar;
        _engeller = engeller;
        _saat = saat;
        _gunlukcu = gunlukcu;
    }

    public async Task<SohbetListesi> SohbetListesiAsync(int kullaniciId)
    {
        IReadOnlyList<SohbetOzeti> sohbetler = await _mesajlar.SohbetleriGetirAsync(kullaniciId);
        IReadOnlySet<int> engellediklerim = await _engeller.EngellediklerimAsync(kullaniciId);
        IReadOnlySet<int> beniEngelleyenler = await _engeller.BeniEngelleyenlerAsync(kullaniciId);

        var kisiler = sohbetler.Select(s => new KisiBilgisi(
            s.KarsiId,
            s.KarsiAd,
            _oturumlar.CevrimiciMi(s.KarsiId) && !beniEngelleyenler.Contains(s.KarsiId),
            s.Okunmamis,
            engellediklerim.Contains(s.KarsiId))).ToList();
        return new SohbetListesi(kisiler);
    }

    public async Task<KullaniciBilgisi> KullaniciSorgulaAsync(int soranId, int arananId)
    {
        KullaniciKaydi? kullanici = arananId == soranId ? null : await _kullanicilar.IdIleBulAsync(arananId);
        if (kullanici is null)
        {
            return new KullaniciBilgisi(arananId, false, null, false);
        }

        bool cevrimici = _oturumlar.CevrimiciMi(arananId) && !await _engeller.EngelliMiAsync(arananId, soranId);
        return new KullaniciBilgisi(arananId, true, kullanici.GorunenAd, cevrimici);
    }

    public async Task<IslemSonucu> EngelleAsync(int kullaniciId, EngelleIstegi istek)
    {
        IslemTuru islem = istek.Engelle ? IslemTuru.Engelle : IslemTuru.EngelKaldir;
        if (istek.KullaniciId == kullaniciId || await _kullanicilar.IdIleBulAsync(istek.KullaniciId) is null)
        {
            return new IslemSonucu(islem, false, HataKodu.KullaniciBulunamadi);
        }

        if (istek.Engelle)
        {
            await _engeller.EngelleAsync(kullaniciId, istek.KullaniciId, _saat.SimdiUtc);
        }
        else
        {
            await _engeller.EngeliKaldirAsync(kullaniciId, istek.KullaniciId);
        }
        _gunlukcu.Bilgi($"{kullaniciId} -> {istek.KullaniciId}: {(istek.Engelle ? "engellendi" : "engel kaldirildi")}");

        // Engellenen kisi beni artik cevrimdisi gorur; engel kalkinca tekrar cevrimici.
        if (_oturumlar.Bul(istek.KullaniciId) is { } karsiOturum && _oturumlar.CevrimiciMi(kullaniciId))
        {
            await karsiOturum.GonderAsync(new CevrimiciDurumu(kullaniciId, !istek.Engelle));
        }
        return new IslemSonucu(islem, true, null);
    }

    // Bu kullanici girdi/cikti: konustugu kisilerden cevrimici olanlara haber ver
    // (engelledigi kisiler haric; onlar onu hep cevrimdisi gorur).
    public async Task CevrimiciDurumunuBildirAsync(int kullaniciId, bool cevrimici)
    {
        IReadOnlyList<SohbetOzeti> sohbetler = await _mesajlar.SohbetleriGetirAsync(kullaniciId);
        IReadOnlySet<int> engellediklerim = await _engeller.EngellediklerimAsync(kullaniciId);
        var paket = new CevrimiciDurumu(kullaniciId, cevrimici);

        foreach (SohbetOzeti sohbet in sohbetler.Where(s => !engellediklerim.Contains(s.KarsiId)))
        {
            if (_oturumlar.Bul(sohbet.KarsiId) is { } oturum)
            {
                await oturum.GonderAsync(paket);
            }
        }
    }
}
