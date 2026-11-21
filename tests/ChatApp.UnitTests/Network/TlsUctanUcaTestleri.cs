using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ChatApp.Protocol.Iletisim;
using ChatApp.Protocol.Kurallar;
using ChatApp.Protocol.Paketler;
using ChatApp.Server.Network;
using ChatApp.Server.Security;
using ChatApp.UnitTests.Fakes;

namespace ChatApp.UnitTests.Network;

// Gercek TcpListener + gercek TLS, localhost uzerinde. Veritabani yerine bellekteki sahte depolar.
// Amac: ag katmaninin (TLS el sikismasi, satir okuma, paket yonlendirme, kapanis) birlikte calistigini gormek.
[TestClass]
public sealed class TlsUctanUcaTestleri
{
    private static readonly TimeSpan Bekleme = TimeSpan.FromSeconds(10);

    private TestOrtami _o = null!;
    private TcpSunucu _sunucu = null!;
    private X509Certificate2 _sertifika = null!;
    private string _klasor = null!;

    [TestInitialize]
    public async Task Hazirla()
    {
        _o = new TestOrtami();
        await _o.HesapAcAsync("ali", "Ali");
        await _o.HesapAcAsync("ayse", "Ayse");

        _klasor = Path.Combine(Path.GetTempPath(), "chatapp-tls-" + Guid.NewGuid().ToString("N"));
        _sertifika = new SertifikaDeposu(_klasor, new SahteSirKoruyucu(), _o.Saat, _o.Gunlukcu).YukleVeyaOlustur();
        _sunucu = new TcpSunucu(_o.Oturumlar, _o.Isleyici, _o.Saat, _o.Gunlukcu);
        _sunucu.Baslat(0, _sertifika);   // 0: bos bir port sec
    }

    [TestCleanup]
    public async Task Temizle()
    {
        await _sunucu.DisposeAsync();
        _sertifika.Dispose();
        Directory.Delete(_klasor, recursive: true);
    }

    [TestMethod]
    public async Task IkiIstemci_TlsUzerindenMesajlasir()
    {
        await using var ali = await TestIstemcisi.BaglanAsync(_sunucu.Port, _sertifika);
        await using var ayse = await TestIstemcisi.BaglanAsync(_sunucu.Port, _sertifika);

        await ali.GonderAsync(new GirisIstegi("ali", TestOrtami.Sifre));
        var aliGiris = await ali.BekleAsync<GirisSonucu>();
        await ayse.GonderAsync(new GirisIstegi("ayse", TestOrtami.Sifre));
        var ayseGiris = await ayse.BekleAsync<GirisSonucu>();
        Assert.IsTrue(aliGiris.Basarili && ayseGiris.Basarili);

        await ali.GonderAsync(new MesajGonder(Guid.NewGuid(), ayseGiris.KullaniciId, "TLS ile merhaba ğüşıöç"));

        MesajGeldi gelen = await ayse.BekleAsync<MesajGeldi>();
        Assert.AreEqual("TLS ile merhaba ğüşıöç", gelen.Mesaj.Metin);
        Assert.AreEqual(aliGiris.KullaniciId, gelen.Mesaj.GonderenId);
        await ali.BekleAsync<MesajDurumuDegisti>();   // cift tik geldi
    }

    [TestMethod]
    public async Task TlsKurmayanIstemci_Reddedilir_SunucuCalismayaDevamEder()
    {
        using (var duz = new TcpClient())
        {
            await duz.ConnectAsync("127.0.0.1", _sunucu.Port);
            NetworkStream akis = duz.GetStream();
            await akis.WriteAsync(Encoding.UTF8.GetBytes("{\"tur\":\"GirisIstegi\"}\n"));
            byte[] tampon = new byte[256];
            // Sunucu TLS bekliyor; duz metne cevap vermez, baglantiyi kapatir.
            int okunan;
            try
            {
                okunan = await akis.ReadAsync(tampon).AsTask().WaitAsync(Bekleme);
            }
            catch (IOException)
            {
                okunan = 0;
            }
            Assert.AreEqual(0, okunan);
        }

        await using var ali = await TestIstemcisi.BaglanAsync(_sunucu.Port, _sertifika);
        await ali.GonderAsync(new GirisIstegi("ali", TestOrtami.Sifre));
        Assert.IsTrue((await ali.BekleAsync<GirisSonucu>()).Basarili);
    }

    [TestMethod]
    public async Task BozukPaketGonderen_BaglantisiKesilir_DigerleriEtkilenmez()
    {
        await using var ayse = await TestIstemcisi.BaglanAsync(_sunucu.Port, _sertifika);
        await ayse.GonderAsync(new GirisIstegi("ayse", TestOrtami.Sifre));
        await ayse.BekleAsync<GirisSonucu>();

        await using var bozuk = await TestIstemcisi.BaglanAsync(_sunucu.Port, _sertifika);
        await bozuk.HamGonderAsync("bu bir json degil\n");
        Assert.IsTrue(await bozuk.KapandiMiAsync());

        await using var ali = await TestIstemcisi.BaglanAsync(_sunucu.Port, _sertifika);
        await ali.GonderAsync(new GirisIstegi("ali", TestOrtami.Sifre));
        int aliId = (await ali.BekleAsync<GirisSonucu>()).KullaniciId!.Value;
        await ali.GonderAsync(new MesajGonder(Guid.NewGuid(), null, "genel"));
        Assert.AreEqual(aliId, (await ayse.BekleAsync<MesajGeldi>()).Mesaj.GonderenId);
    }

    [TestMethod]
    public async Task SunucuDurunca_IstemcilereKapanisBildirilir()
    {
        await using var ali = await TestIstemcisi.BaglanAsync(_sunucu.Port, _sertifika);
        await ali.GonderAsync(new GirisIstegi("ali", TestOrtami.Sifre));
        await ali.BekleAsync<GirisSonucu>();

        await _sunucu.DurdurAsync();

        OturumSonlandi son = await ali.BekleAsync<OturumSonlandi>();
        Assert.AreEqual(ChatApp.Protocol.Modeller.OturumSonuSebebi.SunucuKapandi, son.Sebep);
        Assert.IsEmpty(_o.Oturumlar.TumBaglantilar());
    }

    // Testler icin kucuk TLS istemcisi. Sunucu sertifikasini parmak izinden tanir (gercek istemcinin yaptigi gibi).
    private sealed class TestIstemcisi : IAsyncDisposable
    {
        private readonly TcpClient _tcp;
        private readonly SslStream _tls;
        private readonly PaketOkuyucu _okuyucu;

        private TestIstemcisi(TcpClient tcp, SslStream tls)
        {
            _tcp = tcp;
            _tls = tls;
            _okuyucu = new PaketOkuyucu(tls, ProtokolSinirlari.IstemcininOkuyacagiSatir);
        }

        public static async Task<TestIstemcisi> BaglanAsync(int port, X509Certificate2 beklenenSertifika)
        {
            var tcp = new TcpClient();
            await tcp.ConnectAsync("127.0.0.1", port);
            var tls = new SslStream(tcp.GetStream(), false,
                (_, sertifika, _, _) => sertifika is not null
                    && sertifika.GetCertHashString() == beklenenSertifika.GetCertHashString());
            await tls.AuthenticateAsClientAsync("ChatApp Sunucusu");
            return new TestIstemcisi(tcp, tls);
        }

        public async Task GonderAsync(Paket paket) => await _tls.WriteAsync(PaketYazici.Serilestir(paket));

        public async Task HamGonderAsync(string metin) => await _tls.WriteAsync(Encoding.UTF8.GetBytes(metin));

        // Istenen tipte paket gelene kadar okur (aradaki diger paketler atlanir).
        public async Task<T> BekleAsync<T>() where T : Paket
        {
            using var sure = new CancellationTokenSource(Bekleme);
            while (true)
            {
                Paket? paket = await _okuyucu.OkuAsync(sure.Token);
                if (paket is null)
                {
                    throw new AssertFailedException($"{typeof(T).Name} beklenirken baglanti kapandi.");
                }
                if (paket is T aranan)
                {
                    return aranan;
                }
            }
        }

        public async Task<bool> KapandiMiAsync()
        {
            using var sure = new CancellationTokenSource(Bekleme);
            try
            {
                while (await _okuyucu.OkuAsync(sure.Token) is not null)
                {
                }
                return true;
            }
            catch (IOException)
            {
                return true;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _tls.DisposeAsync();
            _tcp.Dispose();
        }
    }
}
