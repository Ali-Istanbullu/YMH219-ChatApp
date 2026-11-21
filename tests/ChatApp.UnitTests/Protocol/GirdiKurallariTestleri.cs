using ChatApp.Protocol.Kurallar;

namespace ChatApp.UnitTests.Protocol;

[TestClass]
public sealed class GirdiKurallariTestleri
{
    [TestMethod]
    [DataRow("ali", true)]
    [DataRow("ali.istanbullu", true)]
    [DataRow("ali_2026", true)]
    [DataRow("al", false)]                         // cok kisa
    [DataRow("abcdefghijklmnopqrstu", false)]      // 21 karakter
    [DataRow("Ali", false)]                        // buyuk harf
    [DataRow("ali veli", false)]                   // bosluk
    [DataRow("alı", false)]                        // Turkce karakter
    [DataRow("", false)]
    [DataRow(null, false)]
    public void KullaniciAdi(string? ad, bool gecerli) =>
        Assert.AreEqual(gecerli, GirdiKurallari.KullaniciAdiGecerliMi(ad));

    [TestMethod]
    public void GorunenAd_BasSonBosluklariAtilir_TurkceSerbest()
    {
        Assert.AreEqual("Ali İstanbullu", GirdiKurallari.GorunenAdiDuzenle("  Ali İstanbullu "));
        Assert.IsNull(GirdiKurallari.GorunenAdiDuzenle("   "));
        Assert.IsNull(GirdiKurallari.GorunenAdiDuzenle(new string('a', 41)));
        Assert.IsNull(GirdiKurallari.GorunenAdiDuzenle("Ali\u0007"));
    }

    [TestMethod]
    [DataRow("1234567", false)]
    [DataRow("12345678", true)]
    [DataRow(null, false)]
    public void Sifre(string? sifre, bool gecerli) => Assert.AreEqual(gecerli, GirdiKurallari.SifreGecerliMi(sifre));

    [TestMethod]
    public void Mesaj_Kurallari()
    {
        Assert.IsTrue(GirdiKurallari.MesajGecerliMi("merhaba"));
        Assert.IsTrue(GirdiKurallari.MesajGecerliMi("iki\nsatir"));
        Assert.IsTrue(GirdiKurallari.MesajGecerliMi(new string('a', 2000)));
        Assert.IsFalse(GirdiKurallari.MesajGecerliMi(new string('a', 2001)));
        Assert.IsFalse(GirdiKurallari.MesajGecerliMi("   "));
        Assert.IsFalse(GirdiKurallari.MesajGecerliMi("zil\u0007"));
        Assert.IsFalse(GirdiKurallari.MesajGecerliMi(null));
    }

    [TestMethod]
    public void DavetKodu_KucukHarfVeBosluklaGirilseDeKabulEdilir()
    {
        Assert.AreEqual("K7M2-QX9P", GirdiKurallari.DavetKodunuDuzenle(" k7m2-qx9p "));
        Assert.IsNull(GirdiKurallari.DavetKodunuDuzenle("K7M2QX9P"));
        Assert.IsNull(GirdiKurallari.DavetKodunuDuzenle("K0M2-QX9P"));   // 0 kullanilmaz
        Assert.IsNull(GirdiKurallari.DavetKodunuDuzenle(null));
    }
}
