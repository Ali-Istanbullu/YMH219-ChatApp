using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;
using ChatApp.UnitTests.Fakes;

namespace ChatApp.UnitTests.Services;

[TestClass]
public sealed class MesajServisiTestleri
{
    private readonly TestOrtami _o = new();
    private int _aliId;
    private int _ayseId;
    private int _veliId;

    [TestInitialize]
    public async Task Hazirla()
    {
        _aliId = await _o.HesapAcAsync("ali", "Ali");
        _ayseId = await _o.HesapAcAsync("ayse", "Ayse");
        _veliId = await _o.HesapAcAsync("veli", "Veli");
    }

    [TestMethod]
    public async Task OzelMesaj_CevrimiciAliciya_HemenIletilir_GonderenCiftTikAlir()
    {
        SahteOturum ali = await _o.GirisYapAsync("ali");
        SahteOturum ayse = await _o.GirisYapAsync("ayse");
        ali.Temizle();

        await _o.MesajAtAsync(ali, _ayseId, "selam");

        MesajGeldi gelen = ayse.Paketler<MesajGeldi>().Single();
        Assert.AreEqual("selam", gelen.Mesaj.Metin);
        Assert.AreEqual(_aliId, gelen.Mesaj.GonderenId);
        Assert.AreEqual("Ali", gelen.Mesaj.GonderenAd);

        Assert.HasCount(1, ali.Paketler<MesajKabulEdildi>());                                     // tek tik
        Assert.AreEqual(MesajDurumu.Iletildi, ali.Paketler<MesajDurumuDegisti>().Single().Durum);   // cift tik
        Assert.IsNotNull(_o.Mesajlar.Hepsi.Single().IletildiZamani);
    }

    [TestMethod]
    public async Task OzelMesaj_CevrimdisiAliciya_GirinceIletilir()
    {
        SahteOturum ali = await _o.GirisYapAsync("ali");
        await _o.MesajAtAsync(ali, _ayseId, "sen yokken yazdim");
        Assert.IsEmpty(ali.Paketler<MesajDurumuDegisti>());   // henuz iletilmedi: tek tik

        SahteOturum ayse = await _o.GirisYapAsync("ayse");

        Assert.AreEqual("sen yokken yazdim", ayse.Paketler<MesajGeldi>().Single().Mesaj.Metin);
        Assert.HasCount(1, ali.Paketler<MesajDurumuDegisti>());   // simdi cift tik
    }

    [TestMethod]
    public async Task OzelMesaj_BeniEngelleyeneIletilmez_BenFarkEtmem()
    {
        await _o.Engeller.EngelleAsync(_ayseId, _aliId, _o.Saat.SimdiUtc);
        SahteOturum ali = await _o.GirisYapAsync("ali");
        SahteOturum ayse = await _o.GirisYapAsync("ayse");

        await _o.MesajAtAsync(ali, _ayseId, "selam");

        Assert.IsEmpty(ayse.Paketler<MesajGeldi>());
        Assert.HasCount(1, ali.Paketler<MesajKabulEdildi>());   // gonderen normal sekilde "gonderildi" gorur
        Assert.IsEmpty(ali.Paketler<MesajReddedildi>());
        Assert.IsEmpty(ali.Paketler<MesajDurumuDegisti>());     // tek tik kalir
    }

    [TestMethod]
    public async Task OzelMesaj_EngelledigimKisiyeGonderilemez()
    {
        await _o.Engeller.EngelleAsync(_aliId, _ayseId, _o.Saat.SimdiUtc);
        SahteOturum ali = await _o.GirisYapAsync("ali");

        await _o.MesajAtAsync(ali, _ayseId, "selam");

        Assert.AreEqual(HataKodu.EngelledigimKisi, ali.Paketler<MesajReddedildi>().Single().Kod);
        Assert.IsEmpty(_o.Mesajlar.Hepsi);
    }

    [TestMethod]
    public async Task CevrimdisiykenEngelledigimKisininMesaji_GirinceBanaIletilmez()
    {
        SahteOturum ayse = await _o.GirisYapAsync("ayse");
        await _o.MesajAtAsync(ayse, _aliId, "selam");
        await _o.Engeller.EngelleAsync(_aliId, _ayseId, _o.Saat.SimdiUtc);

        SahteOturum ali = await _o.GirisYapAsync("ali");
        Assert.IsEmpty(ali.Paketler<MesajGeldi>());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public async Task GecersizMesaj_Reddedilir(string metin)
    {
        SahteOturum ali = await _o.GirisYapAsync("ali");
        await _o.MesajAtAsync(ali, _ayseId, metin);
        Assert.AreEqual(HataKodu.MesajGecersiz, ali.Paketler<MesajReddedildi>().Single().Kod);
    }

    [TestMethod]
    public async Task CokUzunMesaj_Reddedilir()
    {
        SahteOturum ali = await _o.GirisYapAsync("ali");
        await _o.MesajAtAsync(ali, _ayseId, new string('a', 2001));
        Assert.AreEqual(HataKodu.MesajGecersiz, ali.Paketler<MesajReddedildi>().Single().Kod);
    }

    [TestMethod]
    public async Task KendineMesaj_VeOlmayanKisiyeMesaj_Reddedilir()
    {
        SahteOturum ali = await _o.GirisYapAsync("ali");
        await _o.MesajAtAsync(ali, _aliId, "ben");
        await _o.MesajAtAsync(ali, 111_111, "kimse");

        var kodlar = ali.Paketler<MesajReddedildi>().Select(r => r.Kod).ToList();
        CollectionAssert.AreEqual(new[] { HataKodu.KendineMesaj, HataKodu.KullaniciBulunamadi }, kodlar);
    }

    [TestMethod]
    public async Task SaniyedeBestenFazlaMesaj_Reddedilir()
    {
        SahteOturum ali = await _o.GirisYapAsync("ali");
        for (int i = 0; i < 7; i++)
        {
            await _o.MesajAtAsync(ali, _ayseId, $"mesaj {i}");
        }

        Assert.HasCount(5, ali.Paketler<MesajKabulEdildi>());
        Assert.HasCount(2, ali.Paketler<MesajReddedildi>().Where(r => r.Kod == HataKodu.HizSiniriAsildi));

        _o.Saat.Ilerlet(TimeSpan.FromSeconds(1));
        await _o.MesajAtAsync(ali, _ayseId, "bir saniye sonra");
        Assert.HasCount(6, ali.Paketler<MesajKabulEdildi>());
    }

    [TestMethod]
    public async Task GenelOda_HerkeseGider_BeniEngelleyeneGitmez()
    {
        await _o.Engeller.EngelleAsync(_veliId, _aliId, _o.Saat.SimdiUtc);
        SahteOturum ali = await _o.GirisYapAsync("ali");
        SahteOturum ayse = await _o.GirisYapAsync("ayse");
        SahteOturum veli = await _o.GirisYapAsync("veli");

        await _o.MesajAtAsync(ali, null, "herkese merhaba");

        Assert.AreEqual("herkese merhaba", ayse.Paketler<MesajGeldi>().Single().Mesaj.Metin);
        Assert.IsNull(ayse.Paketler<MesajGeldi>().Single().Mesaj.AliciId);
        Assert.IsEmpty(veli.Paketler<MesajGeldi>());
        Assert.IsEmpty(ali.Paketler<MesajGeldi>());   // kendi mesaji ona geri gelmez (MesajKabulEdildi yeter)
    }

    [TestMethod]
    public async Task Okundu_GonderenMaviTikAlir()
    {
        SahteOturum ali = await _o.GirisYapAsync("ali");
        SahteOturum ayse = await _o.GirisYapAsync("ayse");
        await _o.MesajAtAsync(ali, _ayseId, "bir");
        await _o.MesajAtAsync(ali, _ayseId, "iki");
        long sonId = ayse.Paketler<MesajGeldi>().Last().Mesaj.Id;

        await _o.Isleyici.IsleAsync(ayse, new OkunduBildir(_aliId, sonId));

        MesajlarOkundu okundu = ali.Paketler<MesajlarOkundu>().Single();
        Assert.AreEqual(_ayseId, okundu.OkuyanId);
        Assert.AreEqual(sonId, okundu.SonMesajId);
        Assert.IsTrue(_o.Mesajlar.Hepsi.All(m => m.OkunduZamani is not null));
    }

    [TestMethod]
    public async Task Okundu_EngelledigimKisiyeOkunduBilgisiGitmez()
    {
        SahteOturum ali = await _o.GirisYapAsync("ali");
        SahteOturum ayse = await _o.GirisYapAsync("ayse");
        await _o.MesajAtAsync(ali, _ayseId, "bir");
        await _o.Engeller.EngelleAsync(_ayseId, _aliId, _o.Saat.SimdiUtc);

        await _o.Isleyici.IsleAsync(ayse, new OkunduBildir(_aliId, long.MaxValue));
        Assert.IsEmpty(ali.Paketler<MesajlarOkundu>());
    }

    [TestMethod]
    public async Task Gecmis_YuzerYuzerGelir_EskidenYeniyeSirali()
    {
        SahteOturum ali = await _o.GirisYapAsync("ali");
        for (int i = 1; i <= 150; i++)
        {
            await _o.Mesajlar.KaydetAsync(_aliId, _ayseId, $"mesaj {i}", _o.Saat.SimdiUtc);
        }

        GecmisCevabi ilk = await _o.MesajServisi.GecmisAsync(_aliId, new GecmisIstegi(_ayseId, null));
        Assert.HasCount(100, ilk.Mesajlar);
        Assert.IsTrue(ilk.DahaVar);
        Assert.AreEqual("mesaj 51", ilk.Mesajlar[0].Metin);
        Assert.AreEqual("mesaj 150", ilk.Mesajlar[^1].Metin);

        GecmisCevabi ikinci = await _o.MesajServisi.GecmisAsync(_aliId, new GecmisIstegi(_ayseId, ilk.Mesajlar[0].Id));
        Assert.HasCount(50, ikinci.Mesajlar);
        Assert.IsFalse(ikinci.DahaVar);
        Assert.AreEqual("mesaj 1", ikinci.Mesajlar[0].Metin);
    }

    [TestMethod]
    public async Task Gecmis_BaskalarininOzelMesajlariniGostermez()
    {
        await _o.Mesajlar.KaydetAsync(_ayseId, _veliId, "aramizda", _o.Saat.SimdiUtc);
        GecmisCevabi gecmis = await _o.MesajServisi.GecmisAsync(_aliId, new GecmisIstegi(_veliId, null));
        Assert.IsEmpty(gecmis.Mesajlar);
    }

    [TestMethod]
    public async Task GenelGecmis_EngelledigimKisininMesajlariniGostermez()
    {
        await _o.Mesajlar.KaydetAsync(_ayseId, null, "ayseden", _o.Saat.SimdiUtc);
        await _o.Mesajlar.KaydetAsync(_veliId, null, "veliden", _o.Saat.SimdiUtc);
        await _o.Engeller.EngelleAsync(_aliId, _veliId, _o.Saat.SimdiUtc);

        GecmisCevabi gecmis = await _o.MesajServisi.GecmisAsync(_aliId, new GecmisIstegi(null, null));
        Assert.AreEqual("ayseden", gecmis.Mesajlar.Single().Metin);
    }
}
