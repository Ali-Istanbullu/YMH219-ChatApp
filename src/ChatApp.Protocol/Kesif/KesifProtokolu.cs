using System.Text;
using System.Text.Json;

namespace ChatApp.Protocol.Kesif;

// Ayni agda sunucuyu otomatik bulma ("kesif"):
//   ChatApp, UDP ile agin yayin adresine "CHATAPP-KESIF 1" yollar.
//   ChatServer cevap verir: {"Uygulama":"ChatApp","Port":5050}. Sunucunun adresi, cevabin geldigi IP'dir.
// Boylece son kullanici IP adresi yazmak zorunda kalmaz.
public static class KesifProtokolu
{
    public const int VarsayilanPort = 5051;
    public const string UygulamaAdi = "ChatApp";

    // Tanimadigimiz paketlere cevap verilmez; en fazla bu kadar bayt okunur.
    public const int EnFazlaPaket = 512;

    public static byte[] Istek { get; } = Encoding.ASCII.GetBytes("CHATAPP-KESIF 1");

    public static bool IstekMi(ReadOnlySpan<byte> veri) => veri.SequenceEqual(Istek);

    public static byte[] CevapOlustur(int tcpPortu) =>
        JsonSerializer.SerializeToUtf8Bytes(new KesifCevabi(UygulamaAdi, tcpPortu));

    // Gecerli bir ChatServer cevabiysa TCP portunu dondurur; degilse null.
    public static int? CevaptanPort(ReadOnlySpan<byte> veri)
    {
        if (veri.Length is 0 or > EnFazlaPaket)
        {
            return null;
        }
        try
        {
            KesifCevabi? cevap = JsonSerializer.Deserialize<KesifCevabi>(veri);
            return cevap is { Uygulama: UygulamaAdi, Port: >= 1 and <= 65535 } ? cevap.Port : null;
        }
        catch (JsonException)
        {
            return null;   // baska bir programin paketi; yok say
        }
    }

    private sealed record KesifCevabi(string Uygulama, int Port);
}
