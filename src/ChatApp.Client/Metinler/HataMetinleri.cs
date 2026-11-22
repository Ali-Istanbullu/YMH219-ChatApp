using ChatApp.Protocol.Modeller;

namespace ChatApp.Client.Metinler;

// Sunucudan gelen hata kodlarinin kullaniciya gosterilecek Turkce karsiliklari.
public static class HataMetinleri
{
    public static string Acikla(HataKodu? kod) => kod switch
    {
        HataKodu.GecersizIstek => "Geçersiz istek.",
        HataKodu.GirisGerekli => "Önce giriş yapmalısınız.",
        HataKodu.KullaniciAdiGecersiz => "Kullanıcı adı 3-20 karakter olmalı; sadece küçük harf (a-z), rakam, nokta ve alt çizgi.",
        HataKodu.GorunenAdGecersiz => "Görünen ad 1-40 karakter olmalı.",
        HataKodu.SifreGecersiz => "Şifre 8-128 karakter olmalı.",
        HataKodu.KullaniciAdiAlinmis => "Bu kullanıcı adı alınmış.",
        HataKodu.DavetKoduGecersiz => "Davet kodu geçersiz ya da daha önce kullanılmış.",
        HataKodu.KullaniciAdiVeyaSifreYanlis => "Kullanıcı adı ya da şifre yanlış.",
        HataKodu.HesapGeciciKilitli => "Çok fazla yanlış deneme. 1 dakika bekleyip tekrar deneyin.",
        HataKodu.HesapYasakli => "Hesabınız yasaklandı.",
        HataKodu.KullaniciBulunamadi => "Kullanıcı bulunamadı.",
        HataKodu.KendineMesaj => "Kendinize mesaj gönderemezsiniz.",
        HataKodu.EngelledigimKisi => "Bu kişiyi engellediniz; mesaj göndermek için önce engeli kaldırın.",
        HataKodu.MesajGecersiz => "Mesaj boş olamaz ve en fazla 2000 karakter olabilir.",
        HataKodu.HizSiniriAsildi => "Çok hızlı işlem yapıyorsunuz. Biraz bekleyip tekrar deneyin.",
        HataKodu.EskiSifreYanlis => "Mevcut şifre yanlış.",
        HataKodu.SunucuHatasi => "Sunucuda geçici bir sorun var. Biraz sonra tekrar deneyin.",
        HataKodu.MesajBulunamadi => "Mesaj bulunamadı (silinmiş ya da eski mesajlar temizlenmiş olabilir).",
        HataKodu.SadeceKendiMesajin => "Sadece kendi mesajınızı herkesten silebilirsiniz.",
        _ => "Bilinmeyen bir hata oluştu."
    };

    public static string Acikla(OturumSonuSebebi sebep, string? aciklama) => sebep switch
    {
        OturumSonuSebebi.BaskaYerdenGiris => "Hesabınıza başka bir yerden giriş yapıldı; bu oturum kapatıldı.",
        OturumSonuSebebi.Atildi => "Yönetici bağlantınızı kesti.",
        OturumSonuSebebi.Yasaklandi => "Hesabınız yasaklandı." + (string.IsNullOrWhiteSpace(aciklama) ? "" : $" Sebep: {aciklama}"),
        _ => "Sunucu kapandı."
    };

    // Giris basarisizsa: yasak sebebi varsa eklenir.
    public static string GirisHatasi(HataKodu? kod, string? aciklama) =>
        kod == HataKodu.HesapYasakli && !string.IsNullOrWhiteSpace(aciklama)
            ? $"{Acikla(kod)} Sebep: {aciklama}"
            : Acikla(kod);
}
