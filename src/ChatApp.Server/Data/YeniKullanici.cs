namespace ChatApp.Server.Data;

// Kayit sirasinda veritabanina eklenecek hesap.
public sealed record YeniKullanici(
    int Id,
    string KullaniciAdi,
    string GorunenAd,
    byte[] SifreHash,
    byte[] Tuz,
    DateTimeOffset KayitZamani);
