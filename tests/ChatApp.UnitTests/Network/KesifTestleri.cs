using System.Net;
using System.Net.Sockets;
using System.Text;
using ChatApp.Client.Network;
using ChatApp.Protocol.Kesif;
using ChatApp.Server.Network;
using ChatApp.UnitTests.Fakes;

namespace ChatApp.UnitTests.Network;

// Sunucuyu agda otomatik bulma: gercek UDP, localhost uzerinde (testler bos bir UDP portu kullanir).
[TestClass]
public sealed class KesifTestleri
{
    private static readonly TimeSpan KisaBekleme = TimeSpan.FromMilliseconds(700);

    private static int BosUdpPortu()
    {
        using var gecici = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)gecici.Client.LocalEndPoint!).Port;
    }

    [TestMethod]
    public async Task CalisanSunucu_BulunurPortuDogru()
    {
        int udpPortu = BosUdpPortu();
        await using var yanitlayici = new KesifYanitlayici(new SahteGunlukcu(), udpPortu);
        yanitlayici.Baslat(tcpPortu: 6060);

        var bulucu = new SunucuBulucu(new SahteIstemciGunlukcu(), udpPortu, KisaBekleme);
        IReadOnlyList<SunucuAdresi> bulunanlar = await bulucu.BulAsync();

        Assert.IsTrue(bulunanlar.Any(a => a.Port == 6060), "localhost'taki sunucu bulunmali");
    }

    [TestMethod]
    public async Task SunucuYoksa_BosListe_Takilmadan()
    {
        var bulucu = new SunucuBulucu(new SahteIstemciGunlukcu(), BosUdpPortu(), KisaBekleme);
        IReadOnlyList<SunucuAdresi> bulunanlar = await bulucu.BulAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.IsEmpty(bulunanlar);
    }

    [TestMethod]
    public async Task DurdurulanSunucu_ArtikBulunmaz()
    {
        int udpPortu = BosUdpPortu();
        var yanitlayici = new KesifYanitlayici(new SahteGunlukcu(), udpPortu);
        yanitlayici.Baslat(6060);
        await yanitlayici.DurdurAsync();
        Assert.IsFalse(yanitlayici.CalisiyorMu);

        var bulucu = new SunucuBulucu(new SahteIstemciGunlukcu(), udpPortu, KisaBekleme);
        Assert.IsEmpty(await bulucu.BulAsync());
    }

    [TestMethod]
    public void UdpPortuDoluysa_SocketException()
    {
        using var dolu = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        int port = ((IPEndPoint)dolu.Client.LocalEndPoint!).Port;
        var yanitlayici = new KesifYanitlayici(new SahteGunlukcu(), port);

        Assert.ThrowsExactly<SocketException>(() => yanitlayici.Baslat(6060));
        Assert.IsFalse(yanitlayici.CalisiyorMu);
    }

    [TestMethod]
    public void Protokol_SadeceGecerliIstekVeCevapKabulEdilir()
    {
        Assert.IsTrue(KesifProtokolu.IstekMi(KesifProtokolu.Istek));
        Assert.IsFalse(KesifProtokolu.IstekMi(Encoding.ASCII.GetBytes("CHATAPP-KESIF 2")));

        Assert.AreEqual(5050, KesifProtokolu.CevaptanPort(KesifProtokolu.CevapOlustur(5050)));
        Assert.IsNull(KesifProtokolu.CevaptanPort(Encoding.UTF8.GetBytes("{\"Uygulama\":\"Baska\",\"Port\":5050}")));
        Assert.IsNull(KesifProtokolu.CevaptanPort(Encoding.UTF8.GetBytes("{\"Uygulama\":\"ChatApp\",\"Port\":0}")));
        Assert.IsNull(KesifProtokolu.CevaptanPort(Encoding.UTF8.GetBytes("bozuk")));
        Assert.IsNull(KesifProtokolu.CevaptanPort(new byte[KesifProtokolu.EnFazlaPaket + 1]));
    }

    [TestMethod]
    [DataRow("192.168.1.20", "255.255.255.0", "192.168.1.255")]
    [DataRow("10.0.5.7", "255.255.0.0", "10.0.255.255")]
    [DataRow("172.16.3.4", "255.255.255.252", "172.16.3.7")]
    public void YayinAdresi_Hesaplanir(string adres, string maske, string beklenen)
    {
        Assert.AreEqual(IPAddress.Parse(beklenen), SunucuBulucu.YayinAdresi(IPAddress.Parse(adres), IPAddress.Parse(maske)));
    }
}
