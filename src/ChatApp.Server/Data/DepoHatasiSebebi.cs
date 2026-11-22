namespace ChatApp.Server.Data;

// Veritabanina neden ulasilamadi? Sunucu penceresi kullaniciya buna gore Turkce aciklama gosterir.
public enum DepoHatasiSebebi
{
    Diger,
    BaglantiKurulamadi,     // o adres/portta PostgreSQL yok ya da kapali
    SunucuBulunamadi,       // adres (host adi) bulunamadi
    KimlikDogrulanamadi,    // kullanici adi / sifre yanlis
    VeritabaniYok,          // bu adla veritabani yok
    ZamanAsimi              // PostgreSQL zamaninda cevap vermedi
}
