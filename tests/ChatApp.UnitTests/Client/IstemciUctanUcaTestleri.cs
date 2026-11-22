using System.Collections.Concurrent;
using System.Security.Cryptography.X509Certificates;
using ChatApp.Client.Network;
using ChatApp.Client.Presentation;
using ChatApp.Client.Session;
using ChatApp.Client.Settings;
using ChatApp.Client.State;
using ChatApp.Protocol.Modeller;
using ChatApp.Server.Network;
using ChatApp.Server.Security;
using ChatApp.UnitTests.Fakes;

namespace ChatApp.UnitTests.Client;

// GERCEK istemci (TLS, parmak izi, yeniden baglanma, presenter) + GERCEK TcpSunucu, localhost uzerinde.
// Veritabani yerine sunucunun bellekteki sahte depolari kullanilir.
[TestClass]
public sealed class IstemciUctanUcaTestleri
{
    private static readonly TimeSpan Bekleme = TimeSpan.FromSeconds(10);

    private TestOrtami _o = null!;
    private TcpSunucu _sunucu = null!;
    private X509Certificate2 _sertifika = null!;
    private string _klasor = null!;
    private int _aliId;
    private int _ayseId;
    private readonly List<SohbetIstemcisi> _istemciler = [];

    [TestInitialize]
    public async Task Hazirla()
    {
        _o = new TestOrtami();
        _aliId = await _o.HesapAcAsync("ali", "Ali");
        _ayseId = await _o.HesapAcAsync("ayse", "Ayse");

        _klasor = Path.Combine(Path.GetTempPath(), "chatapp-e2e-" + Guid.NewGuid().ToString("N"));
        _sertifika = new SertifikaDeposu(_klasor, new SahteSirKoruyucu(), _o.Saat, _o.Gunlukcu).YukleVeyaOlustur();
        _sunucu = new TcpSunucu(_o.Oturumlar, _o.Isleyici, _o.Saat, _o.Gunlukcu);
        _sunucu.Baslat(0, _sertifika);
    }

    [TestCleanup]
    public async Task Temizle()
    {
        foreach (SohbetIstemcisi istemci in _istemciler)
        {
            await istemci.DisposeAsync();
        }
        await _sunucu.DisposeAsync();
        _sertifika.Dispose();
        Directory.Delete(_klasor, recursive: true);
    }

    private SunucuAdresi Adres => new("127.0.0.1", _sunucu.Port);

    private ParmakIziDeposu YeniParmakIziDeposu() =>
        new(Path.Combine(_klasor, "istemci-" + Guid.NewGuid().ToString("N"), "sunucular.json"), new SahteIstemciGunlukcu());

    private SohbetIstemcisi YeniIstemci(ParmakIziDeposu? depo = null, TimeSpan? yenidenBaglanmaBeklemesi = null)
    {
        var kurucu = new TlsBaglantiKurucu(depo ?? YeniParmakIziDeposu());
        SohbetIstemcisi istemci = yenidenBaglanmaBeklemesi is { } sure
            ? new SohbetIstemcisi(kurucu, new SahteIstemciGunlukcu(), (_, iptal) => Task.Delay(sure, iptal))
            : new SohbetIstemcisi(kurucu, new SahteIstemciGunlukcu());
        _istemciler.Add(istemci);
        return istemci;
    }

    private async Task<SohbetYoneticisi> GirisYapmisYoneticiAsync(string kullaniciAdi)
    {
        SohbetIstemcisi istemci = YeniIstemci();
        var yonetici = new SohbetYoneticisi(istemci, new SahteIstemciGunlukcu(), TimeProvider.System);
        Assert.IsTrue((await istemci.GirisYapAsync(Adres, kullaniciAdi, TestOrtami.Sifre)).Basarili);
        await yonetici.OturumuBaslatAsync();
        return yonetici;
    }

    // Olaylar baska is parcaciklarindan gelir; kosul saglanana kadar beklenir.
    private static async Task KosulBekleAsync(Func<bool> kosul, string aciklama)
    {
        DateTime son = DateTime.UtcNow + Bekleme;
        while (true)
        {
            try
            {
                if (kosul())
                {
                    return;
                }
            }
            catch (Exception hata) when (hata is InvalidOperationException or NullReferenceException)
            {
                // liste o an degisiyordu; tekrar bak
            }
            if (DateTime.UtcNow > son)
            {
                Assert.Fail("Olusmadi: " + aciklama);
            }
            await Task.Delay(20);
        }
    }

    [TestMethod]
    public async Task OzelMesaj_TiklerBildirimVeOkunduBilgisi_UctanUca()
    {
        SohbetYoneticisi ali = await GirisYapmisYoneticiAsync("ali");
        SohbetYoneticisi ayse = await GirisYapmisYoneticiAsync("ayse");
        var ayseBildirimleri = new ConcurrentQueue<MesajSatiri>();
        ayse.BildirimGerekli += ayseBildirimleri.Enqueue;
        await ayse.PencereAktifligiDegistiAsync(false);   // Ayse'nin penceresi arka planda

        Assert.IsNull(await ali.YeniSohbetAsync(_ayseId.ToString()));
        Assert.AreEqual(_ayseId, ali.AcikSohbet!.KarsiId);
        Assert.IsNull(await ali.MesajGonderAsync("merhaba Ayşe"));

        MesajSatiri aliMesaji = ali.AcikSohbet.Mesajlar.Single();
        await KosulBekleAsync(() => aliMesaji.Durum == MesajDurumu.Iletildi, "Ali cift tik gormeli");
        await KosulBekleAsync(() => ayseBildirimleri.Count == 1, "Ayse bildirim almali");
        Sohbet ayseninSohbeti = ayse.Durum!.Bul(_aliId)!;
        Assert.AreEqual("merhaba Ayşe", ayseninSohbeti.Mesajlar.Last().Metin);
        Assert.AreEqual(1, ayseninSohbeti.Okunmamis);

        await ayse.SohbetAcAsync(ayseninSohbeti);
        await ayse.PencereAktifligiDegistiAsync(true);

        await KosulBekleAsync(() => aliMesaji.Durum == MesajDurumu.Okundu, "Ali mavi tik gormeli");
        Assert.AreEqual(0, ayseninSohbeti.Okunmamis);
    }

    [TestMethod]
    public async Task GenelOda_Mesaji_HerkeseGider()
    {
        SohbetYoneticisi ali = await GirisYapmisYoneticiAsync("ali");
        SohbetYoneticisi ayse = await GirisYapmisYoneticiAsync("ayse");

        Assert.IsNull(await ali.MesajGonderAsync("herkese selam"));

        await KosulBekleAsync(() => ayse.Durum!.Genel.Mesajlar.Any(m => m.Metin == "herkese selam"), "Ayse Genel'de gormeli");
        await KosulBekleAsync(() => ali.Durum!.Genel.Mesajlar.Single().Id is not null, "Ali'nin mesaji kabul edilmeli");
    }

    [TestMethod]
    public async Task HerkestenSil_KarsidaSilindiGorunur_SohbetSil_SadeceBendeGider()
    {
        SohbetYoneticisi ali = await GirisYapmisYoneticiAsync("ali");
        SohbetYoneticisi ayse = await GirisYapmisYoneticiAsync("ayse");
        Assert.IsNull(await ali.YeniSohbetAsync(_ayseId.ToString()));
        Assert.IsNull(await ali.MesajGonderAsync("yanlislikla yazdim"));
        MesajSatiri aliMesaji = ali.AcikSohbet!.Mesajlar.Single();
        await KosulBekleAsync(() => ayse.Durum!.Bul(_aliId)?.Mesajlar.Count == 1 && aliMesaji.Id is not null, "mesaj ulasmali");

        Assert.IsNull(await ali.MesajSilAsync(aliMesaji, herkesten: true));

        MesajSatiri aysedeki = ayse.Durum!.Bul(_aliId)!.Mesajlar.Single();
        await KosulBekleAsync(() => aysedeki.HerkestenSilindi, "Ayse'de 'silindi' gorunmeli");
        Assert.AreEqual("yanlislikla yazdim", _o.Mesajlar.Hepsi.Single().Metin, "veritabaninda kalir");

        await ayse.SohbetAcAsync(ayse.Durum.Bul(_aliId)!);
        Assert.IsNull(await ayse.SohbetSilAsync());
        Assert.IsNull(ayse.Durum.Bul(_aliId));
        Assert.HasCount(1, ali.AcikSohbet!.Mesajlar, "Ali'de sohbet duruyor");
    }

    [TestMethod]
    public async Task ParmakIzi_IlkBaglantidaHatirlanir_DegisirseSertifikaDegistiHatasi()
    {
        ParmakIziDeposu depo = YeniParmakIziDeposu();
        SohbetIstemcisi istemci = YeniIstemci(depo);
        Assert.IsTrue((await istemci.GirisYapAsync(Adres, "ali", TestOrtami.Sifre)).Basarili);
        string gercek = SertifikaDeposu.ParmakIzi(_sertifika);
        Assert.AreEqual(gercek, depo.Bul(Adres));
        await istemci.CikisYapAsync();

        depo.Kaydet(Adres, "00FF00FF");   // "baska bir sunucu" hatirlaniyormus gibi

        SertifikaDegistiHatasi hata = await Assert.ThrowsExactlyAsync<SertifikaDegistiHatasi>(
            () => istemci.GirisYapAsync(Adres, "ali", TestOrtami.Sifre));
        Assert.AreEqual("00FF00FF", hata.EskiParmakIzi);
        Assert.AreEqual(gercek, hata.YeniParmakIzi);
    }

    [TestMethod]
    public async Task KapaliPort_TurkceBaglantiHatasi()
    {
        SohbetIstemcisi istemci = YeniIstemci();
        BaglantiHatasi hata = await Assert.ThrowsExactlyAsync<BaglantiHatasi>(
            () => istemci.GirisYapAsync(new SunucuAdresi("127.0.0.1", 1), "ali", TestOrtami.Sifre));
        Assert.IsFalse(string.IsNullOrWhiteSpace(hata.Message));
    }

    [TestMethod]
    public async Task SunucuYenidenBaslayinca_IstemciKendiliginden_YenidenBaglanir()
    {
        SohbetIstemcisi istemci = YeniIstemci(yenidenBaglanmaBeklemesi: TimeSpan.FromMilliseconds(50));
        var durumlar = new ConcurrentQueue<BaglantiDurumu>();
        istemci.DurumDegisti += (d, _) => durumlar.Enqueue(d);
        Assert.IsTrue((await istemci.GirisYapAsync(Adres, "ali", TestOrtami.Sifre)).Basarili);
        int port = _sunucu.Port;

        await _sunucu.DurdurAsync();
        await KosulBekleAsync(() => durumlar.Contains(BaglantiDurumu.YenidenBaglaniyor), "kopma fark edilmeli");
        _sunucu.Baslat(port, _sertifika);

        await KosulBekleAsync(() => istemci.Durum == BaglantiDurumu.Bagli && durumlar.Last() == BaglantiDurumu.Bagli, "yeniden baglanmali");
        await KosulBekleAsync(() => _o.Oturumlar.CevrimiciMi(_aliId), "sunucu Ali'yi cevrimici gormeli");
    }
}
