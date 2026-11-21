namespace ChatApp.Protocol.Paketler;

// Istemci -> Sunucu. "Bu kisiden gelen, su numaraya kadar olan mesajlari okudum."
public sealed record OkunduBildir(int KarsiId, long SonMesajId) : Paket;
