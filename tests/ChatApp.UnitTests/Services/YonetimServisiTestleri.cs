using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;
using ChatApp.UnitTests.Fakes;

namespace ChatApp.UnitTests.Services;

[TestClass]
public sealed class YonetimServisiTestleri
{
    private readonly TestOrtami _o = new();

    [TestMethod]
    public async Task Yasakla_CevrimiciyseHemenCikarir_TekrarGiremez()
    {
        int aliId = await _o.HesapAcAsync("ali", "Ali");
        SahteOturum ali = await _o.GirisYapAsync("ali");

        Assert.IsTrue(await _o.Yonetim.YasaklaAsync(aliId, "  kufur  "));

        Assert.IsTrue(ali.Kapatildi);
        Assert.AreEqual(new OturumSonlandi(OturumSonuSebebi.Yasaklandi, "kufur"), ali.KapanisSebebi);

        var yeniDeneme = new SahteOturum();
        await _o.Isleyici.IsleAsync(yeniDeneme, new GirisIstegi("ali", TestOrtami.Sifre));
        Assert.AreEqual(HataKodu.HesapYasakli, yeniDeneme.Paketler<GirisSonucu>().Single().Hata);
        Assert.HasCount(1, await _o.Yonetim.YasaklilarAsync());
    }

    [TestMethod]
    public async Task YasagiKaldir_TekrarGirebilir()
    {
        int aliId = await _o.HesapAcAsync("ali", "Ali");
        await _o.Yonetim.YasaklaAsync(aliId, null);
        await _o.Yonetim.YasagiKaldirAsync(aliId);

        await _o.GirisYapAsync("ali");
        Assert.IsEmpty(await _o.Yonetim.YasaklilarAsync());
    }

    [TestMethod]
    public async Task At_BaglantiyiKeser_HesapDurur()
    {
        int aliId = await _o.HesapAcAsync("ali", "Ali");
        SahteOturum ali = await _o.GirisYapAsync("ali");

        Assert.IsTrue(await _o.Yonetim.AtAsync(aliId));
        Assert.AreEqual(OturumSonuSebebi.Atildi, ali.KapanisSebebi!.Sebep);

        await _o.GirisYapAsync("ali");   // tekrar girebilir
    }

    [TestMethod]
    public async Task OlmayanKisi_AtilamazYasaklanamaz()
    {
        Assert.IsFalse(await _o.Yonetim.AtAsync(111_111));
        Assert.IsFalse(await _o.Yonetim.YasaklaAsync(111_111, "x"));
    }

    [TestMethod]
    public async Task KullaniciBul_NumaraVeyaAdIle()
    {
        int aliId = await _o.HesapAcAsync("ali", "Ali");
        Assert.AreEqual(aliId, (await _o.Yonetim.KullaniciBulAsync(aliId.ToString()))!.Id);
        Assert.AreEqual(aliId, (await _o.Yonetim.KullaniciBulAsync(" ALI "))!.Id);
        Assert.IsNull(await _o.Yonetim.KullaniciBulAsync("yok"));
    }
}
