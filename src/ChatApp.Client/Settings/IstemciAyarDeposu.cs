using System.Text.Json;
using ChatApp.Client.Abstractions;

namespace ChatApp.Client.Settings;

// Giris ekraninin hatirladiklari. Sifre HATIRLANMAZ (Ali karari).
// ElleAdres: "Gelismis" altinda elle yazilan sunucu adresi; bossa sunucu agda otomatik bulunur.
public sealed record IstemciAyarlari(string ElleAdres, string SonKullaniciAdi)
{
    public static IstemciAyarlari Varsayilan { get; } = new(string.Empty, string.Empty);
}

// ayarlar.json'u okur/yazar. Dosya yoksa ya da bozuksa varsayilanlar kullanilir; hata ekrana cikmaz.
public sealed class IstemciAyarDeposu
{
    private readonly string _dosyaYolu;
    private readonly IGunlukcu _gunlukcu;

    public IstemciAyarDeposu(string dosyaYolu, IGunlukcu gunlukcu)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dosyaYolu);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        _dosyaYolu = dosyaYolu;
        _gunlukcu = gunlukcu;
    }

    public IstemciAyarlari Yukle()
    {
        if (!File.Exists(_dosyaYolu))
        {
            return IstemciAyarlari.Varsayilan;
        }
        try
        {
            IstemciAyarlari? ayarlar = JsonSerializer.Deserialize<IstemciAyarlari>(File.ReadAllText(_dosyaYolu));
            // Eski surumun dosyasinda olmayan alanlar null gelir; bos kabul edilir.
            return ayarlar is null
                ? IstemciAyarlari.Varsayilan
                : new IstemciAyarlari(ayarlar.ElleAdres ?? string.Empty, ayarlar.SonKullaniciAdi ?? string.Empty);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException or JsonException)
        {
            _gunlukcu.Hata(hata, "Istemci ayarlari okunamadi; varsayilanlar kullanildi.");
            return IstemciAyarlari.Varsayilan;
        }
    }

    public void Kaydet(IstemciAyarlari ayarlar)
    {
        ArgumentNullException.ThrowIfNull(ayarlar);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_dosyaYolu))!);
            File.WriteAllText(_dosyaYolu, JsonSerializer.Serialize(ayarlar));
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            // Sadece bir dahaki acilista hatirlanmaz; uygulama devam eder.
            _gunlukcu.Hata(hata, "Istemci ayarlari yazilamadi.");
        }
    }
}
