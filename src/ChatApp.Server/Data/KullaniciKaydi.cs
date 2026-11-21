namespace ChatApp.Server.Data;

// kullanicilar tablosundaki bir satir.
public sealed record KullaniciKaydi(
    int Id,
    string KullaniciAdi,
    string GorunenAd,
    byte[] SifreHash,
    byte[] Tuz,
    DateTimeOffset KayitZamani,
    DateTimeOffset? SonGiris,
    bool Yasakli,
    string? YasakSebebi,
    DateTimeOffset? YasakZamani);
