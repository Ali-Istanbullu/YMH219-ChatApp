namespace ChatApp.Server.Data;

// DepoHatasi'ni sunucu penceresinde gosterilecek Turkce metne cevirir.
// Ilk satir: ne oldu ve ne yapilmali. Ikinci satir: PostgreSQL/Npgsql'in kendi (Ingilizce) mesaji.
public static class DepoHatasiAciklayici
{
    public static string Acikla(DepoHatasi hata)
    {
        ArgumentNullException.ThrowIfNull(hata);
        string aciklama = hata.Sebep switch
        {
            DepoHatasiSebebi.BaglantiKurulamadi =>
                "Bu adres ve portta çalışan bir PostgreSQL yok. Port numarasını ve PostgreSQL servisinin açık olduğunu kontrol edin.",
            DepoHatasiSebebi.SunucuBulunamadi => "Sunucu adresi bulunamadı. Adresi kontrol edin (bu bilgisayar için: localhost).",
            DepoHatasiSebebi.KimlikDogrulanamadi => "Kullanıcı adı ya da şifre yanlış.",
            DepoHatasiSebebi.VeritabaniYok => "Bu adla bir veritabanı yok. Veritabanı adını kontrol edin.",
            DepoHatasiSebebi.ZamanAsimi => "PostgreSQL zamanında cevap vermedi (zaman aşımı). Adres ya da port yanlış olabilir veya PostgreSQL kapalı/meşgul.",
            _ => "Veritabanı bağlantısında sorun oluştu."
        };
        string? teknik = hata.InnerException?.Message;
        return string.IsNullOrWhiteSpace(teknik) ? aciklama : $"{aciklama}\nTeknik ayrıntı: {teknik}";
    }
}
