namespace ChatApp.Server.WinForms.Platform;

// ChatServer'in dosyalari: %LocalAppData%\ChatServer
//   ayarlar.json              veritabani ayarlari (sifre DPAPI ile sifreli)
//   sunucu-sertifikasi.dat    TLS sertifikasi (DPAPI ile sifreli)
//   logs\chatserver*.log      log (1 MB x 5)
// Program klasorune yazilmaz. Klasor silinirse her istekte yeniden olusturulur.
public static class UygulamaYollari
{
    public static string KokKlasor()
    {
        string klasor = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChatServer");
        Directory.CreateDirectory(klasor);   // olusturulamazsa IOException; cagiran yakalar
        return klasor;
    }

    public static string AyarDosyasi() => Path.Combine(KokKlasor(), "ayarlar.json");

    public static string GunlukDosyasi()
    {
        string klasor = Path.Combine(KokKlasor(), "logs");
        Directory.CreateDirectory(klasor);
        return Path.Combine(klasor, "chatserver.log");
    }
}
