using System.Diagnostics.CodeAnalysis;

namespace ChatApp.Client.Network;

// Kullanicinin yazdigi sunucu adresi: "localhost", "192.168.1.20:5050", "0.tcp.eu.ngrok.io:12345".
// Port yazilmazsa 5050.
public sealed record SunucuAdresi(string Sunucu, int Port)
{
    public const int VarsayilanPort = 5050;

    // Parmak izi deposunda anahtar olarak kullanilir.
    public string Anahtar => $"{Sunucu.ToLowerInvariant()}:{Port}";

    public static bool TryParse(string? metin, [NotNullWhen(true)] out SunucuAdresi? adres)
    {
        adres = null;
        string temiz = (metin ?? string.Empty).Trim();
        if (temiz.Length == 0 || temiz.Any(char.IsWhiteSpace))
        {
            return false;
        }

        string sunucu = temiz;
        int port = VarsayilanPort;
        string[] parcalar = temiz.Split(':');
        if (parcalar.Length == 2)
        {
            sunucu = parcalar[0];
            if (!int.TryParse(parcalar[1], out port) || port is < 1 or > 65535)
            {
                return false;
            }
        }
        else if (parcalar.Length > 2)
        {
            return false;   // IPv6 adresleri desteklenmiyor (sinirlamalarda yazili)
        }

        if (sunucu.Length == 0)
        {
            return false;
        }
        adres = new SunucuAdresi(sunucu, port);
        return true;
    }

    public override string ToString() => Port == VarsayilanPort ? Sunucu : $"{Sunucu}:{Port}";
}
