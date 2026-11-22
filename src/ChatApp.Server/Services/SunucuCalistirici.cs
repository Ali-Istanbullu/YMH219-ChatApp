using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using ChatApp.Server.Abstractions;
using ChatApp.Server.Data;
using ChatApp.Server.Network;
using ChatApp.Server.Security;

namespace ChatApp.Server.Services;

// Sunucuyu baslatip durdurur: sertifikayi yukler, TCP'yi acar, mesaj temizligini zamanlar.
// Sunucu penceresi sadece bu sinifla konusur; ag ve sertifika ayrintilarini bilmez.
public sealed class SunucuCalistirici : IAsyncDisposable
{
    private static readonly TimeSpan TemizlikAraligi = TimeSpan.FromDays(1);

    private readonly TcpSunucu _tcp;
    private readonly SertifikaDeposu _sertifikalar;
    private readonly MesajTemizleyici _temizleyici;
    private readonly IGunlukcu _gunlukcu;

    private X509Certificate2? _sertifika;
    private CancellationTokenSource? _temizlikIptali;
    private Task? _temizlikGorevi;

    public SunucuCalistirici(TcpSunucu tcp, SertifikaDeposu sertifikalar, MesajTemizleyici temizleyici, IGunlukcu gunlukcu)
    {
        ArgumentNullException.ThrowIfNull(tcp);
        ArgumentNullException.ThrowIfNull(sertifikalar);
        ArgumentNullException.ThrowIfNull(temizleyici);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        _tcp = tcp;
        _sertifikalar = sertifikalar;
        _temizleyici = temizleyici;
        _gunlukcu = gunlukcu;
    }

    public bool CalisiyorMu => _tcp.CalisiyorMu;

    // Istemcilerin ilk baglantida gordugu parmak izi (sunucu baslayinca dolar).
    public string? ParmakIzi => _sertifika is null ? null : SertifikaDeposu.ParmakIzi(_sertifika);

    // Beklenen hatalar yukari cikar (pencere gosterir):
    //   SocketException: port kullanimda; IOException / UnauthorizedAccessException: sertifika dosyasi.
    public void Baslat(int port)
    {
        _sertifika ??= _sertifikalar.YukleVeyaOlustur();
        _tcp.Baslat(port, _sertifika);

        _temizlikIptali = new CancellationTokenSource();
        _temizlikGorevi = TemizlikDongusuAsync(_temizlikIptali.Token);
    }

    public async Task DurdurAsync()
    {
        if (_temizlikIptali is not null)
        {
            _temizlikIptali.Cancel();
            await _temizlikGorevi!;
            _temizlikIptali.Dispose();
            _temizlikIptali = null;
        }
        await _tcp.DurdurAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await DurdurAsync();
        _sertifika?.Dispose();
    }

    // Acilista bir kez, sonra gunde bir kez.
    private async Task TemizlikDongusuAsync(CancellationToken iptal)
    {
        using var zamanlayici = new PeriodicTimer(TemizlikAraligi);
        do
        {
            try
            {
                await _temizleyici.GerekirseTemizleAsync();
            }
            catch (DepoHatasi hata)
            {
                _gunlukcu.Hata(hata, "Mesaj temizligi yapilamadi (veritabani hatasi); yarin tekrar denenecek.");
            }
            catch (Exception hata) when (hata is not OperationCanceledException)
            {
                // Arka plan gorevi: buradaki bug'i kimse beklemiyor, en azindan loga yazilsin.
                _gunlukcu.Hata(hata, "Mesaj temizliginde beklenmeyen hata.");
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
        }
        while (await BekleAsync(zamanlayici, iptal));
    }

    private static async Task<bool> BekleAsync(PeriodicTimer zamanlayici, CancellationToken iptal)
    {
        try
        {
            return await zamanlayici.WaitForNextTickAsync(iptal);
        }
        catch (OperationCanceledException)
        {
            return false;   // sunucu durduruldu
        }
    }
}
