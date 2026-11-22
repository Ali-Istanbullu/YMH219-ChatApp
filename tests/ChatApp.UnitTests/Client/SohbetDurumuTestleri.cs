using ChatApp.Client.State;
using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;

namespace ChatApp.UnitTests.Client;

[TestClass]
public sealed class SohbetDurumuTestleri
{
    private const int Ben = 111111;
    private const int Ayse = 222222;
    private static readonly DateTimeOffset T0 = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

    private readonly SohbetDurumu _d = new(Ben, "Ali");

    private static MesajBilgisi Mesaj(long id, int gonderen, int? alici, string metin = "m", MesajDurumu durum = MesajDurumu.Gonderildi) =>
        new(id, gonderen, gonderen == Ben ? "Ali" : "Ayse", alici, metin, T0.AddMinutes(id), durum);

    [TestMethod]
    public void IlkKezYazanKisi_SohbetOlusur_OkunmamisArtar_SorgulanacakKisiDoner()
    {
        Degisiklik d = _d.Uygula(new MesajGeldi(Mesaj(1, Ayse, Ben, "selam")));

        Sohbet sohbet = _d.Bul(Ayse)!;
        Assert.AreEqual("Ayse", sohbet.Ad);
        Assert.AreEqual(1, sohbet.Okunmamis);
        Assert.AreEqual("selam", d.YeniMesaj!.Metin);
        Assert.AreEqual(Ayse, d.SorgulanacakKisi);
        Assert.AreSame(sohbet, d.Sohbet);
    }

    [TestMethod]
    public void AyniMesajIkinciKezGelirse_EklenmezSayilmaz()
    {
        _d.Uygula(new MesajGeldi(Mesaj(1, Ayse, Ben)));
        Degisiklik d = _d.Uygula(new MesajGeldi(Mesaj(1, Ayse, Ben)));

        Assert.IsNull(d.YeniMesaj);
        Assert.HasCount(1, _d.Bul(Ayse)!.Mesajlar);
        Assert.AreEqual(1, _d.Bul(Ayse)!.Okunmamis);
    }

    [TestMethod]
    public void GenelMesaj_GenelOdayaDuser()
    {
        _d.Uygula(new MesajGeldi(Mesaj(1, Ayse, null, "herkese")));
        Assert.AreEqual("herkese", _d.Genel.Mesajlar.Single().Metin);
        Assert.IsEmpty(_d.OzelSohbetler);
    }

    [TestMethod]
    public void GonderdigimMesaj_GonderiliyorKabulIletildiOkundu()
    {
        Sohbet sohbet = _d.SohbetGetir(Ayse, "Ayse");
        var istemciId = Guid.NewGuid();
        MesajSatiri satir = _d.YerelMesajEkle(sohbet, istemciId, "merhaba", T0);
        Assert.IsTrue(satir.Gonderiliyor);

        _d.Uygula(new MesajKabulEdildi(istemciId, Mesaj(10, Ben, Ayse, "merhaba")));
        Assert.AreEqual(10L, satir.Id);
        Assert.AreEqual(MesajDurumu.Gonderildi, satir.Durum);

        _d.Uygula(new MesajDurumuDegisti(10, MesajDurumu.Iletildi));
        Assert.AreEqual(MesajDurumu.Iletildi, satir.Durum);

        _d.Uygula(new MesajlarOkundu(Ayse, 10));
        Assert.AreEqual(MesajDurumu.Okundu, satir.Durum);

        _d.Uygula(new MesajDurumuDegisti(10, MesajDurumu.Iletildi));   // gec gelen eski bilgi
        Assert.AreEqual(MesajDurumu.Okundu, satir.Durum, "tikler geri gitmemeli");
        Assert.HasCount(1, sohbet.Mesajlar);
    }

    [TestMethod]
    public void GonderilemeyenMesaj_YerindeKalir_SonradanGelenlerAltinaGelir()
    {
        Sohbet genel = _d.Genel;
        _d.Uygula(new MesajGeldi(Mesaj(1, Ayse, null, "ilk")));
        MesajSatiri gonderilemeyen = _d.YerelMesajEkle(genel, Guid.NewGuid(), "bekleyen", T0);
        gonderilemeyen.Hata = "hiz siniri";

        var yeniId = Guid.NewGuid();
        _d.YerelMesajEkle(genel, yeniId, "sonra yazdim", T0);
        _d.Uygula(new MesajKabulEdildi(yeniId, Mesaj(2, Ben, null, "sonra yazdim")));
        _d.Uygula(new MesajGeldi(Mesaj(3, Ayse, null, "cevap")));

        CollectionAssert.AreEqual(new[] { "ilk", "bekleyen", "sonra yazdim", "cevap" },
            genel.Mesajlar.Select(m => m.Metin).ToArray());
    }

    [TestMethod]
    public void GonderilmeyiBekleyenler_YazilmaSirasiyla_ReddedilenlerHaric()
    {
        Sohbet ayse = _d.SohbetGetir(Ayse, "Ayse");
        MesajSatiri bir = _d.YerelMesajEkle(_d.Genel, Guid.NewGuid(), "1", T0);
        MesajSatiri iki = _d.YerelMesajEkle(ayse, Guid.NewGuid(), "2", T0);
        MesajSatiri reddedilen = _d.YerelMesajEkle(ayse, Guid.NewGuid(), "3", T0);
        reddedilen.Hata = "engelli";

        CollectionAssert.AreEqual(new[] { bir, iki }, _d.GonderilmeyiBekleyenler().ToArray());
    }

    [TestMethod]
    public void HerkestenSilindi_Isaretlenir_BendenSilinen_Kalkar_SohbetSilinen_Gider()
    {
        _d.Uygula(new MesajGeldi(Mesaj(1, Ayse, Ben, "silinecek")));
        _d.Uygula(new MesajGeldi(Mesaj(2, Ayse, Ben, "gizlenecek")));
        Sohbet sohbet = _d.Bul(Ayse)!;

        _d.Uygula(new MesajHerkestenSilindi(1));
        Assert.IsTrue(sohbet.Mesajlar[0].HerkestenSilindi);

        _d.MesajiKaldir(sohbet.Mesajlar[1]);
        Assert.HasCount(1, sohbet.Mesajlar);

        _d.SohbetiKaldir(Ayse);
        Assert.IsNull(_d.Bul(Ayse));
        _d.Uygula(new MesajGeldi(Mesaj(3, Ayse, Ben, "yeniden")));   // yeniden yazinca geri gelir
        Assert.AreEqual("yeniden", _d.Bul(Ayse)!.Mesajlar.Single().Metin);
    }

    [TestMethod]
    public void ReddedilenMesaj_TurkceSebepAlir()
    {
        Sohbet sohbet = _d.SohbetGetir(Ayse, "Ayse");
        var istemciId = Guid.NewGuid();
        MesajSatiri satir = _d.YerelMesajEkle(sohbet, istemciId, "x", T0);

        _d.Uygula(new MesajReddedildi(istemciId, HataKodu.HizSiniriAsildi));

        Assert.IsFalse(satir.Gonderiliyor);
        Assert.Contains("hızlı", satir.Hata!);
    }

    [TestMethod]
    public void Gecmis_MevcutMesajlarlaBirlesir_SiraliOlur_DahaEskiBilgisiGuncellenir()
    {
        Sohbet sohbet = _d.SohbetGetir(Ayse, "Ayse");
        _d.Uygula(new MesajGeldi(Mesaj(5, Ayse, Ben)));

        _d.Uygula(new GecmisCevabi(Ayse, [Mesaj(3, Ben, Ayse), Mesaj(4, Ayse, Ben), Mesaj(5, Ayse, Ben)], DahaVar: true));

        CollectionAssert.AreEqual(new long?[] { 3, 4, 5 }, sohbet.Mesajlar.Select(m => m.Id).ToArray());
        Assert.IsTrue(sohbet.GecmisYuklendi);
        Assert.IsTrue(sohbet.DahaEskiVar);
        Assert.AreEqual(3L, sohbet.EnEskiMesajId);

        _d.Uygula(new GecmisCevabi(Ayse, [Mesaj(1, Ayse, Ben), Mesaj(2, Ben, Ayse)], DahaVar: false));
        CollectionAssert.AreEqual(new long?[] { 1, 2, 3, 4, 5 }, sohbet.Mesajlar.Select(m => m.Id).ToArray());
        Assert.IsFalse(sohbet.DahaEskiVar);
    }

    [TestMethod]
    public void SohbetListesi_OkunmamisiSunucununSayisinaEsitler_UstuneEklemez()
    {
        _d.Uygula(new MesajGeldi(Mesaj(1, Ayse, Ben)));   // giriste once bekleyen mesaj gelir...
        _d.Uygula(new SohbetListesi([new KisiBilgisi(Ayse, "Ayşe", true, 1, false)]));   // ...sonra liste

        Sohbet sohbet = _d.Bul(Ayse)!;
        Assert.AreEqual(1, sohbet.Okunmamis);
        Assert.AreEqual("Ayşe", sohbet.Ad);
        Assert.IsTrue(sohbet.Cevrimici);
    }

    [TestMethod]
    public void OkunduIsaretle_KarsininSonMesajiniBirKezDoner()
    {
        Sohbet sohbet = _d.SohbetGetir(Ayse, "Ayse");
        _d.Uygula(new MesajGeldi(Mesaj(7, Ayse, Ben)));
        _d.Uygula(new MesajGeldi(Mesaj(8, Ayse, Ben)));

        Assert.AreEqual(8L, _d.OkunduIsaretle(sohbet));
        Assert.AreEqual(0, sohbet.Okunmamis);
        Assert.IsNull(_d.OkunduIsaretle(sohbet), "ayni bildirim ikinci kez gitmemeli");
        Assert.IsNull(_d.OkunduIsaretle(_d.Genel), "Genel'de okundu bilgisi yok");
    }

    [TestMethod]
    public void OzelSohbetler_EnSonMesajiOlanUstte()
    {
        _d.SohbetGetir(333333, "Zeynep");
        _d.Uygula(new MesajGeldi(Mesaj(1, Ayse, Ben)));
        _d.Uygula(new MesajGeldi(Mesaj(2, 444444, Ben)));

        CollectionAssert.AreEqual(new int?[] { 444444, Ayse, 333333 }, _d.OzelSohbetler.Select(s => s.KarsiId).ToArray());
    }

    [TestMethod]
    public void CevrimiciDurumu_BilinenKisiyiGunceller_BilinmeyeniEklemez()
    {
        _d.SohbetGetir(Ayse, "Ayse");
        _d.Uygula(new CevrimiciDurumu(Ayse, true));
        _d.Uygula(new CevrimiciDurumu(999999, true));

        Assert.IsTrue(_d.Bul(Ayse)!.Cevrimici);
        Assert.IsNull(_d.Bul(999999));
    }
}
