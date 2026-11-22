using System.Collections.Concurrent;
using ChatApp.Client.Network;
using ChatApp.Client.Presentation;
using ChatApp.Client.Session;
using ChatApp.Client.State;
using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;
using ChatApp.UnitTests.Fakes;

namespace ChatApp.UnitTests.Client;

[TestClass]
public sealed class SohbetYoneticisiTestleri
{
    private static readonly SunucuAdresi Adres = new("localhost", 5050);

    private readonly SahteBaglantiKurucu _kurucu = new();
    private readonly SahteIstemciGunlukcu _gunlukcu = new();
    private readonly ConcurrentQueue<TaskCompletionSource> _beklemeler = new();
    private SohbetIstemcisi _istemci = null!;
    private SohbetYoneticisi _yonetici = null!;

    [TestInitialize]
    public async Task Hazirla()
    {
        // Yeniden baglanma beklemesi, test "devam" diyene kadar surer (kopukken mesaj yazabilmek icin).
        _istemci = new SohbetIstemcisi(_kurucu, _gunlukcu, (_, iptal) =>
        {
            var kapi = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            iptal.Register(() => kapi.TrySetCanceled());
            _beklemeler.Enqueue(kapi);
            return kapi.Task;
        });
        _yonetici = new SohbetYoneticisi(_istemci, _gunlukcu, TimeProvider.System);
        Assert.IsTrue((await _istemci.GirisYapAsync(Adres, "ali", "sifre-123")).Basarili);
        await _yonetici.OturumuBaslatAsync();
    }

    [TestCleanup]
    public async Task Temizle() => await _istemci.DisposeAsync();

    private static async Task KosulBekleAsync(Func<bool> kosul)
    {
        DateTime son = DateTime.UtcNow.AddSeconds(5);
        while (!kosul())
        {
            if (DateTime.UtcNow > son)
            {
                Assert.Fail("Beklenen durum olusmadi.");
            }
            await Task.Delay(10);
        }
    }

    [TestMethod]
    public async Task KopukkenYazilanMesaj_BaglantiBekler_BaglantiGelinceKendiligindenGider()
    {
        SahteBaglanti ilk = _kurucu.Acilanlar.Single();
        ilk.Kopar();
        await KosulBekleAsync(() => _istemci.Durum == BaglantiDurumu.YenidenBaglaniyor && !_beklemeler.IsEmpty);

        Assert.IsNull(await _yonetici.MesajGonderAsync("kopukken yazdim"));
        MesajSatiri satir = _yonetici.Durum!.Genel.Mesajlar.Single();
        Assert.IsTrue(satir.BaglantiBekliyor);
        Assert.IsNull(satir.Hata, "baglanti yuzunden: hata degil, bekliyor");

        _beklemeler.Single().SetResult();   // yeniden baglanma denemesi baslasin

        await KosulBekleAsync(() => satir.Id is not null);
        Assert.IsFalse(satir.BaglantiBekliyor);
        SahteBaglanti ikinci = _kurucu.Acilanlar.Last();
        Assert.AreEqual("kopukken yazdim", ikinci.Gonderilenler.OfType<MesajGonder>().Single().Metin);
    }

    [TestMethod]
    public async Task ReddedilenMesaj_TekrarGonderIleAyniNumarayla_YenidenGider()
    {
        SahteBaglanti baglanti = _kurucu.Acilanlar.Single();
        Func<Paket, IEnumerable<Paket>> normal = baglanti.Cevaplayici;
        baglanti.Cevaplayici = p => p is MesajGonder m ? [new MesajReddedildi(m.IstemciMesajId, HataKodu.HizSiniriAsildi)] : normal(p);

        await _yonetici.MesajGonderAsync("hizli");
        MesajSatiri satir = _yonetici.Durum!.Genel.Mesajlar.Single();
        await KosulBekleAsync(() => satir.Hata is not null);

        baglanti.Cevaplayici = normal;
        await _yonetici.TekrarGonderAsync(satir);

        await KosulBekleAsync(() => satir.Id is not null);
        MesajGonder[] gidenler = [.. baglanti.Gonderilenler.OfType<MesajGonder>()];
        Assert.HasCount(2, gidenler);
        Assert.AreEqual(gidenler[0].IstemciMesajId, gidenler[1].IstemciMesajId);
    }

    [TestMethod]
    public async Task GonderilmemisMesajiSilmek_SunucuyaGitmez_ArtikGonderilmez()
    {
        _kurucu.Acilanlar.Single().Kopar();
        await KosulBekleAsync(() => !_beklemeler.IsEmpty);
        await _yonetici.MesajGonderAsync("vazgectim");
        MesajSatiri satir = _yonetici.Durum!.Genel.Mesajlar.Single();

        Assert.IsNull(await _yonetici.MesajSilAsync(satir, herkesten: false));
        Assert.IsEmpty(_yonetici.Durum.Genel.Mesajlar);

        _beklemeler.Single().SetResult();
        await KosulBekleAsync(() => _istemci.Durum == BaglantiDurumu.Bagli && _kurucu.Acilanlar.Count == 2);
        await Task.Delay(100);
        Assert.IsEmpty(_kurucu.Acilanlar.Last().Gonderilenler.OfType<MesajGonder>());
    }

    [TestMethod]
    public async Task BaskasininMesajiniHerkestenSilme_SunucuyaSorulmadanReddedilir()
    {
        _kurucu.Acilanlar.Single().Gelsin(new MesajGeldi(
            new MesajBilgisi(50, 222222, "Ayse", null, "ayse yazdi", DateTimeOffset.UnixEpoch, MesajDurumu.Gonderildi)));
        await KosulBekleAsync(() => _yonetici.Durum!.Genel.Mesajlar.Count == 1);

        string? hata = await _yonetici.MesajSilAsync(_yonetici.Durum!.Genel.Mesajlar[0], herkesten: true);

        Assert.IsNotNull(hata);
        Assert.IsEmpty(_kurucu.Acilanlar.Single().Gonderilenler.OfType<MesajSilIstegi>());
    }
}
