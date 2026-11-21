using ChatApp.Protocol.Modeller;

namespace ChatApp.Protocol.Paketler;

// Sunucu -> Istemci. Sunucu mesaji aldi ve kaydetti (tek tik).
public sealed record MesajKabulEdildi(Guid IstemciMesajId, MesajBilgisi Mesaj) : Paket;
