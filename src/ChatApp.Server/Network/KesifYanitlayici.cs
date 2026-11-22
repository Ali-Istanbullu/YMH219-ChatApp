using System.Net;
using System.Net.Sockets;
using ChatApp.Protocol.Kesif;
using ChatApp.Server.Abstractions;

namespace ChatApp.Server.Network;

// ChatApp'lerin "ChatServer var mi?" sorusuna cevap verir (UDP 5051).
// Sunucu calisirken acik; durdurulunca kapanir.
public sealed class KesifYanitlayici : IAsyncDisposable
{
    private readonly IGunlukcu _gunlukcu;
    private readonly int _udpPortu;

    private UdpClient? _udp;
    private CancellationTokenSource? _durdurma;
    private Task? _dongu;

    public KesifYanitlayici(IGunlukcu gunlukcu) : this(gunlukcu, KesifProtokolu.VarsayilanPort)
    {
    }

    // Testler baska port kullanabilsin diye.
    internal KesifYanitlayici(IGunlukcu gunlukcu, int udpPortu)
    {
        ArgumentNullException.ThrowIfNull(gunlukcu);
        _gunlukcu = gunlukcu;
        _udpPortu = udpPortu;
    }

    public bool CalisiyorMu => _udp is not null;

    // tcpPortu: istemcilere bildirilecek sohbet portu. UDP portu doluysa SocketException (cagiran karar verir).
    public void Baslat(int tcpPortu)
    {
        if (CalisiyorMu)
        {
            throw new InvalidOperationException("Kesif zaten calisiyor.");
        }
        var udp = new UdpClient(new IPEndPoint(IPAddress.Any, _udpPortu));
        _udp = udp;
        _durdurma = new CancellationTokenSource();
        _dongu = DinleAsync(udp, KesifProtokolu.CevapOlustur(tcpPortu), _durdurma.Token);
        _gunlukcu.Bilgi($"Otomatik bulma acik (UDP {_udpPortu}).");
    }

    public async Task DurdurAsync()
    {
        if (_udp is null)
        {
            return;
        }
        _durdurma!.Cancel();
        _udp.Dispose();
        _udp = null;
        await _dongu!;
        _durdurma.Dispose();
        _durdurma = null;
    }

    public async ValueTask DisposeAsync() => await DurdurAsync();

    private async Task DinleAsync(UdpClient udp, byte[] cevap, CancellationToken iptal)
    {
        while (!iptal.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult gelen = await udp.ReceiveAsync(iptal);
                if (KesifProtokolu.IstekMi(gelen.Buffer))
                {
                    await udp.SendAsync(cevap, gelen.RemoteEndPoint, iptal);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;   // durduruldu
            }
            catch (SocketException hata)
            {
                // Tek bir pakette ag hatasi (ornegin karsi taraf kapanmis): sonraki istekleri dinlemeye devam.
                _gunlukcu.Uyari($"Otomatik bulma: {hata.SocketErrorCode}");
            }
        }
    }
}
