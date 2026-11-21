using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ChatApp.Server.Abstractions;

namespace ChatApp.Server.Security;

// TLS icin sunucunun kendi sertifikasi.
// Ilk acilista uretilir (RSA 2048, 5 yil) ve diske SIFRELI yazilir (ISirKoruyucu = Windows'ta DPAPI).
// Sonraki acilislarda ayni sertifika yuklenir; boylece istemcilerin hatirladigi parmak izi degismez.
// Sertifika resmi bir kurumdan alinmadigi icin istemciler ona "ilk baglantida guven, sonra hatirla"
// yontemiyle guvenir (SSH'teki gibi).
public sealed class SertifikaDeposu
{
    public const string DosyaAdi = "sunucu-sertifikasi.dat";
    private const string SertifikaAdi = "CN=ChatApp Sunucusu";
    private const string SunucuKimlikDogrulamaOid = "1.3.6.1.5.5.7.3.1";

    private readonly string _dosyaYolu;
    private readonly ISirKoruyucu _sirKoruyucu;
    private readonly ISaat _saat;
    private readonly IGunlukcu _gunlukcu;

    public SertifikaDeposu(string klasor, ISirKoruyucu sirKoruyucu, ISaat saat, IGunlukcu gunlukcu)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(klasor);
        ArgumentNullException.ThrowIfNull(sirKoruyucu);
        ArgumentNullException.ThrowIfNull(saat);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        _dosyaYolu = Path.Combine(klasor, DosyaAdi);
        _sirKoruyucu = sirKoruyucu;
        _saat = saat;
        _gunlukcu = gunlukcu;
    }

    // Dosya okunamazsa IOException / UnauthorizedAccessException yukari cikar (beklenen; pencere gosterir).
    public X509Certificate2 YukleVeyaOlustur()
    {
        if (File.Exists(_dosyaYolu))
        {
            try
            {
                byte[] pfx = _sirKoruyucu.Coz(File.ReadAllBytes(_dosyaYolu));
                return X509CertificateLoader.LoadPkcs12(pfx, password: null);
            }
            catch (CryptographicException hata)
            {
                // Dosya bozuk ya da baska bir Windows kullanicisi tarafindan sifrelenmis.
                // Yenisi uretilir; istemciler "sunucu degisti" uyarisi gorur.
                _gunlukcu.Hata(hata, "Sunucu sertifikasi okunamadi, yenisi uretiliyor.");
            }
        }

        byte[] yeniPfx = Uret();
        Directory.CreateDirectory(Path.GetDirectoryName(_dosyaYolu)!);
        File.WriteAllBytes(_dosyaYolu, _sirKoruyucu.Koru(yeniPfx));
        _gunlukcu.Bilgi("Yeni sunucu sertifikasi uretildi.");
        return X509CertificateLoader.LoadPkcs12(yeniPfx, password: null);
    }

    // Kullanicinin karsilastirabilecegi parmak izi (SHA-256, onaltilik).
    public static string ParmakIzi(X509Certificate2 sertifika) =>
        sertifika.GetCertHashString(HashAlgorithmName.SHA256);

    private byte[] Uret()
    {
        using RSA anahtar = RSA.Create(2048);
        var istek = new CertificateRequest(SertifikaAdi, anahtar, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        istek.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        istek.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        istek.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid(SunucuKimlikDogrulamaOid) }, false));

        DateTimeOffset simdi = _saat.SimdiUtc;
        using X509Certificate2 sertifika = istek.CreateSelfSigned(simdi.AddDays(-1), simdi.AddYears(5));
        return sertifika.Export(X509ContentType.Pfx);
    }
}
