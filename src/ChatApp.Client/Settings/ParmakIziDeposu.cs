using System.Text.Json;
using ChatApp.Client.Abstractions;
using ChatApp.Client.Network;

namespace ChatApp.Client.Settings;

// Baglanilan sunucularin sertifika parmak izlerini hatirlar (sunucular.json).
// Dosya silinir ya da bozulursa bos kabul edilir: bir sonraki baglantida parmak izi yeniden ogrenilir.
public sealed class ParmakIziDeposu
{
    private readonly string _dosyaYolu;
    private readonly IGunlukcu _gunlukcu;
    private readonly Lock _kilit = new();

    public ParmakIziDeposu(string dosyaYolu, IGunlukcu gunlukcu)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dosyaYolu);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        _dosyaYolu = dosyaYolu;
        _gunlukcu = gunlukcu;
    }

    public string? Bul(SunucuAdresi adres)
    {
        lock (_kilit)
        {
            return Oku().GetValueOrDefault(adres.Anahtar);
        }
    }

    // Kullanici "yenisine guven" dediginde ya da ilk baglantida.
    // Yazilamazsa sadece loga yazilir (bir dahaki baglantida yeniden ogrenilir).
    public void Kaydet(SunucuAdresi adres, string parmakIzi)
    {
        lock (_kilit)
        {
            Dictionary<string, string> hepsi = Oku();
            hepsi[adres.Anahtar] = parmakIzi;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_dosyaYolu))!);
                File.WriteAllText(_dosyaYolu, JsonSerializer.Serialize(hepsi));
                _gunlukcu.Bilgi($"Sunucu parmak izi kaydedildi: {adres}");
            }
            catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
            {
                _gunlukcu.Hata(hata, "Parmak izi dosyasi yazilamadi.");
            }
        }
    }

    private Dictionary<string, string> Oku()
    {
        if (!File.Exists(_dosyaYolu))
        {
            return new Dictionary<string, string>();
        }
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_dosyaYolu))
                ?? new Dictionary<string, string>();
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException or JsonException)
        {
            _gunlukcu.Hata(hata, "Parmak izi dosyasi okunamadi; bos kabul edildi.");
            return new Dictionary<string, string>();
        }
    }
}
