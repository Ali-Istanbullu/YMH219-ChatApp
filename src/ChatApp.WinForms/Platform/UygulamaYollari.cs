namespace ChatApp.WinForms.Platform;

// ChatApp'in dosyalari: %LocalAppData%\ChatApp
//   ayarlar.json      son sunucu adresi + kullanici adi (sifre YOK)
//   sunucular.json    bilinen sunucularin sertifika parmak izleri
//   logs\chatapp*.log log (1 MB x 5)
// Klasor silinirse her istekte yeniden olusturulur.
public static class UygulamaYollari
{
    // Testler exe'yi gecici bir klasorle calistirir (CHATAPP_VERI_KLASORU); boylece gercek ayarlara ve verilere dokunulmaz.
    public const string TestKlasoruDegiskeni = "CHATAPP_VERI_KLASORU";

    public static string KokKlasor()
    {
        string? test = Environment.GetEnvironmentVariable(TestKlasoruDegiskeni);
        string klasor = !string.IsNullOrWhiteSpace(test)
            ? test
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChatApp");
        Directory.CreateDirectory(klasor);
        return klasor;
    }

    public static string GunlukDosyasi()
    {
        string klasor = Path.Combine(KokKlasor(), "logs");
        Directory.CreateDirectory(klasor);
        return Path.Combine(klasor, "chatapp.log");
    }
}
