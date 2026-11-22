using System.Collections.Concurrent;
using ChatApp.Client.Network;
using ChatApp.Client.Session;
using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;
using ChatApp.UnitTests.Fakes;

namespace ChatApp.UnitTests.Client;

[TestClass]
public sealed class SohbetIstemcisiTestleri
{
    private static readonly SunucuAdresi Adres = new("localhost", 5050);
    private static readonly TimeSpan Bekleme = TimeSpan.FromSeconds(5);

    private readonly SahteBaglantiKurucu _kurucu = new();
    private readonly SahteIstemciGunlukcu _gunlukcu = new();
    private readonly ConcurrentQueue<TimeSpan> _beklemeler = new();
    private readonly ConcurrentQueue<(BaglantiDurumu Durum, string? Aciklama)> _durumlar = new();
    private SohbetIstemcisi _istemci = null!;

    [TestInitialize]
    public void Hazirla()
    {
        // Yeniden baglanma beklemeleri kaydedilir ama gercekten beklenmez.
        _istemci = new SohbetIstemcisi(_kurucu, _gunlukcu, (sure, _) =>
        {
            _beklemeler.Enqueue(sure);
            return Task.CompletedTask;
        });
        _istemci.DurumDegisti += (d, a) => _durumlar.Enqueue((d, a));
    }

    [TestCleanup]
    public async Task Temizle() => await _istemci.DisposeAsync();

    private static async Task KosulBekleAsync(Func<bool> kosul)
    {
        DateTime son = DateTime.UtcNow + Bekleme;
        while (!kosul())
        {
            if (DateTime.UtcNow > son)
            {
                Assert.Fail("Beklenen durum olusmadi.");
            }
            await Task.Delay(10);
        }
    }

    private SahteBaglanti IlkBaglanti => _kurucu.Acilanlar.First();

    [TestMethod]
    public async Task Giris_Basarili_DurumBagli_SonrakiPaketlerOlayaGelir()
    {
        var gelenler = new ConcurrentQueue<Paket>();
        _istemci.PaketGeldi += gelenler.Enqueue;

        GirisSonucu sonuc = await _istemci.GirisYapAsync(Adres, "ali", "dogru-sifre");

        Assert.IsTrue(sonuc.Basarili);
        Assert.AreEqual(BaglantiDurumu.Bagli, _istemci.Durum);
        Assert.AreEqual(SahteSunucu.AliId, _istemci.KullaniciId);

        IlkBaglanti.Gelsin(new CevrimiciDurumu(5, true));
        await KosulBekleAsync(() => gelenler.Count == 1);
        Assert.IsInstanceOfType<CevrimiciDurumu>(gelenler.Single());
    }

    [TestMethod]
    public async Task YanlisSifre_Basarisiz_BaglantiKapanir_DurumKapaliKalir()
    {
        GirisSonucu sonuc = await _istemci.GirisYapAsync(Adres, "ali", "yanlis");

        Assert.IsFalse(sonuc.Basarili);
        Assert.AreEqual(HataKodu.KullaniciAdiVeyaSifreYanlis, sonuc.Hata);
        Assert.AreEqual(BaglantiDurumu.Kapali, _istemci.Durum);
        Assert.IsTrue(IlkBaglanti.Kapatildi);
    }

    [TestMethod]
    public async Task BaglanilamazsaBaglantiHatasi_Yukari_Cikar()
    {
        _kurucu.SonrakiHataVersin(new BaglantiHatasi("sunucu yok"));
        await Assert.ThrowsExactlyAsync<BaglantiHatasi>(() => _istemci.GirisYapAsync(Adres, "ali", "x"));
        Assert.AreEqual(BaglantiDurumu.Kapali, _istemci.Durum);
    }

    [TestMethod]
    public async Task BaglantiKoparsa_AyniKullaniciyla_YenidenBaglanir()
    {
        await _istemci.GirisYapAsync(Adres, "ali", "dogru-sifre");

        IlkBaglanti.Kopar();

        await KosulBekleAsync(() => _kurucu.Acilanlar.Count == 2 && _istemci.Durum == BaglantiDurumu.Bagli);
        GirisIstegi ikinciGiris = _kurucu.Acilanlar.Last().Gonderilenler.OfType<GirisIstegi>().Single();
        Assert.AreEqual(new GirisIstegi("ali", "dogru-sifre"), ikinciGiris);
        Assert.IsTrue(_durumlar.Any(d => d.Durum == BaglantiDurumu.YenidenBaglaniyor));
    }

    [TestMethod]
    public async Task YenidenBaglanamazsa_ArtanAraliklarlaDener_2_4_8_16()
    {
        await _istemci.GirisYapAsync(Adres, "ali", "dogru-sifre");
        _kurucu.SonrakiHataVersin(new BaglantiHatasi("yok"));
        _kurucu.SonrakiHataVersin(new BaglantiHatasi("yok"));
        _kurucu.SonrakiHataVersin(new BaglantiHatasi("yok"));

        IlkBaglanti.Kopar();

        await KosulBekleAsync(() => _kurucu.DenemeSayisi == 5 && _istemci.Durum == BaglantiDurumu.Bagli);
        CollectionAssert.AreEqual(new[] { 2.0, 4.0, 8.0, 16.0 }, _beklemeler.Select(b => b.TotalSeconds).ToArray());
    }

    [TestMethod]
    public async Task Atildiysa_YenidenBaglanmaz_SebepleKapanir()
    {
        await _istemci.GirisYapAsync(Adres, "ali", "dogru-sifre");

        IlkBaglanti.Gelsin(new OturumSonlandi(OturumSonuSebebi.Atildi, null));

        await KosulBekleAsync(() => _istemci.Durum == BaglantiDurumu.Kapali);
        Assert.AreEqual(1, _kurucu.DenemeSayisi);
        Assert.Contains("Yönetici", _durumlar.Last().Aciklama!);
    }

    [TestMethod]
    public async Task SunucuKapandiysa_YenidenBaglanir()
    {
        await _istemci.GirisYapAsync(Adres, "ali", "dogru-sifre");

        IlkBaglanti.Gelsin(new OturumSonlandi(OturumSonuSebebi.SunucuKapandi, null));

        await KosulBekleAsync(() => _kurucu.Acilanlar.Count == 2 && _istemci.Durum == BaglantiDurumu.Bagli);
    }

    [TestMethod]
    public async Task YenidenGirisYasakliysa_OturumSebebiyleBiter()
    {
        await _istemci.GirisYapAsync(Adres, "ali", "dogru-sifre");
        _kurucu.SonrakiBuOlsun(new SahteBaglanti(p => p is GirisIstegi
            ? [new GirisSonucu(false, null, null, HataKodu.HesapYasakli, "spam")]
            : []));

        IlkBaglanti.Kopar();

        await KosulBekleAsync(() => _istemci.Durum == BaglantiDurumu.Kapali);
        string aciklama = _durumlar.Last().Aciklama!;
        Assert.Contains("yasaklandı", aciklama);
        Assert.Contains("spam", aciklama);
    }

    [TestMethod]
    public async Task SifreDegisince_YenidenBaglanmaYeniSifreyleYapilir()
    {
        await _istemci.GirisYapAsync(Adres, "ali", "eski-sifre");
        _istemci.SifreDegisti("yeni-sifre");

        IlkBaglanti.Kopar();

        await KosulBekleAsync(() => _kurucu.Acilanlar.Count == 2 && _istemci.Durum == BaglantiDurumu.Bagli);
        Assert.AreEqual("yeni-sifre", _kurucu.Acilanlar.Last().Gonderilenler.OfType<GirisIstegi>().Single().Sifre);
    }

    [TestMethod]
    public async Task IsteAsync_EslesenCevabiDoner()
    {
        await _istemci.GirisYapAsync(Adres, "ali", "dogru-sifre");

        KullaniciBilgisi? bilgi = await _istemci.IsteAsync<KullaniciBilgisi>(new KullaniciSorgula(777777), k => k.KullaniciId == 777777);

        Assert.IsNotNull(bilgi);
        Assert.AreEqual("Kisi777777", bilgi.GorunenAd);
    }

    [TestMethod]
    public async Task IsteAsync_SunucuHataBildirirse_NullDoner()
    {
        await _istemci.GirisYapAsync(Adres, "ali", "dogru-sifre");
        IlkBaglanti.Cevaplayici = p => p is KullaniciSorgula ? [new HataBildirimi(HataKodu.SunucuHatasi, "db")] : [];

        Assert.IsNull(await _istemci.IsteAsync<KullaniciBilgisi>(new KullaniciSorgula(1), _ => true));
    }

    [TestMethod]
    public async Task CikisYapinca_BaglantiKapanir_AciklamasizKapali_YenidenBaglanmaz()
    {
        await _istemci.GirisYapAsync(Adres, "ali", "dogru-sifre");

        await _istemci.CikisYapAsync();

        Assert.AreEqual(BaglantiDurumu.Kapali, _istemci.Durum);
        Assert.IsNull(_durumlar.Last().Aciklama);
        Assert.IsTrue(IlkBaglanti.Kapatildi);
        await Task.Delay(100);
        Assert.AreEqual(1, _kurucu.DenemeSayisi);
        Assert.IsFalse(await _istemci.GonderAsync(new KullaniciSorgula(1)));
    }
}
