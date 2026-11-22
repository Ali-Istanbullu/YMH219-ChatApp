using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;
using ChatApp.Server.Data;
using ChatApp.UnitTests.Fakes;

namespace ChatApp.UnitTests.Services;

// Mesaj/sohbet silme, ayni mesajin tekrar gelmesi ve yonetici kayitlari (Ali kararlari 22.11).
[TestClass]
public sealed class MesajSilmeTestleri
{
    private readonly TestOrtami _o = new();
    private int _aliId;
    private int _ayseId;
    private SahteOturum _ali = null!;
    private SahteOturum _ayse = null!;

    [TestInitialize]
    public async Task Hazirla()
    {
        _aliId = await _o.HesapAcAsync("ali", "Ali");
        _ayseId = await _o.HesapAcAsync("ayse", "Ayse");
        _ali = await _o.GirisYapAsync("ali");
        _ayse = await _o.GirisYapAsync("ayse");
    }

    private async Task<long> MesajAtAsync(SahteOturum kimden, int? kime, string metin)
    {
        await _o.MesajAtAsync(kimden, kime, metin);
        return _o.Mesajlar.Hepsi[^1].Id;
    }

    private async Task<IslemSonucu> SilAsync(SahteOturum kim, long mesajId, bool herkesten)
    {
        kim.Temizle();
        await _o.Isleyici.IsleAsync(kim, new MesajSilIstegi(mesajId, herkesten));
        return kim.Paketler<IslemSonucu>().Single();
    }

    private async Task<GecmisCevabi> GecmisAsync(SahteOturum kim, int? karsi) =>
        await _o.MesajServisi.GecmisAsync(kim.KullaniciId!.Value, new GecmisIstegi(karsi, null));

    [TestMethod]
    public async Task AyniMesajTekrarGelirse_BirKezKaydedilir_AliciyaBirKezGider()
    {
        var istemciId = Guid.NewGuid();
        await _o.Isleyici.IsleAsync(_ali, new MesajGonder(istemciId, _ayseId, "bir kez"));
        await _o.Isleyici.IsleAsync(_ali, new MesajGonder(istemciId, _ayseId, "bir kez"));   // baglanti kopunca tekrar

        Assert.HasCount(1, _o.Mesajlar.Hepsi);
        Assert.HasCount(1, _ayse.Paketler<MesajGeldi>());
        MesajKabulEdildi[] kabuller = [.. _ali.Paketler<MesajKabulEdildi>()];
        Assert.HasCount(2, kabuller);
        Assert.AreEqual(kabuller[0].Mesaj.Id, kabuller[1].Mesaj.Id);
    }

    [TestMethod]
    public async Task BendenSil_SadeceBendeGizlenir_KayitKalir()
    {
        long id = await MesajAtAsync(_ayse, _aliId, "gizlenecek");

        Assert.IsTrue((await SilAsync(_ali, id, herkesten: false)).Basarili);

        Assert.IsEmpty((await GecmisAsync(_ali, _ayseId)).Mesajlar);
        Assert.HasCount(1, (await GecmisAsync(_ayse, _aliId)).Mesajlar);
        Assert.AreEqual("gizlenecek", _o.Mesajlar.Hepsi.Single().Metin);
    }

    [TestMethod]
    public async Task HerkestenSil_KarsiyaBildirilir_MetinGitmez_VeritabanindaKalir()
    {
        long id = await MesajAtAsync(_ali, _ayseId, "pişman oldum");
        _ayse.Temizle();

        Assert.IsTrue((await SilAsync(_ali, id, herkesten: true)).Basarili);

        Assert.AreEqual(id, _ayse.Paketler<MesajHerkestenSilindi>().Single().MesajId);
        MesajBilgisi aysedeki = (await GecmisAsync(_ayse, _aliId)).Mesajlar.Single();
        Assert.IsTrue(aysedeki.HerkestenSilindi);
        Assert.AreEqual(string.Empty, aysedeki.Metin);
        Assert.AreEqual("pişman oldum", _o.Mesajlar.Hepsi.Single().Metin);
    }

    [TestMethod]
    public async Task GenelOdadaHerkestenSil_OdadakiHerkeseBildirilir()
    {
        long id = await MesajAtAsync(_ali, null, "genel");
        _ayse.Temizle();

        await SilAsync(_ali, id, herkesten: true);

        Assert.HasCount(1, _ayse.Paketler<MesajHerkestenSilindi>());
    }

    [TestMethod]
    public async Task BaskasininMesajiniHerkestenSilemez()
    {
        long id = await MesajAtAsync(_ayse, _aliId, "ayse'nin");

        IslemSonucu sonuc = await SilAsync(_ali, id, herkesten: true);

        Assert.IsFalse(sonuc.Basarili);
        Assert.AreEqual(HataKodu.SadeceKendiMesajin, sonuc.Hata);
        Assert.IsFalse(_o.Mesajlar.Hepsi.Single().HerkestenSilindi);
    }

    [TestMethod]
    public async Task TarafiOlmadigiOzelMesajiSilemez()
    {
        int veli = await _o.HesapAcAsync("veli", "Veli");
        long id = await MesajAtAsync(_ayse, veli, "ozel");

        IslemSonucu sonuc = await SilAsync(_ali, id, herkesten: false);

        Assert.AreEqual(HataKodu.MesajBulunamadi, sonuc.Hata);
        Assert.AreEqual(HataKodu.MesajBulunamadi, (await SilAsync(_ali, 999_999, herkesten: false)).Hata);
    }

    [TestMethod]
    public async Task SohbetSil_SadeceBendenGider_YeniMesajlaGeriGelir()
    {
        await MesajAtAsync(_ayse, _aliId, "eski 1");
        await MesajAtAsync(_ali, _ayseId, "eski 2");

        _ali.Temizle();
        await _o.Isleyici.IsleAsync(_ali, new SohbetSilIstegi(_ayseId));
        Assert.IsTrue(_ali.Paketler<IslemSonucu>().Single().Basarili);

        Assert.IsEmpty((await GecmisAsync(_ali, _ayseId)).Mesajlar);
        Assert.IsEmpty((await _o.Kisiler.SohbetListesiAsync(_aliId)).Kisiler);
        Assert.HasCount(2, (await GecmisAsync(_ayse, _aliId)).Mesajlar);

        await MesajAtAsync(_ayse, _aliId, "yeni");
        Assert.AreEqual("yeni", (await GecmisAsync(_ali, _ayseId)).Mesajlar.Single().Metin);
        Assert.HasCount(1, (await _o.Kisiler.SohbetListesiAsync(_aliId)).Kisiler);
        Assert.HasCount(3, _o.Mesajlar.Hepsi);
    }

    [TestMethod]
    public async Task Yonetici_SilinenVeGizlenenleriMetniyleGorur()
    {
        long gizlenen = await MesajAtAsync(_ayse, _aliId, "gizlenen");
        long silinen = await MesajAtAsync(_ali, _ayseId, "silinen");
        await SilAsync(_ali, gizlenen, herkesten: false);
        await SilAsync(_ali, silinen, herkesten: true);

        YonetimSohbetOzeti ozet = (await _o.Yonetim.KullaniciSohbetleriAsync(_aliId)).Single();
        Assert.AreEqual(_ayseId, ozet.KarsiId);
        Assert.AreEqual(2, ozet.MesajSayisi);

        IReadOnlyList<YonetimMesajKaydi> kayitlar = await _o.Yonetim.OzelKayitlarAsync(_aliId, _ayseId, null);
        CollectionAssert.AreEqual(new[] { gizlenen, silinen }, kayitlar.Select(k => k.Mesaj.Id).ToArray(), "eskiden yeniye");
        CollectionAssert.AreEqual(new[] { _aliId }, kayitlar[0].Gizleyenler.ToArray());
        Assert.AreEqual("silinen", kayitlar[1].Mesaj.Metin);
        Assert.IsTrue(kayitlar[1].Mesaj.HerkestenSilindi);
    }
}
