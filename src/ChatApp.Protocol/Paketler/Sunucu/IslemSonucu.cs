using ChatApp.Protocol.Modeller;

namespace ChatApp.Protocol.Paketler;

// Sunucu -> Istemci. Engelle / engel kaldir / sifre degistir cevabi.
public sealed record IslemSonucu(IslemTuru Islem, bool Basarili, HataKodu? Hata) : Paket;
