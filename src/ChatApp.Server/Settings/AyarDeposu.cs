using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ChatApp.Server.Abstractions;

namespace ChatApp.Server.Settings;

// Ayarlari ayarlar.json dosyasina yazar/okur. Veritabani sifresi dosyaya ACIK yazilmaz:
// ISirKoruyucu (Windows'ta DPAPI) ile sifrelenir. Dosya baska bir Windows kullanicisinda acilamaz.
public sealed class AyarDeposu
{
    private static readonly JsonSerializerOptions JsonAyari = new() { WriteIndented = true };

    private readonly string _dosyaYolu;
    private readonly ISirKoruyucu _sirKoruyucu;
    private readonly IGunlukcu _gunlukcu;

    public AyarDeposu(string dosyaYolu, ISirKoruyucu sirKoruyucu, IGunlukcu gunlukcu)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dosyaYolu);
        ArgumentNullException.ThrowIfNull(sirKoruyucu);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        _dosyaYolu = dosyaYolu;
        _sirKoruyucu = sirKoruyucu;
        _gunlukcu = gunlukcu;
    }

    // Ayar yoksa ya da okunamiyorsa null: pencere ayarlari kullaniciya yeniden sorar.
    public SunucuAyarlari? Yukle()
    {
        if (!File.Exists(_dosyaYolu))
        {
            return null;
        }

        try
        {
            AyarDosyasi dosya = JsonSerializer.Deserialize<AyarDosyasi>(File.ReadAllText(_dosyaYolu), JsonAyari)
                ?? throw new JsonException("Ayar dosyasi bos.");
            string sifre = Encoding.UTF8.GetString(_sirKoruyucu.Coz(Convert.FromBase64String(dosya.KorunmusSifre)));
            return new SunucuAyarlari(dosya.VeritabaniSunucusu, dosya.VeritabaniPortu, dosya.VeritabaniAdi,
                dosya.VeritabaniKullanicisi, sifre, dosya.DinlemePortu);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException or JsonException
                                         or FormatException or CryptographicException)
        {
            // Dosya bozuk, okunamiyor ya da baska Windows kullanicisinda sifrelenmis.
            _gunlukcu.Hata(hata, "Ayar dosyasi okunamadi; ayarlar yeniden sorulacak.");
            return null;
        }
    }

    // Yazilamazsa IOException / UnauthorizedAccessException (pencere kullaniciya gosterir).
    public void Kaydet(SunucuAyarlari ayarlar)
    {
        ArgumentNullException.ThrowIfNull(ayarlar);
        byte[] korunmus = _sirKoruyucu.Koru(Encoding.UTF8.GetBytes(ayarlar.VeritabaniSifresi));
        var dosya = new AyarDosyasi(ayarlar.VeritabaniSunucusu, ayarlar.VeritabaniPortu, ayarlar.VeritabaniAdi,
            ayarlar.VeritabaniKullanicisi, Convert.ToBase64String(korunmus), ayarlar.DinlemePortu);

        // Once gecici dosyaya yaz, sonra yerine koy: yazarken elektrik kesilirse eski dosya bozulmaz.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_dosyaYolu))!);
        string gecici = _dosyaYolu + ".tmp";
        File.WriteAllText(gecici, JsonSerializer.Serialize(dosya, JsonAyari));
        File.Move(gecici, _dosyaYolu, overwrite: true);
    }

    // Dosya calisirken silindiyse bellekteki ayarlardan yeniden yazar (bir dahaki acilista tekrar sorulmasin).
    // Yazilamazsa sadece loga yazilir: sunucu calismaya devam eder.
    public void SilindiyseYenidenOlustur(SunucuAyarlari ayarlar)
    {
        if (File.Exists(_dosyaYolu))
        {
            return;
        }
        try
        {
            Kaydet(ayarlar);
            _gunlukcu.Uyari("Ayar dosyasi silinmisti, yeniden olusturuldu.");
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            _gunlukcu.Hata(hata, "Silinen ayar dosyasi yeniden olusturulamadi.");
        }
    }

    // Diskteki bicim (sifre DPAPI ile sifrelenmis, base64).
    private sealed record AyarDosyasi(
        string VeritabaniSunucusu,
        int VeritabaniPortu,
        string VeritabaniAdi,
        string VeritabaniKullanicisi,
        string KorunmusSifre,
        int DinlemePortu);
}
