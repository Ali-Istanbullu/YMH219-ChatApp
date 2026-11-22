using ChatApp.Client.Abstractions;
using Serilog;
using Serilog.Core;

namespace ChatApp.WinForms.Platform;

// IGunlukcu'nun Serilog uygulamasi (diger projelerle ayni ayarlar).
// 1 MB'a ulasan dosya kapanir, yenisi acilir; en fazla 5 dosya, en eskisi otomatik silinir.
// Mesaj icerikleri ve sifreler loga yazilmaz. Bu sinif ASLA exception firlatmaz.
public sealed class SerilogGunlukcu : IGunlukcu, IDisposable
{
    private const long DosyaBoyutuSiniri = 1024 * 1024;
    private const int TutulacakDosyaSayisi = 5;
    private const string SatirBicimi =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    private readonly Logger _logger;

    public SerilogGunlukcu(string dosyaYolu)
    {
        try
        {
            _logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    path: dosyaYolu,
                    outputTemplate: SatirBicimi,
                    fileSizeLimitBytes: DosyaBoyutuSiniri,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: TutulacakDosyaSayisi)
                .CreateLogger();
        }
        catch
        {
            // Log dosyasi acilamadi: hicbir yere yazmayan logger, uygulama calismaya devam eder.
            _logger = new LoggerConfiguration().CreateLogger();
        }
    }

    public void Bilgi(string mesaj)
    {
        try
        {
            _logger.Information("{Mesaj:l}", mesaj);
        }
        catch
        {
            // log hatasi uygulamayi durdurmaz
        }
    }

    public void Uyari(string mesaj)
    {
        try
        {
            _logger.Warning("{Mesaj:l}", mesaj);
        }
        catch
        {
            // log hatasi uygulamayi durdurmaz
        }
    }

    public void Hata(Exception hata, string mesaj)
    {
        try
        {
            _logger.Error(hata, "{Mesaj:l}", mesaj);
        }
        catch
        {
            // log hatasi uygulamayi durdurmaz
        }
    }

    public void Dispose()
    {
        try
        {
            _logger.Dispose();
        }
        catch
        {
            // kapanista bile exception disari cikmaz
        }
    }
}
