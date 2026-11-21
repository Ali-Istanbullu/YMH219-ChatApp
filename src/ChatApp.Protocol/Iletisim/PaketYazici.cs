using System.Text;
using System.Text.Json;
using ChatApp.Protocol.Paketler;

namespace ChatApp.Protocol.Iletisim;

// Paketi tek satir JSON'a cevirir: {"tur":"...",...}\n
public static class PaketYazici
{
    public static byte[] Serilestir(Paket paket)
    {
        ArgumentNullException.ThrowIfNull(paket);
        string json = JsonSerializer.Serialize(paket, ProtokolAyarlari.Json);
        return Encoding.UTF8.GetBytes(json + "\n");
    }
}
