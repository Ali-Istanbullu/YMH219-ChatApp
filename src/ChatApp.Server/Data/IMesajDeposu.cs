namespace ChatApp.Server.Data;

// Mesajlarin saklandigi yer. Veritabanina ulasilamazsa DepoHatasi.
public interface IMesajDeposu
{
    // aliciId null ise Genel oda mesaji. Yeni mesajin numarasini dondurur.
    Task<long> KaydetAsync(int gonderenId, int? aliciId, string metin, DateTimeOffset zaman);

    Task IletildiIsaretleAsync(long mesajId, DateTimeOffset zaman);

    // Bu kisiye gelen ama henuz iletilmemis ozel mesajlar (eskiden yeniye).
    Task<IReadOnlyList<KayitliMesaj>> IletilmemisleriGetirAsync(int aliciId);

    // gonderen -> okuyan yonundeki, sonMesajId'ye kadar olan okunmamis mesajlari okundu yapar.
    // Okundu yapilan en buyuk mesaj numarasini dondurur; hicbiri degismediyse null.
    Task<long?> OkunduIsaretleAsync(int okuyanId, int gonderenId, long sonMesajId, DateTimeOffset zaman);

    // Iki kisi arasindaki mesajlar, en yeniden eskiye. oncekiMesajId verilirse ondan eskiler.
    Task<IReadOnlyList<KayitliMesaj>> OzelGecmisAsync(int kullaniciId, int karsiId, long? oncekiMesajId, int adet);

    // Genel oda mesajlari, en yeniden eskiye.
    Task<IReadOnlyList<KayitliMesaj>> GenelGecmisAsync(long? oncekiMesajId, int adet);

    // Bu kisinin ozel mesajlastigi herkes (en son konusulan basta) ve okunmamis sayilari.
    Task<IReadOnlyList<SohbetOzeti>> SohbetleriGetirAsync(int kullaniciId);
}
