using ChatApp.Client.Metinler;
using ChatApp.Client.Network;
using ChatApp.Client.Settings;
using ChatApp.Protocol.Modeller;
using ChatApp.UnitTests.Fakes;

namespace ChatApp.UnitTests.Client;

[TestClass]
public sealed class IstemciYardimcilariTestleri
{
    [TestMethod]
    [DataRow("localhost", "localhost", 5050)]
    [DataRow("  192.168.1.20:6000 ", "192.168.1.20", 6000)]
    [DataRow("0.tcp.eu.ngrok.io:12345", "0.tcp.eu.ngrok.io", 12345)]
    public void SunucuAdresi_GecerliOlanlar(string metin, string sunucu, int port)
    {
        Assert.IsTrue(SunucuAdresi.TryParse(metin, out SunucuAdresi? adres));
        Assert.AreEqual(new SunucuAdresi(sunucu, port), adres);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("local host")]
    [DataRow("host:0")]
    [DataRow("host:70000")]
    [DataRow("host:abc")]
    [DataRow(":5050")]
    [DataRow("::1")]
    public void SunucuAdresi_GecersizOlanlar(string metin)
    {
        Assert.IsFalse(SunucuAdresi.TryParse(metin, out _));
    }

    [TestMethod]
    public void HerHataKoduVeOturumSebebi_TurkceAciklamaAlir()
    {
        foreach (HataKodu kod in Enum.GetValues<HataKodu>())
        {
            Assert.AreNotEqual(HataMetinleri.Acikla((HataKodu?)null), HataMetinleri.Acikla(kod), $"{kod} icin metin yok");
        }
        foreach (OturumSonuSebebi sebep in Enum.GetValues<OturumSonuSebebi>())
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(HataMetinleri.Acikla(sebep, null)));
        }
        Assert.Contains("Sebep: spam", HataMetinleri.GirisHatasi(HataKodu.HesapYasakli, "spam"));
    }

    [TestMethod]
    public void Depolar_KlasorSilinseBileYazar_BozukDosyadaVarsayilanaDoner()
    {
        string klasor = Path.Combine(Path.GetTempPath(), "chatapp-istemci-" + Guid.NewGuid().ToString("N"));
        try
        {
            var gunlukcu = new SahteIstemciGunlukcu();
            var adres = new SunucuAdresi("Sunucu", 5050);
            var parmakIzleri = new ParmakIziDeposu(Path.Combine(klasor, "sunucular.json"), gunlukcu);
            var ayarlar = new IstemciAyarDeposu(Path.Combine(klasor, "ayarlar.json"), gunlukcu);

            Assert.IsNull(parmakIzleri.Bul(adres));
            parmakIzleri.Kaydet(adres, "AB12");
            Assert.AreEqual("AB12", parmakIzleri.Bul(new SunucuAdresi("sunucu", 5050)), "adres buyuk/kucuk harf duyarsiz");

            ayarlar.Kaydet(new IstemciAyarlari("sunucu:6000", "ali"));
            Assert.AreEqual(new IstemciAyarlari("sunucu:6000", "ali"), ayarlar.Yukle());

            // Eski surumun dosyasi (alan adi farkli): cokmez, eksik alan bos gelir.
            File.WriteAllText(Path.Combine(klasor, "ayarlar.json"), "{\"SonSunucu\":\"localhost\",\"SonKullaniciAdi\":\"ali\"}");
            Assert.AreEqual(new IstemciAyarlari("", "ali"), ayarlar.Yukle());

            File.WriteAllText(Path.Combine(klasor, "ayarlar.json"), "{ bozuk");
            File.WriteAllText(Path.Combine(klasor, "sunucular.json"), "{ bozuk");
            Assert.AreEqual(IstemciAyarlari.Varsayilan, ayarlar.Yukle());
            Assert.IsNull(parmakIzleri.Bul(adres));

            Directory.Delete(klasor, recursive: true);
            ayarlar.Kaydet(new IstemciAyarlari("x", "y"));   // klasor yeniden olusur
            Assert.AreEqual(new IstemciAyarlari("x", "y"), ayarlar.Yukle());
        }
        finally
        {
            if (Directory.Exists(klasor))
            {
                Directory.Delete(klasor, recursive: true);
            }
        }
    }
}
