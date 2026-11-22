namespace ChatApp.Client.Network;

// Kullanicinin elle yazdigi sertifika parmak izini duzenler.
// ChatServer penceresindeki "Sertifika parmak izi" SHA-256'dir: 64 onaltilik karakter (0-9, A-F).
// Kopyalarken araya giren bosluk, ':' ve '-' yok sayilir; buyuk/kucuk harf fark etmez.
public static class ParmakIziBicimi
{
    public const int Uzunluk = 64;

    // Gecerliyse "AB12...", degilse null.
    public static string? Duzenle(string? metin)
    {
        if (string.IsNullOrWhiteSpace(metin))
        {
            return null;
        }
        string temiz = new(metin.Where(c => !char.IsWhiteSpace(c) && c is not ':' and not '-').ToArray());
        return temiz.Length == Uzunluk && temiz.All(char.IsAsciiHexDigit) ? temiz.ToUpperInvariant() : null;
    }
}
