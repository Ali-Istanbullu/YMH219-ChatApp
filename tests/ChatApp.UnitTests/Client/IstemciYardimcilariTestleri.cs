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

    private const string OrnekParmakIzi = "D98C1EF453A98B83F7A2B878D3A19D59842F0D80B585EFD78CF9FC21A2AC5774";

    [TestMethod]
    [DataRow(OrnekParmakIzi)]
    [DataRow("d98c1ef453a98b83f7a2b878d3a19d59842f0d80b585efd78cf9fc21a2ac5774")]
    [DataRow("  D98C 1EF4 53A9 8B83 F7A2 B878 D3A1 9D59 842F 0D80 B585 EFD7 8CF9 FC21 A2AC 5774 ")]
    [DataRow("D9:8C:1E:F4:53:A9:8B:83:F7:A2:B8:78:D3:A1:9D:59:84:2F:0D:80:B5:85:EF:D7:8C:F9:FC:21:A2:AC:57:74")]
    public void ParmakIzi_BoslukIkiNoktaVeKucukHarfYokSayilir(string metin)
    {
        Assert.AreEqual(OrnekParmakIzi, ParmakIziBicimi.Duzenle(metin));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("192.168.1.20:5050")]
    [DataRow("D98C1EF453A98B83")]                                                           // kisa
    [DataRow("D98C1EF453A98B83F7A2B878D3A19D59842F0D80B585EFD78CF9FC21A2AC577G")]           // G onaltilik degil
    [DataRow("D98C1EF453A98B83F7A2B878D3A19D59842F0D80B585EFD78CF9FC21A2AC57744")]          // uzun
    public void ParmakIzi_GecersizOlanlar(string metin)
    {
        Assert.IsNull(ParmakIziBicimi.Duzenle(metin));
    }

    [TestMethod]
    public void ParmakIziDeposu_Sil_SadeceOAdresiSiler()
    {
        string klasor = Path.Combine(Path.GetTempPath(), "chatapp-istemci-" + Guid.NewGuid().ToString("N"));
        try
        {
            var depo = new ParmakIziDeposu(Path.Combine(klasor, "sunucular.json"), new SahteIstemciGunlukcu());
            var a = new SunucuAdresi("10.0.0.1", 5050);
            var b = new SunucuAdresi("10.0.0.2", 5050);
            depo.Kaydet(a, "AA");
            depo.Kaydet(b, "BB");

            depo.Sil(a);
            depo.Sil(new SunucuAdresi("10.0.0.9", 5050));   // olmayan: sorun yok

            Assert.IsNull(depo.Bul(a));
            Assert.AreEqual("BB", depo.Bul(b));
        }
        finally
        {
            if (Directory.Exists(klasor))
            {
                Directory.Delete(klasor, recursive: true);
            }
        }
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
