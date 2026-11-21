namespace ChatApp.Protocol.Paketler;

// Istemci -> Sunucu. Engelle (true) ya da engeli kaldir (false).
public sealed record EngelleIstegi(int KullaniciId, bool Engelle) : Paket;
