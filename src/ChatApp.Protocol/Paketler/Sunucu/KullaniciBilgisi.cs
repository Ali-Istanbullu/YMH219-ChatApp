using ChatApp.Protocol.Modeller;

namespace ChatApp.Protocol.Paketler;

// Sunucu -> Istemci. KullaniciSorgula cevabi.
public sealed record KullaniciBilgisi(int KullaniciId, bool Var, string? GorunenAd, bool Cevrimici) : Paket;
