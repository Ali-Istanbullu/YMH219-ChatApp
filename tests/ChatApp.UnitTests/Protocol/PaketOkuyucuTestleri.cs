using System.Text;
using ChatApp.Protocol.Iletisim;
using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;

namespace ChatApp.UnitTests.Protocol;

[TestClass]
public sealed class PaketOkuyucuTestleri
{
    private static PaketOkuyucu Okuyucu(string metin, int sinir = 1024) =>
        new(new MemoryStream(Encoding.UTF8.GetBytes(metin)), sinir);

    private static PaketOkuyucu Okuyucu(byte[] veri, int sinir = 1024 * 1024) => new(new MemoryStream(veri), sinir);

    [TestMethod]
    public async Task YazilanPaket_AyniHaliyleOkunur()
    {
        var mesaj = new MesajBilgisi(42, 123456, "Ayşe Çelik", null, "Merhaba ğüşıöç İ", DateTimeOffset.UnixEpoch, MesajDurumu.Okundu);
        Paket[] paketler =
        [
            new GirisIstegi("ali", "sifre"),
            new MesajGonder(Guid.NewGuid(), 654321, "selam"),
            new MesajGeldi(mesaj),
            new SohbetListesi([new KisiBilgisi(1, "Ali", true, 3, false)]),
            new OturumSonlandi(OturumSonuSebebi.Yasaklandi, "spam")
        ];
        byte[] veri = paketler.SelectMany(PaketYazici.Serilestir).ToArray();

        PaketOkuyucu okuyucu = Okuyucu(veri);
        foreach (Paket beklenen in paketler)
        {
            Paket? okunan = await okuyucu.OkuAsync(CancellationToken.None);
            Assert.AreEqual(beklenen.GetType(), okunan!.GetType());
            CollectionAssert.AreEqual(PaketYazici.Serilestir(beklenen), PaketYazici.Serilestir(okunan));
        }
        Assert.IsNull(await okuyucu.OkuAsync(CancellationToken.None));   // akis bitti
    }

    [TestMethod]
    public async Task MesajIcerigi_TurkceKarakterlerleBozulmadanGelir()
    {
        var gonderilen = new MesajGeldi(new MesajBilgisi(1, 2, "Şükrü", 3, "Iğdır'da ılık bir gün", DateTimeOffset.UnixEpoch, MesajDurumu.Gonderildi));
        var okunan = (MesajGeldi?)await Okuyucu(PaketYazici.Serilestir(gonderilen)).OkuAsync(CancellationToken.None);
        Assert.AreEqual("Iğdır'da ılık bir gün", okunan!.Mesaj.Metin);
        Assert.AreEqual("Şükrü", okunan.Mesaj.GonderenAd);
    }

    [TestMethod]
    public async Task TurAlaniSondaOlsaDa_PaketTaninir()
    {
        var paket = await Okuyucu("{\"KullaniciAdi\":\"ali\",\"Sifre\":\"x\",\"tur\":\"GirisIstegi\"}\n").OkuAsync(CancellationToken.None);
        Assert.IsInstanceOfType<GirisIstegi>(paket);
    }

    [TestMethod]
    public async Task SatirSiniriAsilirsa_ProtokolHatasi()
    {
        string uzun = "{\"tur\":\"GirisIstegi\",\"KullaniciAdi\":\"" + new string('a', 2000) + "\",\"Sifre\":\"x\"}\n";
        await Assert.ThrowsExactlyAsync<ProtokolHatasi>(() => Okuyucu(uzun, sinir: 1000).OkuAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task SatirSonuGelmedenSinirAsilirsa_DeProtokolHatasi()
    {
        // Satir sonu hic gondermeden veri basan biri bellegi dolduramamali.
        await Assert.ThrowsExactlyAsync<ProtokolHatasi>(() => Okuyucu(new string('x', 5000), sinir: 1000).OkuAsync(CancellationToken.None));
    }

    [TestMethod]
    [DataRow("bu json degil\n")]
    [DataRow("{\"tur\":\"BoylePaketYok\"}\n")]
    [DataRow("{\"KullaniciAdi\":\"ali\"}\n")]
    [DataRow("{\"tur\":\"GirisIstegi\",\"KullaniciAdi\":\"ali\"}\n")]
    [DataRow("{\"tur\":\"GirisIstegi\",\"KullaniciAdi\":null,\"Sifre\":\"x\"}\n")]
    [DataRow("null\n")]
    public async Task BozukPaket_ProtokolHatasi(string satir)
    {
        await Assert.ThrowsExactlyAsync<ProtokolHatasi>(() => Okuyucu(satir).OkuAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task BaglantiSatirOrtasindaKapanirsa_ProtokolHatasi()
    {
        await Assert.ThrowsExactlyAsync<ProtokolHatasi>(() => Okuyucu("{\"tur\":\"Giris").OkuAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task ParcaParcaGelenVeri_DogruBirlestirilir()
    {
        // Ag verisi istedigi yerden bolunebilir: her ReadAsync tek bayt donduren akis.
        byte[] veri = PaketYazici.Serilestir(new GirisIstegi("ali", "sifre"));
        var okuyucu = new PaketOkuyucu(new TekBaytAkis(veri), 1024);
        Assert.IsInstanceOfType<GirisIstegi>(await okuyucu.OkuAsync(CancellationToken.None));
    }

    private sealed class TekBaytAkis(byte[] veri) : MemoryStream(veri)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> hedef, CancellationToken iptal = default) =>
            base.ReadAsync(hedef[..Math.Min(1, hedef.Length)], iptal);
    }
}
