using ChatApp.Protocol.Modeller;

namespace ChatApp.Protocol.Paketler;

// Sunucu -> Istemci. Sunucu oturumu kapatiyor (atildin, yasaklandin, baska yerden giris yapildi).
public sealed record OturumSonlandi(OturumSonuSebebi Sebep, string? Aciklama) : Paket;
