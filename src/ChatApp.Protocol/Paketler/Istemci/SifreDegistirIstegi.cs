namespace ChatApp.Protocol.Paketler;

// Istemci -> Sunucu. Sifre degistirme.
public sealed record SifreDegistirIstegi(string EskiSifre, string YeniSifre) : Paket;
