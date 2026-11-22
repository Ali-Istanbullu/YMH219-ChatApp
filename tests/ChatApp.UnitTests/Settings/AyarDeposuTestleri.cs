using System.Text;
using ChatApp.Server.Settings;
using ChatApp.UnitTests.Fakes;

namespace ChatApp.UnitTests.Settings;

[TestClass]
public sealed class AyarDeposuTestleri
{
    private readonly SahteGunlukcu _gunlukcu = new();
    private string _klasor = string.Empty;
    private string _dosya = string.Empty;

    [TestInitialize]
    public void Hazirla()
    {
        _klasor = Path.Combine(Path.GetTempPath(), "chatapp-test-" + Guid.NewGuid().ToString("N"));
        _dosya = Path.Combine(_klasor, "ayarlar.json");
    }

    [TestCleanup]
    public void Temizle()
    {
        if (Directory.Exists(_klasor))
        {
            Directory.Delete(_klasor, recursive: true);
        }
    }

    private AyarDeposu Depo() => new(_dosya, new SahteSirKoruyucu(), _gunlukcu);

    [TestMethod]
    public void DosyaYoksa_NullDoner()
    {
        Assert.IsNull(Depo().Yukle());
    }

    [TestMethod]
    public void KaydedilenAyar_AynenGeriOkunur_KlasorYoksaOlusturulur()
    {
        var ayarlar = new SunucuAyarlari("localhost", 5433, "chatapp", "chatapp", "gizli;sifre=1", 5050);
        Depo().Kaydet(ayarlar);
        Assert.AreEqual(ayarlar, Depo().Yukle());
    }

    [TestMethod]
    public void HazirVeritabaniBilgisi_Saklanir_EskiDosyadaYoksaFalse()
    {
        Depo().Kaydet(SunucuAyarlari.Varsayilan with { VeritabaniSifresi = "s", GomuluVeritabani = true });
        Assert.IsTrue(Depo().Yukle()!.GomuluVeritabani);

        // Hazir veritabani ozelliginden onceki surumun yazdigi dosya.
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(_dosya))!.AsObject();
        Assert.IsTrue(json.Remove("GomuluVeritabani"));
        File.WriteAllText(_dosya, json.ToJsonString());
        Assert.IsFalse(Depo().Yukle()!.GomuluVeritabani);
    }

    [TestMethod]
    public void Sifre_DosyayaAcikYazilmaz()
    {
        Depo().Kaydet(SunucuAyarlari.Varsayilan with { VeritabaniSifresi = "CokGizliSifre" });
        Assert.DoesNotContain("CokGizliSifre", File.ReadAllText(_dosya, Encoding.UTF8));
    }

    [TestMethod]
    public void CalisirkenSilinenDosya_YenidenOlusturulur_KlasorDahil()
    {
        var ayarlar = SunucuAyarlari.Varsayilan with { VeritabaniSifresi = "s" };
        Depo().Kaydet(ayarlar);
        Directory.Delete(_klasor, recursive: true);   // kullanici klasoru sildi

        Depo().SilindiyseYenidenOlustur(ayarlar);

        Assert.AreEqual(ayarlar, Depo().Yukle());
        Assert.IsTrue(_gunlukcu.Satirlar.Any(s => s.Contains("yeniden olusturuldu")));
    }

    [TestMethod]
    public void DosyaVarsa_SilindiyseYenidenOlustur_DokunmazHicbirSey()
    {
        Depo().Kaydet(SunucuAyarlari.Varsayilan with { DinlemePortu = 6000 });

        Depo().SilindiyseYenidenOlustur(SunucuAyarlari.Varsayilan);

        Assert.AreEqual(6000, Depo().Yukle()!.DinlemePortu);
    }

    [TestMethod]
    [DataRow("{ bozuk json")]
    [DataRow("{\"VeritabaniSunucusu\":\"a\",\"VeritabaniPortu\":1,\"VeritabaniAdi\":\"b\",\"VeritabaniKullanicisi\":\"c\",\"KorunmusSifre\":\"base64 degil!\",\"DinlemePortu\":2}")]
    public void BozukDosya_NullDoner_LogaYazilir(string icerik)
    {
        Directory.CreateDirectory(_klasor);
        File.WriteAllText(_dosya, icerik);

        Assert.IsNull(Depo().Yukle());
        Assert.IsTrue(_gunlukcu.Satirlar.Any(s => s.Contains("Ayar dosyasi okunamadi")));
    }
}
