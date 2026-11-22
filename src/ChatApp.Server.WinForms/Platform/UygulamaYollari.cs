namespace ChatApp.Server.WinForms.Platform;

// ChatServer'in dosyalari: %LocalAppData%\ChatServer
//   ayarlar.json              veritabani ayarlari (sifre DPAPI ile sifreli)
//   veritabani\               hazir veritabani (ChatServer'in kendi PostgreSQL'i); kaldirinca KALIR
//   sunucu-sertifikasi.dat    TLS sertifikasi (DPAPI ile sifreli)
//   logs\chatserver*.log      log (1 MB x 5)
// Program klasorune yazilmaz. Klasor silinirse her istekte yeniden olusturulur.
public static class UygulamaYollari
{
    // Testler exe'yi gecici bir klasorle calistirir (CHATSERVER_VERI_KLASORU); boylece gercek ayarlara ve verilere dokunulmaz.
    public const string TestKlasoruDegiskeni = "CHATSERVER_VERI_KLASORU";

    public static string KokKlasor()
    {
        string? test = Environment.GetEnvironmentVariable(TestKlasoruDegiskeni);
        string klasor = !string.IsNullOrWhiteSpace(test)
            ? test
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChatServer");
        Directory.CreateDirectory(klasor);   // olusturulamazsa IOException; cagiran yakalar
        return klasor;
    }

    public static string AyarDosyasi() => Path.Combine(KokKlasor(), "ayarlar.json");

    // initdb klasoru kendisi olusturur; burada olusturulmaz.
    public static string VeritabaniKlasoru() => Path.Combine(KokKlasor(), "veritabani");

    public static string PostgresLogDosyasi() => Path.Combine(KokKlasor(), "logs", "postgres.log");

    public static string GunlukDosyasi()
    {
        string klasor = Path.Combine(KokKlasor(), "logs");
        Directory.CreateDirectory(klasor);
        return Path.Combine(klasor, "chatserver.log");
    }
}
