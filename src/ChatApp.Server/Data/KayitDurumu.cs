namespace ChatApp.Server.Data;

// Hesap olusturma denemesinin sonucu.
public enum KayitDurumu
{
    Basarili,
    KullaniciAdiAlinmis,
    IdCakisti,          // rastgele uretilen 6 haneli numara dolu cikti: yenisiyle tekrar denenir
    DavetKoduGecersiz   // kod yok ya da daha once kullanilmis
}
