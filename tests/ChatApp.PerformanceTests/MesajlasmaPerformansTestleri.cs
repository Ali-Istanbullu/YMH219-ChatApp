using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using ChatApp.Client.Network;
using ChatApp.Client.Session;
using ChatApp.Client.Settings;
using ChatApp.Protocol.Paketler;
using ChatApp.Server.Network;
using ChatApp.Server.Security;
using ChatApp.UnitTests.Fakes;

namespace ChatApp.PerformanceTests;

// GERCEK TLS + GERCEK istemciler + GERCEK TcpSunucu (veritabani yerine bellekteki depolar), localhost.
[TestClass]
[TestCategory("Performans")]
public sealed class MesajlasmaPerformansTestleri
{
    // Esikler (milisaniye). Gelistirici bilgisayarinda olculen degerlerin birkac kati; asilirsa bir sey bozulmustur.
    private const int GenelOdaEsikMs = 5000;
    private const int OzelMesajMedyanEsikMs = 100;

    public TestContext TestContext { get; set; } = null!;

    private TestOrtami _o = null!;
    private TcpSunucu _sunucu = null!;
    private X509Certificate2 _sertifika = null!;
    private string _klasor = null!;
    private readonly List<SohbetIstemcisi> _istemciler = [];

    [TestInitialize]
    public void Hazirla()
    {
        _o = new TestOrtami();
        _klasor = Path.Combine(Path.GetTempPath(), "chatapp-perf-" + Guid.NewGuid().ToString("N"));
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

    // kisi sayisi kadar hesap acar ve hepsi gercek TLS ile girer. sayaclar[i]: i. kisiye gelen MesajGeldi sayisi.
    private async Task<(List<SohbetIstemcisi> Istemciler, int[] Sayaclar, List<int> Idler)> KisilerGirsinAsync(int kisiSayisi)
    {
        var sayaclar = new int[kisiSayisi];
        var idler = new List<int>();
        var istemciler = new List<SohbetIstemcisi>();
        var adres = new SunucuAdresi("127.0.0.1", _sunucu.Port);
        var parmakIzleri = new ParmakIziDeposu(Path.Combine(_klasor, "sunucular.json"), new SahteIstemciGunlukcu());

        for (int i = 0; i < kisiSayisi; i++)
        {
            idler.Add(await _o.HesapAcAsync($"kisi{i:00}", $"Kişi {i}"));
            if (i > 0 && i % PaketIsleyici.DakikadaEnFazlaGiris == 0)
            {
                _o.Saat.Ilerlet(TimeSpan.FromMinutes(1));   // ayni IP'den dakikada 10 giris siniri (sahte saat)
            }

            var istemci = new SohbetIstemcisi(new TlsBaglantiKurucu(parmakIzleri), new SahteIstemciGunlukcu());
            int sira = i;
            istemci.PaketGeldi += paket =>
            {
                if (paket is MesajGeldi)
                {
                    Interlocked.Increment(ref sayaclar[sira]);
                }
            };
            _istemciler.Add(istemci);
            istemciler.Add(istemci);
            Assert.IsTrue((await istemci.GirisYapAsync(adres, $"kisi{i:00}", TestOrtami.Sifre)).Basarili);
        }
        return (istemciler, sayaclar, idler);
    }

    [TestMethod]
    public async Task YirmiKisi_GenelOdayaBeserMesaj_HerkesHepsiniAlir()
    {
        const int KisiSayisi = 20;
        const int KisiBasinaMesaj = 5;   // saniyede 5 mesaj siniri
        (List<SohbetIstemcisi> istemciler, int[] sayaclar, _) = await KisilerGirsinAsync(KisiSayisi);
        int beklenen = (KisiSayisi - 1) * KisiBasinaMesaj;   // herkes, digerlerinin mesajlarini alir

        Stopwatch sure = Stopwatch.StartNew();
        await Task.WhenAll(istemciler.Select(async istemci =>
        {
            for (int j = 0; j < KisiBasinaMesaj; j++)
            {
                Assert.IsTrue(await istemci.GonderAsync(new MesajGonder(Guid.NewGuid(), null, $"Genel odaya {j}. mesaj: çğıöşü")));
            }
        }));
        while (sayaclar.Any(s => Volatile.Read(ref s) < beklenen) && sure.Elapsed < TimeSpan.FromSeconds(30))
        {
            await Task.Delay(5);
        }
        sure.Stop();

        int teslim = sayaclar.Sum();
        TestContext.WriteLine($"Genel oda: {KisiSayisi} kişi x {KisiBasinaMesaj} mesaj -> {teslim} teslim, " +
            $"{sure.ElapsedMilliseconds} ms ({teslim / Math.Max(sure.Elapsed.TotalSeconds, 0.001):N0} teslim/sn). Eşik {GenelOdaEsikMs} ms.");
        Assert.AreEqual(KisiSayisi * beklenen, teslim, "her mesaj herkese ulasmali");
        Assert.IsLessThan(GenelOdaEsikMs, sure.ElapsedMilliseconds);
    }

    [TestMethod]
    public async Task OzelMesaj_GondermedenAlmayaGecikme()
    {
        (List<SohbetIstemcisi> istemciler, int[] sayaclar, List<int> idler) = await KisilerGirsinAsync(2);
        var gecikmeler = new List<double>();

        for (int i = 0; i < 5; i++)   // saniyede 5 mesaj siniri
        {
            int onceki = Volatile.Read(ref sayaclar[1]);
            Stopwatch sure = Stopwatch.StartNew();
            Assert.IsTrue(await istemciler[0].GonderAsync(new MesajGonder(Guid.NewGuid(), idler[1], $"özel {i}")));
            while (Volatile.Read(ref sayaclar[1]) == onceki && sure.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(1);
            }
            gecikmeler.Add(sure.Elapsed.TotalMilliseconds);
        }

        double medyan = gecikmeler.Order().ElementAt(gecikmeler.Count / 2);
        TestContext.WriteLine($"Özel mesaj gecikmesi (ms): {string.Join(", ", gecikmeler.Select(g => g.ToString("N1")))}; " +
            $"medyan {medyan:N1}. Eşik {OzelMesajMedyanEsikMs} ms.");
        Assert.IsLessThan(OzelMesajMedyanEsikMs, medyan);
    }

    [TestMethod]
    public void SifreHashleme_GercekAyarla_GirisSuresi()
    {
        // Gercek ayar (600.000 tekrar): her giriste bir kez yapilir. Kaba kuvvet denemelerini yavaslatmak icin bilerek yavas.
        const int EsikMs = 3000;
        var hashleyici = new SifreHashleyici();

        Stopwatch sure = Stopwatch.StartNew();
        (byte[] hash, byte[] tuz) = hashleyici.Hashle("ornek-sifre-123");
        long hashMs = sure.ElapsedMilliseconds;
        sure.Restart();
        Assert.IsTrue(hashleyici.DogruMu("ornek-sifre-123", hash, tuz));
        long dogrulaMs = sure.ElapsedMilliseconds;

        TestContext.WriteLine($"Şifre hash: {hashMs} ms, doğrulama: {dogrulaMs} ms ({SifreHashleyici.VarsayilanTekrar:N0} tekrar). Eşik {EsikMs} ms.");
        Assert.IsLessThan(EsikMs, hashMs);
        Assert.IsLessThan(EsikMs, dogrulaMs);
    }
}
