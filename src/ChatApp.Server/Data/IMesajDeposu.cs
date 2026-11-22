namespace ChatApp.Server.Data;

// Mesajlarin saklandigi yer. Veritabanina ulasilamazsa DepoHatasi.
// Silme islemleri satiri SILMEZ, sadece isaretler (Ali karari: yasak sebebine kanit kalsin).
// Gercekten silen tek yer EnEskileriSilAsync (500 MB -> 250 MB temizligi).
public interface IMesajDeposu
{
    // aliciId null ise Genel oda mesaji. Ayni gonderen + istemciMesajId daha once kaydedildiyse
    // (baglanti koptu, istemci tekrar gonderdi) yeni satir acilmaz: eskisinin numarasi ve Yeni = false doner.
    Task<(long Id, bool Yeni)> KaydetAsync(int gonderenId, int? aliciId, string metin, DateTimeOffset zaman, Guid istemciMesajId);

    Task<KayitliMesaj?> MesajBulAsync(long mesajId);

    Task IletildiIsaretleAsync(long mesajId, DateTimeOffset zaman);

    // Bu kisiye gelen ama henuz iletilmemis ozel mesajlar (eskiden yeniye).
    Task<IReadOnlyList<KayitliMesaj>> IletilmemisleriGetirAsync(int aliciId);

    // gonderen -> okuyan yonundeki, sonMesajId'ye kadar olan okunmamis mesajlari okundu yapar.
    // Okundu yapilan en buyuk mesaj numarasini dondurur; hicbiri degismediyse null.
    Task<long?> OkunduIsaretleAsync(int okuyanId, int gonderenId, long sonMesajId, DateTimeOffset zaman);

    // Iki kisi arasindaki, kullanicinin GORDUGU mesajlar (gizledikleri ve sildigi sohbetin eskileri haric),
    // en yeniden eskiye. oncekiMesajId verilirse ondan eskiler.
    Task<IReadOnlyList<KayitliMesaj>> OzelGecmisAsync(int kullaniciId, int karsiId, long? oncekiMesajId, int adet);

    // Genel oda mesajlari (kullanicinin gizledikleri haric), en yeniden eskiye.
    Task<IReadOnlyList<KayitliMesaj>> GenelGecmisAsync(int kullaniciId, long? oncekiMesajId, int adet);

    // Bu kisinin ozel mesajlastigi herkes (gorunen mesaji olanlar; en son konusulan basta) ve okunmamis sayilari.
    Task<IReadOnlyList<SohbetOzeti>> SohbetleriGetirAsync(int kullaniciId);

    // "Benden sil": mesaj sadece bu kullanicidan gizlenir.
    Task GizleAsync(int kullaniciId, long mesajId, DateTimeOffset zaman);

    // "Herkesten sil": mesaj isaretlenir, metni veritabaninda kalir.
    Task HerkestenSilAsync(long mesajId, DateTimeOffset zaman);

    // "Sohbeti sil": su ana kadarki mesajlar bu kullanicidan gizlenir; sonra gelenler gorunur.
    Task SohbetiSilAsync(int kullaniciId, int karsiId, DateTimeOffset zaman);

    // ---- Yonetici ekrani (silinenler dahil) ----
    Task<IReadOnlyList<YonetimSohbetOzeti>> YonetimSohbetleriAsync(int kullaniciId);
    Task<IReadOnlyList<YonetimMesajKaydi>> YonetimOzelKayitlarAsync(int kullaniciId, int karsiId, long? oncekiMesajId, int adet);
    Task<IReadOnlyList<YonetimMesajKaydi>> YonetimGenelKayitlarAsync(long? oncekiMesajId, int adet);

    // ---- Boyut siniri ----
    // Mesaj tablosunun diskte kapladigi yer (bayt, indeksler dahil).
    Task<long> TabloBoyutuAsync();

    Task<long> MesajSayisiAsync();

    // En yeni "tutulacakAdet" mesaj kalir, daha eskiler (silinmis/gizlenmis olanlar dahil) gercekten silinir.
    Task<long> EnEskileriSilAsync(long tutulacakAdet);

    // Silinen satirlarin yerini diske geri verir (tablo dosyasi gercekten kuculur).
    Task SikistirAsync();
}
