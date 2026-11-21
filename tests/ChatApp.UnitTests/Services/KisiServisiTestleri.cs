using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;
using ChatApp.UnitTests.Fakes;

namespace ChatApp.UnitTests.Services;

[TestClass]
public sealed class KisiServisiTestleri
{
    private readonly TestOrtami _o = new();
    private int _aliId;
    private int _ayseId;

    [TestInitialize]
    public async Task Hazirla()
    {
        _aliId = await _o.HesapAcAsync("ali", "Ali");
        _ayseId = await _o.HesapAcAsync("ayse", "Ayse");
    }

    [TestMethod]
    public async Task GirisYapinca_SohbetListesiOkunmamisSayisiylaGelir()
    {
        SahteOturum ayse = await _o.GirisYapAsync("ayse");
        await _o.MesajAtAsync(ayse, _aliId, "bir");
        await _o.MesajAtAsync(ayse, _aliId, "iki");

        SahteOturum ali = await _o.GirisYapAsync("ali");

        KisiBilgisi kisi = ali.Paketler<SohbetListesi>().Single().Kisiler.Single();
        Assert.AreEqual(_ayseId, kisi.Id);
        Assert.AreEqual("Ayse", kisi.GorunenAd);
        Assert.AreEqual(2, kisi.OkunmamisSayisi);
        Assert.IsTrue(kisi.Cevrimici);
        Assert.IsFalse(kisi.Engelli);
    }

    [TestMethod]
    public async Task CevrimiciOlunca_KonustuguKisilereHaberGider()
    {
        await _o.Mesajlar.KaydetAsync(_aliId, _ayseId, "eski", _o.Saat.SimdiUtc);
        SahteOturum ayse = await _o.GirisYapAsync("ayse");

        SahteOturum ali = await _o.GirisYapAsync("ali");
        Assert.AreEqual(new CevrimiciDurumu(_aliId, true), ayse.Paketler<CevrimiciDurumu>().Single());

        await _o.Isleyici.BaglantiKapandiAsync(ali);
        Assert.AreEqual(new CevrimiciDurumu(_aliId, false), ayse.Paketler<CevrimiciDurumu>().Last());
    }

    [TestMethod]
    public async Task EngelledigimKisi_BeniHepCevrimdisiGorur()
    {
        await _o.Mesajlar.KaydetAsync(_aliId, _ayseId, "eski", _o.Saat.SimdiUtc);
        await _o.Engeller.EngelleAsync(_aliId, _ayseId, _o.Saat.SimdiUtc);
        SahteOturum ayse = await _o.GirisYapAsync("ayse");

        await _o.GirisYapAsync("ali");

        Assert.IsEmpty(ayse.Paketler<CevrimiciDurumu>());
        KullaniciBilgisi bilgi = await _o.Kisiler.KullaniciSorgulaAsync(_ayseId, _aliId);
        Assert.IsTrue(bilgi.Var);
        Assert.IsFalse(bilgi.Cevrimici);
    }

    [TestMethod]
    public async Task Engelleyince_KarsiTarafaCevrimdisiGorunurum_KaldirincaTekrarCevrimici()
    {
        SahteOturum ali = await _o.GirisYapAsync("ali");
        SahteOturum ayse = await _o.GirisYapAsync("ayse");

        await _o.Isleyici.IsleAsync(ali, new EngelleIstegi(_ayseId, true));
        Assert.IsTrue(ali.Paketler<IslemSonucu>().Single().Basarili);
        Assert.AreEqual(new CevrimiciDurumu(_aliId, false), ayse.Paketler<CevrimiciDurumu>().Last());

        await _o.Isleyici.IsleAsync(ali, new EngelleIstegi(_ayseId, false));
        Assert.AreEqual(new CevrimiciDurumu(_aliId, true), ayse.Paketler<CevrimiciDurumu>().Last());
    }

    [TestMethod]
    public async Task KullaniciSorgula_OlmayanVeKendisi_YokDoner()
    {
        Assert.IsFalse((await _o.Kisiler.KullaniciSorgulaAsync(_aliId, 111_111)).Var);
        Assert.IsFalse((await _o.Kisiler.KullaniciSorgulaAsync(_aliId, _aliId)).Var);
    }

    [TestMethod]
    public async Task OlmayanKisiyiEngelleme_Basarisiz()
    {
        IslemSonucu sonuc = await _o.Kisiler.EngelleAsync(_aliId, new EngelleIstegi(111_111, true));
        Assert.AreEqual(HataKodu.KullaniciBulunamadi, sonuc.Hata);
    }
}
