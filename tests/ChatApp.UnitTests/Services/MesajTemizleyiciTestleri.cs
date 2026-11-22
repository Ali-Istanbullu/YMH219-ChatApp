using ChatApp.Server.Services;
using ChatApp.UnitTests.Fakes;

namespace ChatApp.UnitTests.Services;

[TestClass]
public sealed class MesajTemizleyiciTestleri
{
    private readonly TestOrtami _o = new();

    // Sahte depoda her mesaj 250 bayt: ust sinir 5000 bayt (20 mesaj), hedef 2500 bayt (10 mesaj).
    private MesajTemizleyici Temizleyici() => new(_o.Mesajlar, _o.Gunlukcu, ustSinir: 5000, hedef: 2500);

    private async Task MesajlarEkleAsync(int adet)
    {
        int ali = await _o.HesapAcAsync("ali", "Ali");
        for (int i = 1; i <= adet; i++)
        {
            await _o.Mesajlar.KaydetAsync(ali, null, $"mesaj {i}", _o.Saat.SimdiUtc);
        }
    }

    [TestMethod]
    public async Task SinirAltindaysa_HicbirSeySilinmez()
    {
        await MesajlarEkleAsync(20);   // tam 5000 bayt: sinirda, asmadi

        Assert.AreEqual(0, await Temizleyici().GerekirseTemizleAsync());
        Assert.HasCount(20, _o.Mesajlar.Hepsi);
        Assert.AreEqual(0, _o.Mesajlar.SikistirmaSayisi);
    }

    [TestMethod]
    public async Task SiniriGecince_HedefeKadarEnEskilerSilinir_TabloSikistirilir()
    {
        await MesajlarEkleAsync(24);   // 6000 bayt > 5000

        long silinen = await Temizleyici().GerekirseTemizleAsync();

        // 24 x (2500 / 6000) = 10 mesaj kalir.
        Assert.AreEqual(14, silinen);
        Assert.HasCount(10, _o.Mesajlar.Hepsi);
        Assert.AreEqual("mesaj 15", _o.Mesajlar.Hepsi[0].Metin);   // en yeniler kaldi
        Assert.AreEqual("mesaj 24", _o.Mesajlar.Hepsi[^1].Metin);
        Assert.AreEqual(1, _o.Mesajlar.SikistirmaSayisi);
        Assert.IsTrue(_o.Gunlukcu.Satirlar.Any(s => s.Contains("14 eski mesaj silindi")));
    }

    [TestMethod]
    public async Task Istatistik_HesapMesajVeBoyutuVerir()
    {
        await MesajlarEkleAsync(4);
        SunucuIstatistigi istatistik = await _o.Yonetim.IstatistikAsync();
        Assert.AreEqual(new SunucuIstatistigi(1, 4, 1000), istatistik);
    }
}
