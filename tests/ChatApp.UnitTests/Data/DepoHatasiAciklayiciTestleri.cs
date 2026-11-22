using ChatApp.Server.Data;

namespace ChatApp.UnitTests.Data;

[TestClass]
public sealed class DepoHatasiAciklayiciTestleri
{
    [TestMethod]
    [DataRow(DepoHatasiSebebi.KimlikDogrulanamadi, "şifre yanlış")]
    [DataRow(DepoHatasiSebebi.BaglantiKurulamadi, "PostgreSQL yok")]
    [DataRow(DepoHatasiSebebi.SunucuBulunamadi, "adresi bulunamadı")]
    [DataRow(DepoHatasiSebebi.VeritabaniYok, "veritabanı yok")]
    [DataRow(DepoHatasiSebebi.ZamanAsimi, "zaman aşımı")]
    [DataRow(DepoHatasiSebebi.Diger, "sorun oluştu")]
    public void HerSebep_TurkceAciklama_VeTeknikAyrinti(DepoHatasiSebebi sebep, string beklenen)
    {
        var hata = new DepoHatasi("x", new IOException("orijinal mesaj"), sebep);

        string metin = DepoHatasiAciklayici.Acikla(hata);

        Assert.Contains(beklenen, metin);
        Assert.Contains("Teknik ayrıntı: orijinal mesaj", metin);
    }
}
