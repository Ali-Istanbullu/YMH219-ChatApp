namespace ChatApp.Protocol.Paketler;

// Istemci -> Sunucu. Giris istegi.
public sealed record GirisIstegi(string KullaniciAdi, string Sifre) : Paket;
