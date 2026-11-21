namespace ChatApp.Protocol.Paketler;

// Istemci -> Sunucu. "Yeni sohbet: ID" girilince: bu kisi var mi, adi ne?
public sealed record KullaniciSorgula(int KullaniciId) : Paket;
