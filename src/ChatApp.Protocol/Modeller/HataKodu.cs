namespace ChatApp.Protocol.Modeller;

// Sunucunun istemciye bildirdigi hata turleri. Istemci bunu kullaniciya Turkce mesaja cevirir.
public enum HataKodu
{
    GecersizIstek,
    GirisGerekli,
    KullaniciAdiGecersiz,
    GorunenAdGecersiz,
    SifreGecersiz,
    KullaniciAdiAlinmis,
    DavetKoduGecersiz,
    KullaniciAdiVeyaSifreYanlis,
    HesapGeciciKilitli,
    HesapYasakli,
    KullaniciBulunamadi,
    KendineMesaj,
    EngelledigimKisi,
    MesajGecersiz,
    HizSiniriAsildi,
    EskiSifreYanlis,
    SunucuHatasi,
    MesajBulunamadi,
    SadeceKendiMesajin
}
