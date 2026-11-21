using ChatApp.Protocol.Modeller;

namespace ChatApp.Protocol.Paketler;

// Sunucu -> Istemci. Bir kisinin cevrimici durumu degisti.
public sealed record CevrimiciDurumu(int KullaniciId, bool Cevrimici) : Paket;
