using ChatApp.Protocol.Modeller;

namespace ChatApp.Protocol.Paketler;

// Sunucu -> Istemci. Bir istegin reddedildigini bildirir (ornegin hiz siniri asildi).
public sealed record HataBildirimi(HataKodu Kod, string Aciklama) : Paket;
