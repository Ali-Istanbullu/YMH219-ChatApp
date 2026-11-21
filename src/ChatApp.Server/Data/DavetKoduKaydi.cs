namespace ChatApp.Server.Data;

// davet_kodlari tablosundaki bir satir. KullananId null ise kod henuz kullanilmamis.
public sealed record DavetKoduKaydi(
    string Kod,
    DateTimeOffset OlusturmaZamani,
    int? KullananId,
    string? KullananAdi,
    DateTimeOffset? KullanilmaZamani);
