using ChatApp.Protocol.Modeller;

namespace ChatApp.Protocol.Paketler;

// Sunucu -> Istemci. Giris cevabi. Aciklama: yasak sebebi gibi ek bilgi.
public sealed record GirisSonucu(bool Basarili, int? KullaniciId, string? GorunenAd, HataKodu? Hata, string? Aciklama) : Paket;
