using ChatApp.Protocol.Kurallar;
using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;
using ChatApp.Server.Abstractions;
using ChatApp.Server.Data;
using ChatApp.Server.Security;
using ChatApp.Server.Sessions;

namespace ChatApp.Server.Services;

// Mesajlasma: gonderme, iletme, okundu bilgisi, gecmis, cevrimdisiyken biriken mesajlar, silme.
// Engelleme kurallari burada (sunucuda) uygulanir; istemci degistirilse bile asilamaz.
public sealed class MesajServisi
{
    public const int SaniyedeEnFazlaMesaj = 5;
    public const int SaniyedeEnFazlaSilme = 10;

    private readonly OturumYoneticisi _oturumlar;
    private readonly IKullaniciDeposu _kullanicilar;
    private readonly IMesajDeposu _mesajlar;
    private readonly IEngelDeposu _engeller;
    private readonly HizSinirlayici _hizSinirlayici;
    private readonly ISaat _saat;

    public MesajServisi(OturumYoneticisi oturumlar, IKullaniciDeposu kullanicilar, IMesajDeposu mesajlar,
        IEngelDeposu engeller, HizSinirlayici hizSinirlayici, ISaat saat)
    {
        ArgumentNullException.ThrowIfNull(oturumlar);
        ArgumentNullException.ThrowIfNull(kullanicilar);
        ArgumentNullException.ThrowIfNull(mesajlar);
        ArgumentNullException.ThrowIfNull(engeller);
        ArgumentNullException.ThrowIfNull(hizSinirlayici);
        ArgumentNullException.ThrowIfNull(saat);
        _oturumlar = oturumlar;
        _kullanicilar = kullanicilar;
        _mesajlar = mesajlar;
        _engeller = engeller;
        _hizSinirlayici = hizSinirlayici;
        _saat = saat;
    }

    public async Task GonderAsync(IIstemciOturumu gonderen, MesajGonder istek)
    {
        int gonderenId = gonderen.KullaniciId!.Value;

        HataKodu? hata = await ReddetmeSebebiAsync(gonderenId, istek);
        if (hata is not null)
        {
            await gonderen.GonderAsync(new MesajReddedildi(istek.IstemciMesajId, hata.Value));
            return;
        }

        DateTimeOffset zaman = _saat.SimdiUtc;
        (long mesajId, bool yeni) = await _mesajlar.KaydetAsync(gonderenId, istek.AliciId, istek.Metin, zaman, istek.IstemciMesajId);
        if (!yeni)
        {
            // Istemci baglanti kopunca ayni mesaji tekrar gonderdi: zaten kayitli. Sadece kabul bilgisi tekrar gider;
            // aliciya ikinci kez iletilmez (iletilmediyse bekleyen mesaj olarak zaten gidecek).
            if (await _mesajlar.MesajBulAsync(mesajId) is { } mevcut)
            {
                await gonderen.GonderAsync(new MesajKabulEdildi(istek.IstemciMesajId, mevcut.MesajBilgisineCevir()));
            }
            return;
        }

        var mesaj = new MesajBilgisi(mesajId, gonderenId, gonderen.GorunenAd!, istek.AliciId, istek.Metin,
            zaman, MesajDurumu.Gonderildi);
        await gonderen.GonderAsync(new MesajKabulEdildi(istek.IstemciMesajId, mesaj));   // tek tik

        if (istek.AliciId is int aliciId)
        {
            await OzelMesajiIletAsync(gonderen, aliciId, mesaj);
        }
        else
        {
            await GenelOdayaDagitAsync(gonderenId, mesaj);
        }
    }

    // Giris yapan kisiye, cevrimdisiyken gelen mesajlari iletir.
    public async Task BekleyenleriIletAsync(IIstemciOturumu oturum)
    {
        int kullaniciId = oturum.KullaniciId!.Value;
        IReadOnlySet<int> engellediklerim = await _engeller.EngellediklerimAsync(kullaniciId);

        foreach (KayitliMesaj mesaj in await _mesajlar.IletilmemisleriGetirAsync(kullaniciId))
        {
            if (engellediklerim.Contains(mesaj.GonderenId))
            {
                continue;   // engelledigim kisinin mesaji bana iletilmez
            }

            await oturum.GonderAsync(new MesajGeldi(mesaj.MesajBilgisineCevir()));
            await _mesajlar.IletildiIsaretleAsync(mesaj.Id, _saat.SimdiUtc);
            if (_oturumlar.Bul(mesaj.GonderenId) is { } gonderenOturumu)
            {
                await gonderenOturumu.GonderAsync(new MesajDurumuDegisti(mesaj.Id, MesajDurumu.Iletildi));
            }
        }
    }

    public async Task OkunduAsync(IIstemciOturumu okuyan, OkunduBildir istek)
    {
        int okuyanId = okuyan.KullaniciId!.Value;
        long? sonOkunan = await _mesajlar.OkunduIsaretleAsync(okuyanId, istek.KarsiId, istek.SonMesajId, _saat.SimdiUtc);

        // Engelledigim kisiye "okundu" bilgisi gitmez.
        if (sonOkunan is long son
            && _oturumlar.Bul(istek.KarsiId) is { } gonderenOturumu
            && !await _engeller.EngelliMiAsync(okuyanId, istek.KarsiId))
        {
            await gonderenOturumu.GonderAsync(new MesajlarOkundu(okuyanId, son));
        }
    }

    public async Task<GecmisCevabi> GecmisAsync(int kullaniciId, GecmisIstegi istek)
    {
        int adet = ProtokolSinirlari.GecmisSayfaBoyutu;

        // Bir fazlasi istenir: gelirse "daha eski mesaj var" demektir.
        IReadOnlyList<KayitliMesaj> mesajlar = istek.KarsiId is int karsiId
            ? await _mesajlar.OzelGecmisAsync(kullaniciId, karsiId, istek.OncekiMesajId, adet + 1)
            : await _mesajlar.GenelGecmisAsync(kullaniciId, istek.OncekiMesajId, adet + 1);

        bool dahaVar = mesajlar.Count > adet;
        IEnumerable<KayitliMesaj> sayfa = mesajlar.Take(adet);

        if (istek.KarsiId is null)
        {
            // Genel odada engelledigim kisilerin mesajlari bana gosterilmez.
            IReadOnlySet<int> engellediklerim = await _engeller.EngellediklerimAsync(kullaniciId);
            sayfa = sayfa.Where(m => !engellediklerim.Contains(m.GonderenId));
        }

        // Veritabani yeniden eskiye verir; ekran eskiden yeniye gosterir.
        var sirali = sayfa.Reverse().Select(m => m.MesajBilgisineCevir()).ToList();
        return new GecmisCevabi(istek.KarsiId, sirali, dahaVar);
    }

    // "Benden sil" ya da "Herkesten sil". Mesaj veritabaninda kalir; sadece isaretlenir.
    public async Task SilAsync(IIstemciOturumu oturum, MesajSilIstegi istek)
    {
        int kullaniciId = oturum.KullaniciId!.Value;
        HataKodu? hata = await SilmeHatasiAsync(kullaniciId, istek);
        if (hata is not null)
        {
            await oturum.GonderAsync(new IslemSonucu(IslemTuru.MesajSil, false, hata));
            return;
        }

        if (!istek.Herkesten)
        {
            await _mesajlar.GizleAsync(kullaniciId, istek.MesajId, _saat.SimdiUtc);
            await oturum.GonderAsync(new IslemSonucu(IslemTuru.MesajSil, true, null));
            return;
        }

        KayitliMesaj mesaj = (await _mesajlar.MesajBulAsync(istek.MesajId))!;
        await _mesajlar.HerkestenSilAsync(istek.MesajId, _saat.SimdiUtc);
        await oturum.GonderAsync(new IslemSonucu(IslemTuru.MesajSil, true, null));

        // Mesaji gormus olabilecek herkese: ozelde karsi taraf, Genel'de odadaki herkes.
        var paket = new MesajHerkestenSilindi(istek.MesajId);
        var alicilar = new List<IIstemciOturumu>();
        if (mesaj.AliciId is int aliciId)
        {
            if (_oturumlar.Bul(aliciId) is { } alici)
            {
                alicilar.Add(alici);
            }
        }
        else
        {
            alicilar.AddRange(_oturumlar.GirisYapanlar().Where(o => o.KullaniciId != kullaniciId));
        }
        foreach (IIstemciOturumu alici in alicilar)
        {
            await alici.GonderAsync(paket);
        }
    }

    // Bu kisiyle olan sohbeti sadece benden gizler.
    public async Task<IslemSonucu> SohbetSilAsync(int kullaniciId, SohbetSilIstegi istek)
    {
        if (istek.KarsiId == kullaniciId || await _kullanicilar.IdIleBulAsync(istek.KarsiId) is null)
        {
            return new IslemSonucu(IslemTuru.SohbetSil, false, HataKodu.KullaniciBulunamadi);
        }
        await _mesajlar.SohbetiSilAsync(kullaniciId, istek.KarsiId, _saat.SimdiUtc);
        return new IslemSonucu(IslemTuru.SohbetSil, true, null);
    }

    private async Task<HataKodu?> SilmeHatasiAsync(int kullaniciId, MesajSilIstegi istek)
    {
        if (!_hizSinirlayici.IzinVer($"sil:{kullaniciId}", SaniyedeEnFazlaSilme, TimeSpan.FromSeconds(1)))
        {
            return HataKodu.HizSiniriAsildi;
        }

        // Sadece gorebilecegi mesaji silebilir: Genel oda mesaji ya da tarafi oldugu ozel mesaj.
        KayitliMesaj? mesaj = await _mesajlar.MesajBulAsync(istek.MesajId);
        bool gorebilir = mesaj is not null
            && (mesaj.AliciId is null || mesaj.GonderenId == kullaniciId || mesaj.AliciId == kullaniciId);
        if (!gorebilir)
        {
            return HataKodu.MesajBulunamadi;
        }
        if (istek.Herkesten && mesaj!.GonderenId != kullaniciId)
        {
            return HataKodu.SadeceKendiMesajin;
        }
        return null;
    }

    private async Task<HataKodu?> ReddetmeSebebiAsync(int gonderenId, MesajGonder istek)
    {
        if (!_hizSinirlayici.IzinVer($"mesaj:{gonderenId}", SaniyedeEnFazlaMesaj, TimeSpan.FromSeconds(1)))
        {
            return HataKodu.HizSiniriAsildi;
        }
        if (!GirdiKurallari.MesajGecerliMi(istek.Metin))
        {
            return HataKodu.MesajGecersiz;
        }
        if (istek.AliciId is not int aliciId)
        {
            return null;   // Genel oda
        }
        if (aliciId == gonderenId)
        {
            return HataKodu.KendineMesaj;
        }
        if (await _kullanicilar.IdIleBulAsync(aliciId) is null)
        {
            return HataKodu.KullaniciBulunamadi;
        }
        if (await _engeller.EngelliMiAsync(gonderenId, aliciId))
        {
            return HataKodu.EngelledigimKisi;   // once engeli kaldirmasi gerekir
        }
        return null;
    }

    private async Task OzelMesajiIletAsync(IIstemciOturumu gonderen, int aliciId, MesajBilgisi mesaj)
    {
        // Alici beni engellediyse mesaj ona iletilmez; ben bunu anlamam (tek tik kalir).
        if (await _engeller.EngelliMiAsync(aliciId, mesaj.GonderenId))
        {
            return;
        }

        IIstemciOturumu? alici = _oturumlar.Bul(aliciId);
        if (alici is null)
        {
            return;   // cevrimdisi: girince BekleyenleriIletAsync ile iletilir
        }

        await alici.GonderAsync(new MesajGeldi(mesaj));
        await _mesajlar.IletildiIsaretleAsync(mesaj.Id, _saat.SimdiUtc);
        await gonderen.GonderAsync(new MesajDurumuDegisti(mesaj.Id, MesajDurumu.Iletildi));   // cift tik
    }

    private async Task GenelOdayaDagitAsync(int gonderenId, MesajBilgisi mesaj)
    {
        IReadOnlySet<int> beniEngelleyenler = await _engeller.BeniEngelleyenlerAsync(gonderenId);
        var paket = new MesajGeldi(mesaj);

        foreach (IIstemciOturumu oturum in _oturumlar.GirisYapanlar())
        {
            if (oturum.KullaniciId is int id && id != gonderenId && !beniEngelleyenler.Contains(id))
            {
                await oturum.GonderAsync(paket);
            }
        }
    }
}
