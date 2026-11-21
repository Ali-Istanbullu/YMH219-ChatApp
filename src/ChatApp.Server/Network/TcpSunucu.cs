using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using ChatApp.Protocol.Iletisim;
using ChatApp.Protocol.Kurallar;
using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;
using ChatApp.Server.Abstractions;
using ChatApp.Server.Sessions;

namespace ChatApp.Server.Network;

// TcpListener ile portu dinler; her istemci icin TLS kurar ve paketleri okuyup PaketIsleyici'ye verir.
//
// Hata kurali: bir istemcinin baglantisinda ne olursa olsun SADECE o baglanti kapanir,
// sunucu ve diger kullanicilar etkilenmez.
//   - ag hatalari (koptu, TLS kurulamadi, zaman asimi, bozuk paket): beklenen, Bilgi/Uyari olarak loglanir
//   - bizim koddaki hata (bug): Hata olarak loglanir; debugger bagliysa orada durur
public sealed class TcpSunucu : IAsyncDisposable
{
    public const int EnFazlaBaglanti = 500;
    private static readonly TimeSpan TlsZamanAsimi = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan GirisZamanAsimi = TimeSpan.FromSeconds(60);

    private readonly OturumYoneticisi _oturumlar;
    private readonly PaketIsleyici _isleyici;
    private readonly ISaat _saat;
    private readonly IGunlukcu _gunlukcu;
    private readonly ConcurrentDictionary<Guid, Task> _istemciGorevleri = new();

    private TcpListener? _dinleyici;
    private X509Certificate2? _sertifika;
    private CancellationTokenSource? _durdurma;
    private Task? _kabulGorevi;

    public TcpSunucu(OturumYoneticisi oturumlar, PaketIsleyici isleyici, ISaat saat, IGunlukcu gunlukcu)
    {
        ArgumentNullException.ThrowIfNull(oturumlar);
        ArgumentNullException.ThrowIfNull(isleyici);
        ArgumentNullException.ThrowIfNull(saat);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        _oturumlar = oturumlar;
        _isleyici = isleyici;
        _saat = saat;
        _gunlukcu = gunlukcu;
    }

    public bool CalisiyorMu => _dinleyici is not null;

    // Gercekte dinlenen port (0 verilirse isletim sistemi bos bir port secer; testlerde kullanilir).
    public int Port => (_dinleyici?.LocalEndpoint as IPEndPoint)?.Port ?? 0;

    // Port doluysa SocketException firlatir (beklenen: pencere kullaniciya gosterir).
    public void Baslat(int port, X509Certificate2 sertifika)
    {
        ArgumentNullException.ThrowIfNull(sertifika);
        if (CalisiyorMu)
        {
            throw new InvalidOperationException("Sunucu zaten calisiyor.");
        }

        var dinleyici = new TcpListener(IPAddress.Any, port);
        dinleyici.Start();   // SocketException: port kullanimda

        _dinleyici = dinleyici;
        _sertifika = sertifika;
        _durdurma = new CancellationTokenSource();
        _kabulGorevi = KabulDongusuAsync(dinleyici, _durdurma.Token);
        _gunlukcu.Bilgi($"Sunucu basladi, port {Port}.");
    }

    public async Task DurdurAsync()
    {
        if (_dinleyici is null)
        {
            return;
        }

        // Sira onemli: once yeni baglanti alma, sonra herkese "sunucu kapandi" de, en son bekleyen islemleri iptal et.
        _dinleyici.Stop();
        _dinleyici = null;

        var kapanis = new OturumSonlandi(OturumSonuSebebi.SunucuKapandi, null);
        await Task.WhenAll(_oturumlar.TumBaglantilar().Select(o => o.KapatAsync(kapanis)));

        _durdurma!.Cancel();
        await _kabulGorevi!;
        await Task.WhenAll(_istemciGorevleri.Values);

        _durdurma.Dispose();
        _durdurma = null;
        _gunlukcu.Bilgi("Sunucu durduruldu.");
    }

    public async ValueTask DisposeAsync() => await DurdurAsync();

    private async Task KabulDongusuAsync(TcpListener dinleyici, CancellationToken iptal)
    {
        while (!iptal.IsCancellationRequested)
        {
            TcpClient istemci;
            try
            {
                istemci = await dinleyici.AcceptTcpClientAsync(iptal);
            }
            catch (Exception hata) when (hata is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                break;   // sunucu durduruluyor
            }

            if (_istemciGorevleri.Count >= EnFazlaBaglanti)
            {
                _gunlukcu.Uyari("Baglanti siniri doldu, yeni baglanti reddedildi.");
                istemci.Dispose();
                continue;
            }

            Guid gorevId = Guid.NewGuid();
            _istemciGorevleri[gorevId] = IstemciyiIsleAsync(istemci, iptal)
                .ContinueWith(_ => _istemciGorevleri.TryRemove(gorevId, out Task? _), TaskScheduler.Default);
        }
    }

    private async Task IstemciyiIsleAsync(TcpClient tcp, CancellationToken sunucuIptal)
    {
        await Task.Yield();   // kabul dongusu beklemesin

        string ip = (tcp.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "?";
        IstemciBaglantisi? baglanti = null;
        try
        {
            tcp.NoDelay = true;
            var tls = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false);

            using (var tlsSuresi = CancellationTokenSource.CreateLinkedTokenSource(sunucuIptal))
            {
                tlsSuresi.CancelAfter(TlsZamanAsimi);
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
                {
                    ServerCertificate = _sertifika,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    ClientCertificateRequired = false
                }, tlsSuresi.Token);
            }

            baglanti = new IstemciBaglantisi(tls, ip, _saat.SimdiUtc);
            _oturumlar.BaglantiEkle(baglanti);
            await OkumaDongusuAsync(baglanti, new PaketOkuyucu(tls, ProtokolSinirlari.SunucununOkuyacagiSatir), sunucuIptal);
        }
        catch (ProtokolHatasi hata)
        {
            _gunlukcu.Uyari($"Kurallara uymayan paket, baglanti kesildi: IP {ip} ({hata.Message})");
        }
        catch (Exception hata) when (BeklenenAgHatasiMi(hata))
        {
            _gunlukcu.Bilgi($"Baglanti kapandi: IP {ip} ({hata.GetType().Name})");
        }
        catch (Exception hata)
        {
            // Bizim koddaki bir hata. Sadece bu baglanti kapanir; ayrintisi loga yazilir.
            _gunlukcu.Hata(hata, $"Beklenmeyen hata, baglanti kapatildi: IP {ip}");
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }
        finally
        {
            if (baglanti is not null)
            {
                await baglanti.KapatAsync(null);
                await _isleyici.BaglantiKapandiAsync(baglanti);
            }
            tcp.Dispose();
        }
    }

    private async Task OkumaDongusuAsync(IstemciBaglantisi baglanti, PaketOkuyucu okuyucu, CancellationToken sunucuIptal)
    {
        // Giris yapmadan bekleyen baglanti 60 saniyede kapatilir (bos baglantilarla sunucuyu doldurmasinlar).
        using var girisSuresi = CancellationTokenSource.CreateLinkedTokenSource(sunucuIptal);
        girisSuresi.CancelAfter(GirisZamanAsimi);

        while (!baglanti.KapandiMi)
        {
            CancellationToken iptal = baglanti.KullaniciId is null ? girisSuresi.Token : sunucuIptal;
            Paket? paket = await okuyucu.OkuAsync(iptal);
            if (paket is null || !await _isleyici.IsleAsync(baglanti, paket))
            {
                return;
            }
        }
    }

    private static bool BeklenenAgHatasiMi(Exception hata) =>
        hata is IOException or SocketException or AuthenticationException
            or ObjectDisposedException or OperationCanceledException;
}
