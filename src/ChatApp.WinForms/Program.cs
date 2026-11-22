using ChatApp.Client.Abstractions;
using ChatApp.Client.Network;
using ChatApp.Client.Presentation;
using ChatApp.Client.Session;
using ChatApp.Client.Settings;
using ChatApp.WinForms.Forms;
using ChatApp.WinForms.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace ChatApp.WinForms;

// ================================================================================
//  CHATAPP BASLANGIC NOKTASI ve "KOMPOZISYON KOKU"
//
//   1) Genel hata yakalayici ve log
//   2) Siniflar birbirine baglanir: DI container SADECE burada kurulur
//   3) Pencere acilir; kapaninca servisler duzgun kapatilir
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
        gunlukcu.Bilgi($"ChatApp {Application.ProductVersion} açıldı.");

        try
        {
            ServiceProvider servisler = ServisleriKur(gunlukcu, UygulamaYollari.KokKlasor());
            try
            {
                Application.Run(servisler.GetRequiredService<AnaForm>());
            }
            finally
            {
                // Baglanti async kapanir; arayuz dongusu bittigi icin ayri is parcaciginda beklenir.
                Task.Run(async () => await servisler.DisposeAsync()).GetAwaiter().GetResult();
            }
        }
        catch (Exception hata) when (!DerlemeModu.HataAyiklamaMi)   // Debug'da debugger dursun
        {
            gunlukcu.Hata(hata, "ChatApp baslatilamadi.");
            MessageBox.Show("ChatApp başlatılamadı. Ayrıntı log dosyasında.", GenelHataYakalayici.Baslik,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            gunlukcu.Bilgi("ChatApp kapandı.");
        }
    }

    // ================= DI KURULUMU (hepsi Singleton: tek pencere, tek oturum) =================
    // internal: smoke testleri ayni kayitlarla container'i kurabilsin.
    internal static ServiceProvider ServisleriKur(SerilogGunlukcu gunlukcu, string kokKlasor)
    {
        var servisler = new ServiceCollection();

        // ---- Disaridan hazir gelenler ----
        servisler.AddSingleton<IGunlukcu>(gunlukcu);
        servisler.AddSingleton(TimeProvider.System);

        // ---- Istemci kutuphanesi ----
        servisler.AddSingleton(_ => new ParmakIziDeposu(Path.Combine(kokKlasor, "sunucular.json"), gunlukcu));
        servisler.AddSingleton(_ => new IstemciAyarDeposu(Path.Combine(kokKlasor, "ayarlar.json"), gunlukcu));
        servisler.AddSingleton<IBaglantiKurucu, TlsBaglantiKurucu>();
        servisler.AddSingleton<SunucuBulucu>();
        servisler.AddSingleton<SohbetIstemcisi>();
        servisler.AddSingleton<SohbetYoneticisi>();

        // ---- Pencere ----
        servisler.AddSingleton<PencereBildirici>();
        servisler.AddSingleton<GirisPaneli>();
        servisler.AddSingleton<SohbetPaneli>();
        servisler.AddSingleton<AnaForm>();

        return servisler.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static string LogDosyasi()
    {
        try
        {
            return UygulamaYollari.GunlukDosyasi();
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return Path.Combine(Path.GetTempPath(), "ChatApp", "chatapp.log");
        }
    }
}
