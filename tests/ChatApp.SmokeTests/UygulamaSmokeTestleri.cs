using System.Diagnostics;
using ChatApp.Server.Settings;
using ChatApp.Server.WinForms.Platform;
using Microsoft.Extensions.DependencyInjection;
using IstemciForm = ChatApp.WinForms.Forms.AnaForm;
using IstemciGunlukcu = ChatApp.WinForms.Platform.SerilogGunlukcu;
using IstemciProgram = ChatApp.WinForms.Program;
using SunucuForm = ChatApp.Server.WinForms.Forms.AnaForm;
using SunucuGunlukcu = ChatApp.Server.WinForms.Platform.SerilogGunlukcu;
using SunucuProgram = ChatApp.Server.WinForms.Program;

namespace ChatApp.SmokeTests;

// Iki exe gercekten ayaga kalkiyor mu? Hepsi GECICI bir klasorle calisir: gercek ayarlara/verilere dokunulmaz.
[TestClass]
[TestCategory("Smoke")]
public sealed class UygulamaSmokeTestleri
{
    private string _klasor = null!;

    [TestInitialize]
    public void Hazirla()
    {
        _klasor = Path.Combine(Path.GetTempPath(), "chatapp-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_klasor);
    }

    [TestCleanup]
    public void Temizle()
    {
        try { Directory.Delete(_klasor, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ChatApp.exe'deki GERCEK DI kayitlari eksiksiz mi (ValidateOnBuild), pencere olusuyor mu?
    [TestMethod]
    public async Task Istemci_DI_GercekKayitlarlaKurulur()
    {
        using var gunlukcu = new IstemciGunlukcu(Path.Combine(_klasor, "chatapp.log"));
        await using ServiceProvider servisler = IstemciProgram.ServisleriKur(gunlukcu, _klasor);

        using IstemciForm form = servisler.GetRequiredService<IstemciForm>();
        Assert.IsNotNull(form);
    }

    // ChatServer.exe'deki GERCEK DI kayitlari eksiksiz mi? Veritabanina baglanilmaz (pencere gosterilmiyor).
    [TestMethod]
    public async Task Sunucu_DI_GercekKayitlarlaKurulur()
    {
        using var gunlukcu = new SunucuGunlukcu(Path.Combine(_klasor, "chatserver.log"));
        var sirKoruyucu = new DpapiSirKoruyucu();
        var ayarDeposu = new AyarDeposu(Path.Combine(_klasor, "ayarlar.json"), sirKoruyucu, gunlukcu);
        var ayarlar = new SunucuAyarlari("localhost", 5432, "chatapp", "chatapp", "test-sifresi", 5050);

        await using ServiceProvider servisler = SunucuProgram.ServisleriKur(ayarlar, gunlukcu, sirKoruyucu, ayarDeposu, _klasor);

        using SunucuForm form = servisler.GetRequiredService<SunucuForm>();
        Assert.IsNotNull(form);
    }

    // ChatApp.exe acilir (giris ekrani), kapat denince duzgun kapanir.
    [TestMethod]
    public void IstemciExe_AcilirVeDuzgunKapanir() =>
        ExeAcilipKapanir("ChatApp.exe", ChatApp.WinForms.Platform.UygulamaYollari.TestKlasoruDegiskeni);

    // ChatServer.exe bos klasorle acilir (ilk kurulum penceresi), kapat denince hicbir sey kurmadan kapanir.
    [TestMethod]
    public void SunucuExe_IlkKurulumPenceresiAcilirVeDuzgunKapanir()
    {
        ExeAcilipKapanir("ChatServer.exe", UygulamaYollari.TestKlasoruDegiskeni);
        Assert.IsFalse(Directory.Exists(Path.Combine(_klasor, "veritabani")), "vazgecilince veritabani kurulmamali");
    }

    private void ExeAcilipKapanir(string exeAdi, string klasorDegiskeni)
    {
        string exe = Path.Combine(AppContext.BaseDirectory, exeAdi);
        if (!File.Exists(exe))
        {
            Assert.Inconclusive($"Exe bulunamadi: {exe}");
        }

        var bilgi = new ProcessStartInfo(exe) { UseShellExecute = false };
        bilgi.Environment[klasorDegiskeni] = _klasor;
        using Process surec = Process.Start(bilgi)!;
        try
        {
            // WaitForInputIdle pencere gorunmeden donebilir: ana pencere tutamaci (handle) olusana kadar bekle.
            var sayac = Stopwatch.StartNew();
            while (surec.MainWindowHandle == IntPtr.Zero && !surec.HasExited && sayac.ElapsedMilliseconds < 15000)
            {
                Thread.Sleep(100);
                surec.Refresh();
            }
            Assert.IsFalse(surec.HasExited, $"{exeAdi} acilir acilmaz kapandi. Log: {_klasor}\\logs");
            Assert.AreNotEqual(IntPtr.Zero, surec.MainWindowHandle, $"{exeAdi}: ana pencere 15 sn icinde acilmadi.");
            Thread.Sleep(1000);   // pencere ilk cizimini ve acilis islerini bitirsin

            Assert.IsTrue(surec.CloseMainWindow(), $"Kapat mesaji gonderilemedi. Pencere: '{surec.MainWindowTitle}'");
            Assert.IsTrue(surec.WaitForExit(15000), $"{exeAdi} 15 sn icinde kapanmadi. Pencere: '{surec.MainWindowTitle}'");
            Assert.AreEqual(0, surec.ExitCode);
            Assert.IsTrue(File.Exists(Path.Combine(_klasor, "logs", exeAdi == "ChatApp.exe" ? "chatapp.log" : "chatserver.log")),
                "log gecici klasore yazilmali (gercek AppData'ya degil)");
        }
        finally
        {
            if (!surec.HasExited)
            {
                surec.Kill();
            }
        }
    }
}
