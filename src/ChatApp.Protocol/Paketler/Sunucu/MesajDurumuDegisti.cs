using ChatApp.Protocol.Modeller;

namespace ChatApp.Protocol.Paketler;

// Sunucu -> Istemci. Gonderdigin bir mesaj karsi tarafa iletildi (cift tik).
public sealed record MesajDurumuDegisti(long MesajId, MesajDurumu Durum) : Paket;
