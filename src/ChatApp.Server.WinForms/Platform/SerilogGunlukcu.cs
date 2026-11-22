using System.Collections.Concurrent;
using ChatApp.Server.Abstractions;
using Serilog;
using Serilog.Core;

namespace ChatApp.Server.WinForms.Platform;

// IGunlukcu'nun Serilog uygulamasi (AliBird / KEL ile ayni ayarlar) + pencerenin "Olay günlüğü" sekmesi.
// 1 MB'a ulasan dosya kapanir, yenisi acilir; en fazla 5 dosya, en eskisi otomatik silinir.
// Mesaj icerikleri ve sifreler loga yazilmaz (bu kural cagiranlarda).
// Bu sinif ASLA exception firlatmaz: log yazilamazsa sunucu yine calisir.
public sealed class SerilogGunlukcu : IGunlukcu, IDisposable
{
    private const long DosyaBoyutuSiniri = 1024 * 1024;
    private const int TutulacakDosyaSayisi = 5;
    private const int PenceredeTutulacakSatir = 1000;

    private const string SatirBicimi =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    private readonly Logger _logger;
    private readonly ConcurrentQueue<string> _sonSatirlar = new();

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
            // Log dosyasi acilamadi (izin yok, disk dolu ...): hicbir yere yazmayan logger.
            _logger = new LoggerConfiguration().CreateLogger();
        }
    }

    // Yeni satir yazildi (pencere gosterir). Her is parcacigindan gelebilir.
    public event Action<string>? SatirYazildi;

    // Pencere acilmadan once yazilanlar da gorulsun diye son satirlar bellekte tutulur.
    public IReadOnlyList<string> SonSatirlar() => _sonSatirlar.ToList();

    public void Bilgi(string mesaj) => Yaz("BLG", mesaj, null);

    public void Uyari(string mesaj) => Yaz("UYR", mesaj, null);

    public void Hata(Exception hata, string mesaj) => Yaz("HATA", mesaj, hata);

    private void Yaz(string seviye, string mesaj, Exception? hata)
    {
        try
        {
            // "{Mesaj:l}": mesajdaki { } sablon sanilmasin.
            switch (seviye)
            {
                case "BLG": _logger.Information("{Mesaj:l}", mesaj); break;
                case "UYR": _logger.Warning("{Mesaj:l}", mesaj); break;
                default: _logger.Error(hata, "{Mesaj:l}", mesaj); break;
            }

            string satir = $"{DateTime.Now:HH:mm:ss} [{seviye}] {mesaj}" + (hata is null ? "" : $" ({hata.GetType().Name}: {hata.Message})");
            _sonSatirlar.Enqueue(satir);
            while (_sonSatirlar.Count > PenceredeTutulacakSatir)
            {
                _sonSatirlar.TryDequeue(out _);
            }
            SatirYazildi?.Invoke(satir);
        }
        catch
        {
            // log hatasi sunucuyu asla durdurmaz.
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
            // kapanista bile exception disari cikmaz.
        }
    }
}
