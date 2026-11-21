using System.Security.Cryptography.X509Certificates;
using ChatApp.Protocol.Kurallar;
using ChatApp.Server.Security;
using ChatApp.UnitTests.Fakes;

namespace ChatApp.UnitTests.Security;

[TestClass]
public sealed class GuvenlikTestleri
{
    [TestMethod]
    public void SifreHash_DogruSifreyiKabulEder_YanlisiReddeder()
    {
        var hashleyici = new SifreHashleyici(tekrar: 1000);
        (byte[] hash, byte[] tuz) = hashleyici.Hashle("dogru-sifre");

        Assert.IsTrue(hashleyici.DogruMu("dogru-sifre", hash, tuz));
        Assert.IsFalse(hashleyici.DogruMu("yanlis-sifre", hash, tuz));
    }

    [TestMethod]
    public void AyniSifre_FarkliTuzlaFarkliHashVerir()
    {
        var hashleyici = new SifreHashleyici(tekrar: 1000);
        (byte[] hash1, byte[] tuz1) = hashleyici.Hashle("ayni");
        (byte[] hash2, byte[] tuz2) = hashleyici.Hashle("ayni");

        CollectionAssert.AreNotEqual(tuz1, tuz2);
        CollectionAssert.AreNotEqual(hash1, hash2);
    }

    [TestMethod]
    public void GirisKilidi_BesYanlistanSonraBirDakikaKilitlenir()
    {
        var saat = new SahteSaat();
        var kilit = new GirisKilidi(saat);

        for (int i = 0; i < GirisKilidi.DenemeHakki - 1; i++)
        {
            kilit.BasarisizDeneme("ali");
        }
        Assert.IsFalse(kilit.KilitliMi("ali"));

        kilit.BasarisizDeneme("ali");
        Assert.IsTrue(kilit.KilitliMi("ali"));
        Assert.IsFalse(kilit.KilitliMi("ayse"));   // baska hesap etkilenmez

        saat.Ilerlet(GirisKilidi.KilitSuresi);
        Assert.IsFalse(kilit.KilitliMi("ali"));
    }

    [TestMethod]
    public void GirisKilidi_DogruGirisSayaciSifirlar()
    {
        var kilit = new GirisKilidi(new SahteSaat());
        for (int i = 0; i < GirisKilidi.DenemeHakki - 1; i++)
        {
            kilit.BasarisizDeneme("ali");
        }
        kilit.BasariliGiris("ali");
        kilit.BasarisizDeneme("ali");
        Assert.IsFalse(kilit.KilitliMi("ali"));
    }

    [TestMethod]
    public void HizSinirlayici_PencereIcindeSiniriAsaniReddeder_PencereGecinceIzinVerir()
    {
        var saat = new SahteSaat();
        var sinirlayici = new HizSinirlayici(saat);
        TimeSpan pencere = TimeSpan.FromSeconds(1);

        for (int i = 0; i < 5; i++)
        {
            Assert.IsTrue(sinirlayici.IzinVer("mesaj:1", 5, pencere));
        }
        Assert.IsFalse(sinirlayici.IzinVer("mesaj:1", 5, pencere));
        Assert.IsTrue(sinirlayici.IzinVer("mesaj:2", 5, pencere));   // anahtarlar birbirinden bagimsiz

        saat.Ilerlet(pencere);
        Assert.IsTrue(sinirlayici.IzinVer("mesaj:1", 5, pencere));
    }

    [TestMethod]
    public void DavetKodu_KurallaraUygunVeTekrarsiz()
    {
        var kodlar = Enumerable.Range(0, 1000).Select(_ => DavetKoduUreteci.Uret()).ToList();
        Assert.IsTrue(kodlar.All(k => GirdiKurallari.DavetKodunuDuzenle(k) == k));
        Assert.HasCount(1000, kodlar.Distinct());
    }

    [TestMethod]
    public void Sertifika_IlkSeferdeUretilir_SonrakiSeferdeAynisiYuklenir()
    {
        string klasor = Path.Combine(Path.GetTempPath(), "chatapp-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var depo = new SertifikaDeposu(klasor, new SahteSirKoruyucu(), new SahteSaat(), new SahteGunlukcu());

            using X509Certificate2 ilk = depo.YukleVeyaOlustur();
            using X509Certificate2 ikinci = depo.YukleVeyaOlustur();

            Assert.IsTrue(ilk.HasPrivateKey);
            Assert.AreEqual(SertifikaDeposu.ParmakIzi(ilk), SertifikaDeposu.ParmakIzi(ikinci));
            Assert.AreEqual(64, SertifikaDeposu.ParmakIzi(ilk).Length);   // SHA-256 = 32 bayt = 64 onaltilik
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }

    [TestMethod]
    public void Sertifika_DosyaBozuksa_YenisiUretilirVeLoglanir()
    {
        string klasor = Path.Combine(Path.GetTempPath(), "chatapp-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(klasor);
            File.WriteAllBytes(Path.Combine(klasor, SertifikaDeposu.DosyaAdi), [1, 2, 3]);
            var gunlukcu = new SahteGunlukcu();

            using X509Certificate2 sertifika = new SertifikaDeposu(klasor, new SahteSirKoruyucu(), new SahteSaat(), gunlukcu)
                .YukleVeyaOlustur();

            Assert.IsTrue(sertifika.HasPrivateKey);
            Assert.HasCount(1, gunlukcu.Hatalar);
        }
        finally
        {
            Directory.Delete(klasor, recursive: true);
        }
    }
}
