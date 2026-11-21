namespace ChatApp.Server.Data;

// Hesaplarin saklandigi yer. Gercegi PostgreSQL; testlerde bellekte calisan sahtesi var.
// Veritabanina ulasilamazsa butun metotlar DepoHatasi firlatir.
public interface IKullaniciDeposu
{
    // Hesabi olusturur ve davet kodunu kullanildi olarak isaretler. Ikisi tek islemde (transaction):
    // biri basarisiz olursa ikisi de geri alinir.
    Task<KayitDurumu> OlusturAsync(YeniKullanici kullanici, string davetKodu);

    Task<KullaniciKaydi?> KullaniciAdiylaBulAsync(string kullaniciAdi);
    Task<KullaniciKaydi?> IdIleBulAsync(int id);
    Task SonGirisiYazAsync(int id, DateTimeOffset zaman);
    Task SifreyiGuncelleAsync(int id, byte[] sifreHash, byte[] tuz);
    Task YasakDurumunuYazAsync(int id, bool yasakli, string? sebep, DateTimeOffset? zaman);
    Task<IReadOnlyList<KullaniciKaydi>> YasaklilariGetirAsync();
    Task<int> HesapSayisiAsync();
}
