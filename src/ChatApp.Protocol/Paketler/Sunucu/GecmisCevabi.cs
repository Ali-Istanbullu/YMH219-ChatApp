using ChatApp.Protocol.Modeller;

namespace ChatApp.Protocol.Paketler;

// Sunucu -> Istemci. Gecmis mesajlar (eskiden yeniye). DahaVar: daha eski mesaj var mi.
public sealed record GecmisCevabi(int? KarsiId, IReadOnlyList<MesajBilgisi> Mesajlar, bool DahaVar) : Paket;
