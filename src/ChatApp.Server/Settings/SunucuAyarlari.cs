namespace ChatApp.Server.Settings;

// Sunucu penceresinde girilen ayarlar: veritabani baglantisi + istemcilerin baglanacagi port.
// Sifre bellekte acik durur; diske AyarDeposu DPAPI ile sifreleyerek yazar.
public sealed record SunucuAyarlari(
    string VeritabaniSunucusu,
    int VeritabaniPortu,
    string VeritabaniAdi,
    string VeritabaniKullanicisi,
    string VeritabaniSifresi,
    int DinlemePortu)
{
    public const int VarsayilanDinlemePortu = 5050;

    // Ilk acilista pencerede hazir gelen degerler (Ali'nin bilgisayarindaki kurulum).
    public static SunucuAyarlari Varsayilan { get; } =
        new("localhost", 5433, "chatapp", "chatapp", string.Empty, VarsayilanDinlemePortu);
}
