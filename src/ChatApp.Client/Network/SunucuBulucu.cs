using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using ChatApp.Protocol.Kesif;
using ChatApp.Client.Abstractions;

namespace ChatApp.Client.Network;

// Ayni agdaki ChatServer'i bulur (Ali karari: son kullanici adres yazmaz).
// Her ag kartinin yayin adresine (ornek 192.168.1.255) ve bu bilgisayara (127.0.0.1) "ChatServer var mi?" sorar,
// kisa bir sure cevap toplar. 255.255.255.255 tek basina yetmez: Docker/WSL gibi sanal kartlar varsa
// Windows yayini yanlis karttan gonderebilir.
public sealed class SunucuBulucu
{
    private static readonly TimeSpan VarsayilanBekleme = TimeSpan.FromSeconds(2);

    private readonly IGunlukcu _gunlukcu;
    private readonly int _udpPortu;
    private readonly TimeSpan _bekleme;

    public SunucuBulucu(IGunlukcu gunlukcu) : this(gunlukcu, KesifProtokolu.VarsayilanPort, VarsayilanBekleme)
    {
    }

    // Testler baska port ve kisa bekleme kullanabilsin diye.
    internal SunucuBulucu(IGunlukcu gunlukcu, int udpPortu, TimeSpan bekleme)
    {
        ArgumentNullException.ThrowIfNull(gunlukcu);
        _gunlukcu = gunlukcu;
        _udpPortu = udpPortu;
        _bekleme = bekleme;
    }

    // Bulunanlar (ayni sunucu birden cok yoldan cevap verirse bir kez). Hic yoksa bos liste.
    // Ag kullanilamiyorsa da bos liste (hata loga yazilir, kullaniciya "bulunamadi" denir).
    public async Task<IReadOnlyList<SunucuAdresi>> BulAsync(CancellationToken iptal = default)
    {
        var bulunanlar = new List<SunucuAdresi>();
        try
        {
            using var udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0)) { EnableBroadcast = true };
            foreach (IPAddress hedef in Hedefler())
            {
                try
                {
                    await udp.SendAsync(KesifProtokolu.Istek, new IPEndPoint(hedef, _udpPortu), iptal);
                }
                catch (SocketException hata)
                {
                    _gunlukcu.Uyari($"Kesif istegi gonderilemedi ({hedef}): {hata.SocketErrorCode}");
                }
            }

            using var sure = CancellationTokenSource.CreateLinkedTokenSource(iptal);
            sure.CancelAfter(_bekleme);
            while (true)
            {
                UdpReceiveResult cevap;
                try
                {
                    cevap = await udp.ReceiveAsync(sure.Token);
                }
                catch (OperationCanceledException) when (!iptal.IsCancellationRequested)
                {
                    break;   // bekleme suresi doldu
                }
                catch (SocketException hata)
                {
                    // Windows'ta ulasilamayan hedefe giden istek, sonraki okumada hata olarak doner; devam.
                    _gunlukcu.Uyari($"Kesif cevabi okunamadi: {hata.SocketErrorCode}");
                    continue;
                }

                if (KesifProtokolu.CevaptanPort(cevap.Buffer) is int port)
                {
                    IPAddress ip = cevap.RemoteEndPoint.Address.IsIPv4MappedToIPv6
                        ? cevap.RemoteEndPoint.Address.MapToIPv4()
                        : cevap.RemoteEndPoint.Address;
                    var adres = new SunucuAdresi(ip.ToString(), port);
                    if (!bulunanlar.Contains(adres))
                    {
                        bulunanlar.Add(adres);
                    }
                }
            }
        }
        catch (SocketException hata)
        {
            _gunlukcu.Hata(hata, "Otomatik bulma yapilamadi (UDP acilamadi).");
        }

        _gunlukcu.Bilgi($"Otomatik bulma: {bulunanlar.Count} sunucu bulundu.");
        return Sirala(bulunanlar);
    }

    // Ayni bilgisayardaki sunucu (127.0.0.1) en sona: baska bilgisayardaki gercek sunucu tercih edilsin.
    private static List<SunucuAdresi> Sirala(List<SunucuAdresi> bulunanlar) =>
        [.. bulunanlar.OrderBy(a => a.Sunucu == IPAddress.Loopback.ToString())];

    private static IEnumerable<IPAddress> Hedefler()
    {
        var hedefler = new HashSet<IPAddress> { IPAddress.Broadcast, IPAddress.Loopback };
        foreach (NetworkInterface kart in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (kart.OperationalStatus != OperationalStatus.Up || kart.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }
            foreach (UnicastIPAddressInformation bilgi in kart.GetIPProperties().UnicastAddresses)
            {
                if (bilgi.Address.AddressFamily == AddressFamily.InterNetwork && bilgi.IPv4Mask is { } maske)
                {
                    hedefler.Add(YayinAdresi(bilgi.Address, maske));
                }
            }
        }
        return hedefler;
    }

    // 192.168.1.20 / 255.255.255.0 -> 192.168.1.255
    internal static IPAddress YayinAdresi(IPAddress adres, IPAddress maske)
    {
        byte[] a = adres.GetAddressBytes();
        byte[] m = maske.GetAddressBytes();
        byte[] y = new byte[4];
        for (int i = 0; i < 4; i++)
        {
            y[i] = (byte)(a[i] | ~m[i]);
        }
        return new IPAddress(y);
    }
}
