using System.Security.Cryptography;
using ChatApp.Protocol.Kurallar;
using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;
using ChatApp.Server.Abstractions;
using ChatApp.Server.Data;
using ChatApp.Server.Security;

namespace ChatApp.Server.Services;

// Hesaplarla ilgili isler: kayit, giris, sifre degistirme.
// Butun kurallar BURADA kontrol edilir; istemcinin yaptigi kontrollere guvenilmez.
public sealed class HesapServisi
{
    private const int IdDenemeSayisi = 20;

    private readonly IKullaniciDeposu _kullanicilar;
    private readonly SifreHashleyici _hashleyici;
    private readonly GirisKilidi _girisKilidi;
    private readonly ISaat _saat;
    private readonly IGunlukcu _gunlukcu;

    public HesapServisi(IKullaniciDeposu kullanicilar, SifreHashleyici hashleyici, GirisKilidi girisKilidi,
        ISaat saat, IGunlukcu gunlukcu)
    {
        ArgumentNullException.ThrowIfNull(kullanicilar);
        ArgumentNullException.ThrowIfNull(hashleyici);
        ArgumentNullException.ThrowIfNull(girisKilidi);
        ArgumentNullException.ThrowIfNull(saat);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        _kullanicilar = kullanicilar;
        _hashleyici = hashleyici;
        _girisKilidi = girisKilidi;
        _saat = saat;
        _gunlukcu = gunlukcu;
    }

    public async Task<KayitSonucu> KayitOlAsync(KayitIstegi istek, string ipAdresi)
    {
        if (!GirdiKurallari.KullaniciAdiGecerliMi(istek.KullaniciAdi))
        {
            return Basarisiz(HataKodu.KullaniciAdiGecersiz);
        }
        string? gorunenAd = GirdiKurallari.GorunenAdiDuzenle(istek.GorunenAd);
        if (gorunenAd is null)
        {
            return Basarisiz(HataKodu.GorunenAdGecersiz);
        }
        if (!GirdiKurallari.SifreGecerliMi(istek.Sifre))
        {
            return Basarisiz(HataKodu.SifreGecersiz);
        }
        string? davetKodu = GirdiKurallari.DavetKodunuDuzenle(istek.DavetKodu);
        if (davetKodu is null)
        {
            return Basarisiz(HataKodu.DavetKoduGecersiz);
        }

        (byte[] hash, byte[] tuz) = _hashleyici.Hashle(istek.Sifre);

        // 6 haneli numara rastgele secilir; doluysa yenisi denenir.
        for (int deneme = 0; deneme < IdDenemeSayisi; deneme++)
        {
            int id = RandomNumberGenerator.GetInt32(100_000, 1_000_000);
            var yeni = new YeniKullanici(id, istek.KullaniciAdi, gorunenAd, hash, tuz, _saat.SimdiUtc);

            switch (await _kullanicilar.OlusturAsync(yeni, davetKodu))
            {
                case KayitDurumu.Basarili:
                    _gunlukcu.Bilgi($"Yeni hesap: {istek.KullaniciAdi} ({id}), IP {ipAdresi}");
                    return new KayitSonucu(true, id, null);
                case KayitDurumu.KullaniciAdiAlinmis:
                    return Basarisiz(HataKodu.KullaniciAdiAlinmis);
                case KayitDurumu.DavetKoduGecersiz:
                    _gunlukcu.Uyari($"Gecersiz davet koduyla kayit denemesi, IP {ipAdresi}");
                    return Basarisiz(HataKodu.DavetKoduGecersiz);
                case KayitDurumu.IdCakisti:
                    continue;
            }
        }

        // 900.000 numaradan 20'si ust uste dolu cikti: neredeyse imkansiz, olursa bu bir bug isaretidir.
        throw new InvalidOperationException("Bos hesap numarasi bulunamadi.");
    }

    // Basarili giriste hesap kaydi da doner (oturumu baglamak icin).
    public async Task<(GirisSonucu Sonuc, KullaniciKaydi? Kullanici)> GirisYapAsync(GirisIstegi istek, string ipAdresi)
    {
        string kullaniciAdi = istek.KullaniciAdi ?? string.Empty;

        if (_girisKilidi.KilitliMi(kullaniciAdi))
        {
            return (new GirisSonucu(false, null, null, HataKodu.HesapGeciciKilitli, null), null);
        }

        KullaniciKaydi? kullanici = GirdiKurallari.KullaniciAdiGecerliMi(kullaniciAdi)
            ? await _kullanicilar.KullaniciAdiylaBulAsync(kullaniciAdi)
            : null;

        bool sifreDogru;
        if (kullanici is null)
        {
            _hashleyici.BosunaHesapla(istek.Sifre ?? string.Empty);   // "bu ad var mi" sure farkindan anlasilmasin
            sifreDogru = false;
        }
        else
        {
            sifreDogru = _hashleyici.DogruMu(istek.Sifre ?? string.Empty, kullanici.SifreHash, kullanici.Tuz);
        }

        if (!sifreDogru)
        {
            _girisKilidi.BasarisizDeneme(kullaniciAdi);
            _gunlukcu.Uyari($"Basarisiz giris: {kullaniciAdi}, IP {ipAdresi}");
            // "Kullanici yok" ile "sifre yanlis" ayni cevabi alir: hangi adlarin var oldugu anlasilmasin.
            return (new GirisSonucu(false, null, null, HataKodu.KullaniciAdiVeyaSifreYanlis, null), null);
        }

        _girisKilidi.BasariliGiris(kullaniciAdi);

        if (kullanici!.Yasakli)
        {
            _gunlukcu.Uyari($"Yasakli hesap giris denedi: {kullaniciAdi} ({kullanici.Id}), IP {ipAdresi}");
            return (new GirisSonucu(false, null, null, HataKodu.HesapYasakli, kullanici.YasakSebebi), null);
        }

        await _kullanicilar.SonGirisiYazAsync(kullanici.Id, _saat.SimdiUtc);
        _gunlukcu.Bilgi($"Giris: {kullaniciAdi} ({kullanici.Id}), IP {ipAdresi}");
        return (new GirisSonucu(true, kullanici.Id, kullanici.GorunenAd, null, null), kullanici);
    }

    public async Task<IslemSonucu> SifreDegistirAsync(int kullaniciId, SifreDegistirIstegi istek)
    {
        if (!GirdiKurallari.SifreGecerliMi(istek.YeniSifre))
        {
            return new IslemSonucu(IslemTuru.SifreDegistir, false, HataKodu.SifreGecersiz);
        }

        KullaniciKaydi? kullanici = await _kullanicilar.IdIleBulAsync(kullaniciId);
        if (kullanici is null || !_hashleyici.DogruMu(istek.EskiSifre, kullanici.SifreHash, kullanici.Tuz))
        {
            return new IslemSonucu(IslemTuru.SifreDegistir, false, HataKodu.EskiSifreYanlis);
        }

        (byte[] hash, byte[] tuz) = _hashleyici.Hashle(istek.YeniSifre);
        await _kullanicilar.SifreyiGuncelleAsync(kullaniciId, hash, tuz);
        _gunlukcu.Bilgi($"Sifre degistirildi: {kullanici.KullaniciAdi} ({kullaniciId})");
        return new IslemSonucu(IslemTuru.SifreDegistir, true, null);
    }

    private static KayitSonucu Basarisiz(HataKodu kod) => new(false, null, kod);
}
