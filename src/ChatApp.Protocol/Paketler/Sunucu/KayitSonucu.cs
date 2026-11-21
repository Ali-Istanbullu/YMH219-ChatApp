using ChatApp.Protocol.Modeller;

namespace ChatApp.Protocol.Paketler;

// Sunucu -> Istemci. Kayit cevabi. Basariliysa yeni hesabin 6 haneli numarasi gelir.
public sealed record KayitSonucu(bool Basarili, int? KullaniciId, HataKodu? Hata) : Paket;
