using System.Text.Json;
using System.Text.Json.Serialization;

namespace ChatApp.Protocol.Iletisim;

// Iki tarafin da ayni JSON ayarlarini kullanmasi icin tek yer.
public static class ProtokolAyarlari
{
    public static readonly JsonSerializerOptions Json = new()
    {
        // Enum'lar sayi degil isim olarak gider: "Durum":"Okundu" (okunakli, sira degisse de bozulmaz).
        Converters = { new JsonStringEnumConverter() },

        // "tur" alani satirin basinda olmasa da paket taninsin.
        AllowOutOfOrderMetadataProperties = true,

        // Eksik ya da null gelen zorunlu alan = bozuk paket (sonradan NullReference'a donusmesin).
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,

        MaxDepth = 16
    };
}
