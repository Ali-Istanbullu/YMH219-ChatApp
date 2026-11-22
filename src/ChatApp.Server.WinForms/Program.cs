using System.Diagnostics;
using ChatApp.Server.Abstractions;
using ChatApp.Server.Data;
using ChatApp.Server.Network;
using ChatApp.Server.Platform;
using ChatApp.Server.Postgres;
using ChatApp.Server.Security;
using ChatApp.Server.Services;
using ChatApp.Server.Sessions;
using ChatApp.Server.Settings;
using ChatApp.Server.WinForms.Forms;
using ChatApp.Server.WinForms.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace ChatApp.Server.WinForms;

// ================================================================================
//  CHATSERVER BASLANGIC NOKTASI ve "KOMPOZISYON KOKU"
//
//   1) Genel hata yakalayici ve log
//   2) Veritabani ayarlari: yoksa (ilk acilis) pencereden sorulur, test edilip kaydedilir
//   3) Siniflar birbirine baglanir: DI container SADECE burada kurulur
//   4) Pencere acilir; kapaninca servisler duzgun kapatilir
// ================================================================================
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var genelHataYakalayici = new GenelHataYakalayici();
        genelHataYakalayici.Kur();

        ApplicationConfiguration.Initialize();

        using var gunlukcu = new SerilogGunlukcu(LogDosyasi());
        genelHataYakalayici.GunlukcuyuAta(gunlukcu);
        gunlukcu.Bilgi($"ChatServer {Application.ProductVersion} açıldı.");

        bool yenidenBaslat = false;
        try
        {
            yenidenBaslat = Calistir(gunlukcu);
        }
        catch (Exception hata) when (!DerlemeModu.HataAyiklamaMi)   // Debug'da debugger dursun
        {
            gunlukcu.Hata(hata, "ChatServer baslatilamadi.");
            MessageBox.Show("ChatServer başlatılamadı. Ayrıntı log dosyasında.", GenelHataYakalayici.Baslik,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            gunlukcu.Bilgi("ChatServer kapandı.");
        }

        if (yenidenBaslat && Environment.ProcessPath is { } exe)
        {
            Process.Start(exe);
        }
    }

    // Pencere kapaninca doner. true: ayarlar degisti, uygulama yeniden baslatilsin.
    private static bool Calistir(SerilogGunlukcu gunlukcu)
    {
        var sirKoruyucu = new DpapiSirKoruyucu();
        var ayarDeposu = new AyarDeposu(UygulamaYollari.AyarDosyasi(), sirKoruyucu, gunlukcu);

        SunucuAyarlari? ayarlar = ayarDeposu.Yukle();
        if (ayarlar is null)
        {
            // Ilk acilis (ya da ayar dosyasi bozuk): veritabani bilgileri sorulur.
            using var ayarFormu = new VeritabaniAyarFormu(SunucuAyarlari.Varsayilan, ayarDeposu, gunlukcu);
            if (ayarFormu.ShowDialog() != DialogResult.OK || ayarFormu.Ayarlar is null)
            {
                return false;   // kullanici vazgecti
            }
            ayarlar = ayarFormu.Ayarlar;
        }

        ServiceProvider servisler = ServisleriKur(ayarlar, gunlukcu, sirKoruyucu, ayarDeposu, UygulamaYollari.KokKlasor());
        try
        {
            AnaForm form = servisler.GetRequiredService<AnaForm>();
            Application.Run(form);
            return form.YenidenBaslatilsin;
        }
        finally
        {
            // Sunucu, veritabani baglantilari async kapanir. Arayuz dongusu bittigi icin
            // islem ayri is parcaciginda beklenir (arayuz is parcaciginda beklemek kilitlenebilir).
            Task.Run(async () => await servisler.DisposeAsync()).GetAwaiter().GetResult();
        }
    }

    // ================= DI KURULUMU (hepsi Singleton: tek sunucu, tek pencere) =================
    // internal: smoke testleri ayni kayitlarla container'i kurabilsin.
    internal static ServiceProvider ServisleriKur(SunucuAyarlari ayarlar, SerilogGunlukcu gunlukcu,
        ISirKoruyucu sirKoruyucu, AyarDeposu ayarDeposu, string sertifikaKlasoru)
    {
        var servisler = new ServiceCollection();

        // ---- Disaridan hazir gelenler ----
        servisler.AddSingleton(ayarlar);
        servisler.AddSingleton(gunlukcu);
        servisler.AddSingleton<IGunlukcu>(gunlukcu);
        servisler.AddSingleton(sirKoruyucu);
        servisler.AddSingleton(ayarDeposu);
        servisler.AddSingleton<ISaat, SistemSaati>();

        // ---- Veritabani ----
        servisler.AddSingleton(_ => new PostgresVeritabani(PostgresVeritabani.BaglantiDizesi(
            ayarlar.VeritabaniSunucusu, ayarlar.VeritabaniPortu, ayarlar.VeritabaniAdi,
            ayarlar.VeritabaniKullanicisi, ayarlar.VeritabaniSifresi)));
        servisler.AddSingleton<IKullaniciDeposu, PostgresKullaniciDeposu>();
        servisler.AddSingleton<IMesajDeposu, PostgresMesajDeposu>();
        servisler.AddSingleton<IEngelDeposu, PostgresEngelDeposu>();
        servisler.AddSingleton<IDavetDeposu, PostgresDavetDeposu>();

        // ---- Guvenlik ----
        servisler.AddSingleton(_ => new SifreHashleyici());
        servisler.AddSingleton<GirisKilidi>();
        servisler.AddSingleton<HizSinirlayici>();
        servisler.AddSingleton(sp => new SertifikaDeposu(sertifikaKlasoru, sirKoruyucu,
            sp.GetRequiredService<ISaat>(), gunlukcu));

        // ---- Sunucu mantigi ----
        servisler.AddSingleton<OturumYoneticisi>();
        servisler.AddSingleton<HesapServisi>();
        servisler.AddSingleton<KisiServisi>();
        servisler.AddSingleton<MesajServisi>();
        servisler.AddSingleton<YonetimServisi>();
        servisler.AddSingleton<MesajTemizleyici>();
        servisler.AddSingleton<PaketIsleyici>();
        servisler.AddSingleton<TcpSunucu>();
        servisler.AddSingleton<KesifYanitlayici>();
        servisler.AddSingleton<SunucuCalistirici>();

        // ---- Pencere ----
        servisler.AddSingleton<AnaForm>();

        // ValidateOnBuild: eksik/hatali kayit varsa uygulama acilirken hemen anlasilir.
        return servisler.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    // Log klasoru olusturulamazsa (izin yok) log bos bir yola yazilmaya calisilir; Serilog sessizce vazgecer.
    private static string LogDosyasi()
    {
        try
        {
            return UygulamaYollari.GunlukDosyasi();
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return Path.Combine(Path.GetTempPath(), "ChatServer", "chatserver.log");
        }
    }
}
