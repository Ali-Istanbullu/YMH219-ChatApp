using System.Text.RegularExpressions;

namespace ChatApp.Protocol.Kurallar;

// Kullanicinin yazdigi her seyin kurallari. Istemci ve sunucu AYNI kurallari kullanir:
//   - istemci: kullanici bosuna istek atmasin diye onceden uyarir (kolaylik)
//   - sunucu : asil karari verir, cunku istemci degistirilebilir ("istemciye guvenme")
public static partial class GirdiKurallari
{
    public const int KullaniciAdiEnAz = 3;
    public const int KullaniciAdiEnFazla = 20;
    public const int GorunenAdEnFazla = 40;
    public const int SifreEnAz = 8;
    public const int SifreEnFazla = 128;
    public const int MesajEnFazla = 2000;

    // Kullanici adi: kucuk harf, rakam, nokta, alt cizgi; 3-20 karakter.
    public static bool KullaniciAdiGecerliMi(string? kullaniciAdi) =>
        kullaniciAdi is not null && KullaniciAdiDeseni().IsMatch(kullaniciAdi);

    // Gorunen ad: bas/son bosluklari atilir; 1-40 karakter, kontrol karakteri olamaz.
    // Gecersizse null doner.
    public static string? GorunenAdiDuzenle(string? gorunenAd)
    {
        string? duzenli = gorunenAd?.Trim();
        if (string.IsNullOrEmpty(duzenli) || duzenli.Length > GorunenAdEnFazla || duzenli.Any(char.IsControl))
        {
            return null;
        }
        return duzenli;
    }

    public static bool SifreGecerliMi(string? sifre) =>
        sifre is not null && sifre.Length is >= SifreEnAz and <= SifreEnFazla;

    // Mesaj: bos olamaz, en fazla 2000 karakter; satir sonu ve tab disinda kontrol karakteri olamaz.
    public static bool MesajGecerliMi(string? metin) =>
        !string.IsNullOrWhiteSpace(metin)
        && metin.Length <= MesajEnFazla
        && !metin.Any(k => char.IsControl(k) && k is not '\n' and not '\t');

    // Davet kodu: "K7M2-QX9P" bicimi. Karismasin diye 0/O ve 1/I harfleri kullanilmaz.
    // Kucuk harfle yazilsa da kabul edilir. Gecersizse null doner.
    public static string? DavetKodunuDuzenle(string? kod)
    {
        string? duzenli = kod?.Trim().ToUpperInvariant();
        return duzenli is not null && DavetKoduDeseni().IsMatch(duzenli) ? duzenli : null;
    }

    [GeneratedRegex("^[a-z0-9._]{3,20}$")]
    private static partial Regex KullaniciAdiDeseni();

    [GeneratedRegex("^[A-HJ-NP-Z2-9]{4}-[A-HJ-NP-Z2-9]{4}$")]
    private static partial Regex DavetKoduDeseni();
}
