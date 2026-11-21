using System.Security.Cryptography;
using System.Text;

namespace ChatApp.Server.Security;

// Sifreleri veritabanina ACIK yazmamak icin hash'ler.
// PBKDF2 + SHA-256: sifre, her kullaniciya ozel rastgele bir "tuz" ile 600.000 kez karistirilir.
//   - tuz: iki kisinin sifresi ayni olsa bile hash'leri farkli olur, hazir tablolarla kirilamaz
//   - cok tekrar: bir denemeyi yavaslatir; caldigi hash'i deneme-yanilma ile kirmak cok uzun surer
// 600.000 tekrar, OWASP'in PBKDF2-HMAC-SHA256 icin onerdigi degerdir.
public sealed class SifreHashleyici
{
    public const int VarsayilanTekrar = 600_000;
    private const int TuzUzunlugu = 16;    // bayt
    private const int HashUzunlugu = 32;   // bayt (SHA-256)

    private readonly int _tekrar;

    // tekrar: testler hizli calissin diye kucultulebilir; gercek sunucu varsayilani kullanir.
    public SifreHashleyici(int tekrar = VarsayilanTekrar)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(tekrar, 1);
        _tekrar = tekrar;
    }

    public (byte[] Hash, byte[] Tuz) Hashle(string sifre)
    {
        byte[] tuz = RandomNumberGenerator.GetBytes(TuzUzunlugu);
        return (Hesapla(sifre, tuz), tuz);
    }

    public bool DogruMu(string sifre, byte[] beklenenHash, byte[] tuz) =>
        // Sabit sureli karsilastirma: "kacinci baytta yanlis" bilgisi zamandan sizmasin.
        CryptographicOperations.FixedTimeEquals(Hesapla(sifre, tuz), beklenenHash);

    // Kullanici adi bulunamadiginda da ayni sure harcansin diye bos yere hash hesaplanir.
    // Boylece cevabin hizindan "bu kullanici adi var mi" anlasilamaz.
    public void BosunaHesapla(string sifre) => Hesapla(sifre, new byte[TuzUzunlugu]);

    private byte[] Hesapla(string sifre, byte[] tuz) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(sifre), tuz, _tekrar, HashAlgorithmName.SHA256, HashUzunlugu);
}
