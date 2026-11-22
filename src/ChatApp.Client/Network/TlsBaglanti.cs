using System.Net.Security;
using System.Net.Sockets;
using ChatApp.Protocol.Iletisim;
using ChatApp.Protocol.Kurallar;
using ChatApp.Protocol.Paketler;

namespace ChatApp.Client.Network;

// TCP + TLS uzerinden satir satir JSON paket okur/yazar.
internal sealed class TlsBaglanti : IBaglanti
{
    private readonly TcpClient _tcp;
    private readonly SslStream _akis;
    private readonly PaketOkuyucu _okuyucu;
    private readonly SemaphoreSlim _yazmaKilidi = new(1, 1);   // ayni anda tek yazma

    public TlsBaglanti(TcpClient tcp, SslStream akis)
    {
        _tcp = tcp;
        _akis = akis;
        _okuyucu = new PaketOkuyucu(akis, ProtokolSinirlari.IstemcininOkuyacagiSatir);
    }

    public Task<Paket?> OkuAsync(CancellationToken iptal) => _okuyucu.OkuAsync(iptal);

    public async Task GonderAsync(Paket paket, CancellationToken iptal)
    {
        byte[] veri = PaketYazici.Serilestir(paket);
        await _yazmaKilidi.WaitAsync(iptal);
        try
        {
            await _akis.WriteAsync(veri, iptal);
            await _akis.FlushAsync(iptal);
        }
        finally
        {
            _yazmaKilidi.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _akis.DisposeAsync();
        _tcp.Dispose();
    }
}
