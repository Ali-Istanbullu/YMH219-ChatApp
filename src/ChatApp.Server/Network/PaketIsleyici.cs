using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;
using ChatApp.Server.Abstractions;
using ChatApp.Server.Data;
using ChatApp.Server.Security;
using ChatApp.Server.Services;
using ChatApp.Server.Sessions;

namespace ChatApp.Server.Network;

// Gelen her paketi ilgili servise yonlendirir.
// Giris yapmadan once SADECE Kayit ve Giris paketi kabul edilir; baska bir sey gelirse baglanti kesilir.
public sealed class PaketIsleyici
{
    public const int SaatteEnFazlaKayit = 5;      // ayni IP'den
    public const int DakikadaEnFazlaGiris = 10;   // ayni IP'den

    private readonly OturumYoneticisi _oturumlar;
    private readonly HesapServisi _hesaplar;
    private readonly MesajServisi _mesajlar;
    private readonly KisiServisi _kisiler;
    private readonly HizSinirlayici _hizSinirlayici;
    private readonly IGunlukcu _gunlukcu;

    public PaketIsleyici(OturumYoneticisi oturumlar, HesapServisi hesaplar, MesajServisi mesajlar,
        KisiServisi kisiler, HizSinirlayici hizSinirlayici, IGunlukcu gunlukcu)
    {
        ArgumentNullException.ThrowIfNull(oturumlar);
        ArgumentNullException.ThrowIfNull(hesaplar);
        ArgumentNullException.ThrowIfNull(mesajlar);
        ArgumentNullException.ThrowIfNull(kisiler);
        ArgumentNullException.ThrowIfNull(hizSinirlayici);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        _oturumlar = oturumlar;
        _hesaplar = hesaplar;
        _mesajlar = mesajlar;
        _kisiler = kisiler;
        _hizSinirlayici = hizSinirlayici;
        _gunlukcu = gunlukcu;
    }

    // Baglanti acik kalmali mi? false: baglanti kesilir.
    public async Task<bool> IsleAsync(IIstemciOturumu oturum, Paket paket)
    {
        try
        {
            return oturum.KullaniciId is int kullaniciId
                ? await GirisSonrasiIsleAsync(oturum, kullaniciId, paket)
                : await GirisOncesiIsleAsync(oturum, paket);
        }
        catch (DepoHatasi hata)
        {
            // Veritabanina ulasilamadi: istemciye bildir, baglanti acik kalsin (gecici olabilir).
            _gunlukcu.Hata(hata, "Veritabani hatasi");
            await oturum.GonderAsync(new HataBildirimi(HataKodu.SunucuHatasi,
                "Sunucuda gecici bir sorun var, lutfen biraz sonra tekrar deneyin."));
            return true;
        }
    }

    // Baglanti kapandi: cevrimdisi olduysa konustugu kisilere haber ver.
    public async Task BaglantiKapandiAsync(IIstemciOturumu oturum)
    {
        if (_oturumlar.BaglantiCikar(oturum) && oturum.KullaniciId is int kullaniciId)
        {
            try
            {
                await _kisiler.CevrimiciDurumunuBildirAsync(kullaniciId, false);
            }
            catch (DepoHatasi hata)
            {
                _gunlukcu.Hata(hata, "Cevrimdisi bilgisi gonderilemedi (veritabani hatasi)");
            }
        }
    }

    private async Task<bool> GirisOncesiIsleAsync(IIstemciOturumu oturum, Paket paket)
    {
        switch (paket)
        {
            case KayitIstegi kayit:
                if (!_hizSinirlayici.IzinVer($"kayit:{oturum.IpAdresi}", SaatteEnFazlaKayit, TimeSpan.FromHours(1)))
                {
                    return await HizSiniriylaKapatAsync(oturum, "kayit");
                }
                await oturum.GonderAsync(await _hesaplar.KayitOlAsync(kayit, oturum.IpAdresi));
                return true;

            case GirisIstegi giris:
                if (!_hizSinirlayici.IzinVer($"giris:{oturum.IpAdresi}", DakikadaEnFazlaGiris, TimeSpan.FromMinutes(1)))
                {
                    return await HizSiniriylaKapatAsync(oturum, "giris");
                }
                return await GirisYapAsync(oturum, giris);

            default:
                await oturum.GonderAsync(new HataBildirimi(HataKodu.GirisGerekli, "Once giris yapmalisiniz."));
                return false;
        }
    }

    private async Task<bool> GirisYapAsync(IIstemciOturumu oturum, GirisIstegi giris)
    {
        (GirisSonucu sonuc, KullaniciKaydi? kullanici) = await _hesaplar.GirisYapAsync(giris, oturum.IpAdresi);
        if (kullanici is null)
        {
            await oturum.GonderAsync(sonuc);
            return true;   // tekrar deneyebilir (deneme sayisi GirisKilidi ve hiz siniriyla sinirli)
        }

        oturum.GirisYapildi(kullanici.Id, kullanici.GorunenAd);
        IIstemciOturumu? eskiOturum = _oturumlar.GirisYap(kullanici.Id, oturum);
        if (eskiOturum is not null)
        {
            await eskiOturum.KapatAsync(new OturumSonlandi(OturumSonuSebebi.BaskaYerdenGiris, null));
        }

        await oturum.GonderAsync(sonuc);
        await oturum.GonderAsync(await _kisiler.SohbetListesiAsync(kullanici.Id));
        await _mesajlar.BekleyenleriIletAsync(oturum);
        if (eskiOturum is null)
        {
            await _kisiler.CevrimiciDurumunuBildirAsync(kullanici.Id, true);
        }
        return true;
    }

    private async Task<bool> GirisSonrasiIsleAsync(IIstemciOturumu oturum, int kullaniciId, Paket paket)
    {
        switch (paket)
        {
            case MesajGonder mesaj:
                await _mesajlar.GonderAsync(oturum, mesaj);
                return true;
            case OkunduBildir okundu:
                await _mesajlar.OkunduAsync(oturum, okundu);
                return true;
            case GecmisIstegi gecmis:
                await oturum.GonderAsync(await _mesajlar.GecmisAsync(kullaniciId, gecmis));
                return true;
            case KullaniciSorgula sorgu:
                await oturum.GonderAsync(await _kisiler.KullaniciSorgulaAsync(kullaniciId, sorgu.KullaniciId));
                return true;
            case EngelleIstegi engel:
                await oturum.GonderAsync(await _kisiler.EngelleAsync(kullaniciId, engel));
                return true;
            case SifreDegistirIstegi sifre:
                await oturum.GonderAsync(await _hesaplar.SifreDegistirAsync(kullaniciId, sifre));
                return true;
            case KayitIstegi or GirisIstegi:
                await oturum.GonderAsync(new HataBildirimi(HataKodu.GecersizIstek, "Zaten giris yaptiniz."));
                return true;
            default:
                // Istemcinin gondermemesi gereken bir paket (ornegin sunucu paketi): bozuk ya da kotu niyetli istemci.
                _gunlukcu.Uyari($"Beklenmeyen paket ({paket.GetType().Name}), baglanti kesildi: {kullaniciId}");
                return false;
        }
    }

    private async Task<bool> HizSiniriylaKapatAsync(IIstemciOturumu oturum, string islem)
    {
        _gunlukcu.Uyari($"Hiz siniri asildi ({islem}), baglanti kesildi: IP {oturum.IpAdresi}");
        await oturum.GonderAsync(new HataBildirimi(HataKodu.HizSiniriAsildi,
            "Cok fazla deneme yapildi. Lutfen biraz bekleyip tekrar deneyin."));
        return false;
    }
}
