using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using ChatApp.Client.Settings;

namespace ChatApp.Client.Network;

// Sunucuya TCP ile baglanir, TLS kurar ve sertifikayi kontrol eder.
//
// Sertifika kontrolu ("ilk kullanimda guven", TOFU):
//   Sunucunun sertifikasi kendi urettigi (self-signed) oldugu icin Windows ona guvenmez.
//   Bunun yerine sertifikanin parmak izi bu bilgisayarda hatirlanir:
//   - Bu sunucuya ilk kez baglaniliyorsa parmak izi kaydedilir.
//   - Sonraki baglantilarda ayni olmali; farkliysa SertifikaDegistiHatasi (kullanici karar verir).
public sealed class TlsBaglantiKurucu : IBaglantiKurucu
{
    private static readonly TimeSpan ZamanAsimi = TimeSpan.FromSeconds(10);

    private readonly ParmakIziDeposu _parmakIzleri;

    public TlsBaglantiKurucu(ParmakIziDeposu parmakIzleri)
    {
        ArgumentNullException.ThrowIfNull(parmakIzleri);
        _parmakIzleri = parmakIzleri;
    }

    public async Task<IBaglanti> BaglanAsync(SunucuAdresi adres, CancellationToken iptal)
    {
        ArgumentNullException.ThrowIfNull(adres);
        using var zamanAsimi = CancellationTokenSource.CreateLinkedTokenSource(iptal);
        zamanAsimi.CancelAfter(ZamanAsimi);

        var tcp = new TcpClient();
        SslStream? akis = null;
        string? gorulenParmakIzi = null;
        try
        {
            await tcp.ConnectAsync(adres.Sunucu, adres.Port, zamanAsimi.Token);

            akis = new SslStream(tcp.GetStream(), leaveInnerStreamOpen: false, (_, sertifika, _, _) =>
            {
                if (sertifika is null)
                {
                    return false;
                }
                gorulenParmakIzi = sertifika.GetCertHashString(HashAlgorithmName.SHA256);
                string? bilinen = _parmakIzleri.Bul(adres);
                return bilinen is null || string.Equals(bilinen, gorulenParmakIzi, StringComparison.OrdinalIgnoreCase);
            });
            await akis.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = adres.Sunucu,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }, zamanAsimi.Token);

            if (_parmakIzleri.Bul(adres) is null)
            {
                _parmakIzleri.Kaydet(adres, gorulenParmakIzi!);   // ilk baglanti: hatirla
            }
            return new TlsBaglanti(tcp, akis);
        }
        catch (Exception hata) when (hata is SocketException or IOException or AuthenticationException
                                         || (hata is OperationCanceledException && !iptal.IsCancellationRequested))
        {
            if (akis is not null)
            {
                await akis.DisposeAsync();
            }
            tcp.Dispose();

            string? bilinen = _parmakIzleri.Bul(adres);
            if (hata is AuthenticationException && gorulenParmakIzi is not null && bilinen is not null
                && !string.Equals(bilinen, gorulenParmakIzi, StringComparison.OrdinalIgnoreCase))
            {
                throw new SertifikaDegistiHatasi(adres, bilinen, gorulenParmakIzi);
            }
            throw new BaglantiHatasi(Acikla(hata, adres), hata);
        }
        catch
        {
            // Iptal ya da beklenmeyen hata: acilan baglanti birakilip hata aynen yukari cikar.
            if (akis is not null)
            {
                await akis.DisposeAsync();
            }
            tcp.Dispose();
            throw;
        }
    }

    private static string Acikla(Exception hata, SunucuAdresi adres) => hata switch
    {
        SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain } =>
            $"\"{adres.Sunucu}\" adresi bulunamadı. Adresi kontrol edin.",
        SocketException { SocketErrorCode: SocketError.ConnectionRefused } =>
            $"{adres} adresinde çalışan bir ChatServer yok. Adresi/portu ve sunucunun açık (Başlat'a basılmış) olduğunu kontrol edin.",
        SocketException { SocketErrorCode: SocketError.NetworkUnreachable or SocketError.HostUnreachable } =>
            "Sunucuya ulaşılamıyor. Sunucuyla aynı ağa (aynı Wi-Fi / modem) bağlı olduğunuzu kontrol edin.",
        OperationCanceledException =>
            $"{adres} zamanında cevap vermedi. Sunucuyla aynı ağa (aynı Wi-Fi / modem) bağlı olduğunuzu, adresin doğru olduğunu " +
            "ve sunucu bilgisayarında güvenlik duvarının ChatServer'a izin verdiğini kontrol edin.",
        AuthenticationException => "Sunucuyla güvenli (TLS) bağlantı kurulamadı.",
        _ => $"Sunucuya bağlanılamadı: {hata.Message}"
    };
}
