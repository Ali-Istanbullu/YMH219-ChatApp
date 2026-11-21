using System.Security.Cryptography;

namespace ChatApp.Server.Security;

// "K7M2-QX9P" biciminde rastgele davet kodu uretir.
// Karismasin diye 0/O ve 1/I kullanilmaz. 32 harf, 8 karakter: 32^8 = ~1 trilyon olasilik (tahmin edilemez).
public static class DavetKoduUreteci
{
    private const string Harfler = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Uret()
    {
        char[] k = RandomNumberGenerator.GetItems<char>(Harfler, 8);
        return $"{new string(k, 0, 4)}-{new string(k, 4, 4)}";
    }
}
