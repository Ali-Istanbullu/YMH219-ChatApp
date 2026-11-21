using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;
using ChatApp.Server.Data;
using ChatApp.UnitTests.Fakes;

namespace ChatApp.UnitTests.Services;

[TestClass]
public sealed class HesapServisiTestleri
{
    private readonly TestOrtami _o = new();

    private async Task<KayitSonucu> KayitAsync(string ad, string? kod = null, string sifre = TestOrtami.Sifre) =>
        await _o.Hesaplar.KayitOlAsync(new KayitIstegi(ad, "Gorunen Ad", sifre, kod ?? await _o.Yonetim.DavetKoduUretAsync()), "10.0.0.1");

    [TestMethod]
    public async Task Kayit_AltiHaneliNumaraVerir_DavetKodunuHarcar()
    {
        string kod = await _o.Yonetim.DavetKoduUretAsync();
        KayitSonucu sonuc = await KayitAsync("ali", kod);

        Assert.IsTrue(sonuc.Basarili);
        Assert.IsTrue(sonuc.KullaniciId is >= 100_000 and <= 999_999);
        DavetKoduKaydi davet = (await _o.Davetler.HepsiniGetirAsync()).Single();
        Assert.AreEqual(sonuc.KullaniciId, davet.KullananId);
    }

    [TestMethod]
    public async Task Kayit_SifreAcikSaklanmaz()
    {
        KayitSonucu sonuc = await KayitAsync("ali");
        KullaniciKaydi kayit = (await _o.Kullanicilar.IdIleBulAsync(sonuc.KullaniciId!.Value))!;
        Assert.HasCount(32, kayit.SifreHash);
        Assert.AreNotEqual(TestOrtami.Sifre, System.Text.Encoding.UTF8.GetString(kayit.SifreHash));
    }

    [TestMethod]
    public async Task Kayit_KullanilmisDavetKoduReddedilir()
    {
        string kod = await _o.Yonetim.DavetKoduUretAsync();
        await KayitAsync("ali", kod);

        KayitSonucu ikinci = await KayitAsync("veli", kod);
        Assert.AreEqual(HataKodu.DavetKoduGecersiz, ikinci.Hata);
    }

    [TestMethod]
    public async Task Kayit_UydurmaDavetKoduReddedilir() =>
        Assert.AreEqual(HataKodu.DavetKoduGecersiz, (await KayitAsync("ali", "ABCD-EFGH")).Hata);

    [TestMethod]
    public async Task Kayit_AlinmisKullaniciAdi_KoduHarcamaz()
    {
        await KayitAsync("ali");
        string kod = await _o.Yonetim.DavetKoduUretAsync();

        Assert.AreEqual(HataKodu.KullaniciAdiAlinmis, (await KayitAsync("ali", kod)).Hata);
        Assert.IsTrue((await KayitAsync("veli", kod)).Basarili);   // kod hala gecerli
    }

    [TestMethod]
    [DataRow("A", HataKodu.KullaniciAdiGecersiz)]
    [DataRow("ali veli", HataKodu.KullaniciAdiGecersiz)]
    public async Task Kayit_GecersizKullaniciAdi(string ad, HataKodu beklenen) =>
        Assert.AreEqual(beklenen, (await KayitAsync(ad)).Hata);

    [TestMethod]
    public async Task Kayit_KisaSifreReddedilir() =>
        Assert.AreEqual(HataKodu.SifreGecersiz, (await KayitAsync("ali", sifre: "1234567")).Hata);

    [TestMethod]
    public async Task Giris_DogruSifreyleBasarili()
    {
        int id = await _o.HesapAcAsync("ali", "Ali");
        (GirisSonucu sonuc, KullaniciKaydi? kayit) = await _o.Hesaplar.GirisYapAsync(new GirisIstegi("ali", TestOrtami.Sifre), "1.1.1.1");

        Assert.IsTrue(sonuc.Basarili);
        Assert.AreEqual(id, sonuc.KullaniciId);
        Assert.AreEqual("Ali", sonuc.GorunenAd);
        Assert.IsNotNull(kayit);
    }

    [TestMethod]
    public async Task Giris_YanlisSifreIleOlmayanKullaniciAyniCevabiAlir()
    {
        await _o.HesapAcAsync("ali", "Ali");
        (GirisSonucu yanlisSifre, _) = await _o.Hesaplar.GirisYapAsync(new GirisIstegi("ali", "yanlis-sifre"), "1.1.1.1");
        (GirisSonucu olmayan, _) = await _o.Hesaplar.GirisYapAsync(new GirisIstegi("boyle.biri.yok", "yanlis-sifre"), "1.1.1.1");

        // Hangi kullanici adlarinin var oldugu cevaptan anlasilmamali.
        Assert.AreEqual(HataKodu.KullaniciAdiVeyaSifreYanlis, yanlisSifre.Hata);
        Assert.AreEqual(yanlisSifre, olmayan);
    }

    [TestMethod]
    public async Task Giris_BesYanlistanSonraDogruSifreDeKilitliyeTakilir()
    {
        await _o.HesapAcAsync("ali", "Ali");
        for (int i = 0; i < 5; i++)
        {
            await _o.Hesaplar.GirisYapAsync(new GirisIstegi("ali", "yanlis-sifre"), "1.1.1.1");
        }

        (GirisSonucu sonuc, _) = await _o.Hesaplar.GirisYapAsync(new GirisIstegi("ali", TestOrtami.Sifre), "1.1.1.1");
        Assert.AreEqual(HataKodu.HesapGeciciKilitli, sonuc.Hata);

        _o.Saat.Ilerlet(TimeSpan.FromMinutes(1));
        (sonuc, _) = await _o.Hesaplar.GirisYapAsync(new GirisIstegi("ali", TestOrtami.Sifre), "1.1.1.1");
        Assert.IsTrue(sonuc.Basarili);
    }

    [TestMethod]
    public async Task Giris_YasakliHesap_SebebiyleReddedilir()
    {
        int id = await _o.HesapAcAsync("ali", "Ali");
        await _o.Yonetim.YasaklaAsync(id, "spam");

        (GirisSonucu sonuc, KullaniciKaydi? kayit) = await _o.Hesaplar.GirisYapAsync(new GirisIstegi("ali", TestOrtami.Sifre), "1.1.1.1");
        Assert.AreEqual(HataKodu.HesapYasakli, sonuc.Hata);
        Assert.AreEqual("spam", sonuc.Aciklama);
        Assert.IsNull(kayit);
    }

    [TestMethod]
    public async Task SifreDegistir_EskiSifreDogruysaYeniSifreGecerliOlur()
    {
        int id = await _o.HesapAcAsync("ali", "Ali");

        IslemSonucu yanlis = await _o.Hesaplar.SifreDegistirAsync(id, new SifreDegistirIstegi("yanlis-eski", "yepyeni-sifre"));
        Assert.AreEqual(HataKodu.EskiSifreYanlis, yanlis.Hata);

        IslemSonucu dogru = await _o.Hesaplar.SifreDegistirAsync(id, new SifreDegistirIstegi(TestOrtami.Sifre, "yepyeni-sifre"));
        Assert.IsTrue(dogru.Basarili);

        (GirisSonucu eskiyle, _) = await _o.Hesaplar.GirisYapAsync(new GirisIstegi("ali", TestOrtami.Sifre), "1.1.1.1");
        (GirisSonucu yeniyle, _) = await _o.Hesaplar.GirisYapAsync(new GirisIstegi("ali", "yepyeni-sifre"), "1.1.1.1");
        Assert.IsFalse(eskiyle.Basarili);
        Assert.IsTrue(yeniyle.Basarili);
    }
}
