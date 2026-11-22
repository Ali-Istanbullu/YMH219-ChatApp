namespace ChatApp.Client.Session;

public enum BaglantiDurumu
{
    Kapali,              // giris yapilmamis ya da oturum bitti
    Bagli,
    YenidenBaglaniyor    // baglanti koptu, otomatik deneniyor
}
