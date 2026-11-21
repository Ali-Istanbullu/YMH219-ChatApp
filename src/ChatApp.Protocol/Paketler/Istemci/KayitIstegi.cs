namespace ChatApp.Protocol.Paketler;

// Istemci -> Sunucu. Yeni hesap acma istegi. Davet kodu tek kullanimliktir.
public sealed record KayitIstegi(string KullaniciAdi, string GorunenAd, string Sifre, string DavetKodu) : Paket;
