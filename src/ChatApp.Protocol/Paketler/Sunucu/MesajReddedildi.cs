using ChatApp.Protocol.Modeller;

namespace ChatApp.Protocol.Paketler;

// Sunucu -> Istemci. Gonderilen mesaj kabul edilmedi (gecersiz, cok hizli, alici yok...).
public sealed record MesajReddedildi(Guid IstemciMesajId, HataKodu Kod) : Paket;
