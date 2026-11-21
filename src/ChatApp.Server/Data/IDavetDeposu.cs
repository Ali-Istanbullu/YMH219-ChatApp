namespace ChatApp.Server.Data;

// Tek kullanimlik davet kodlari. Veritabanina ulasilamazsa DepoHatasi.
public interface IDavetDeposu
{
    // Kod zaten varsa false (cok dusuk ihtimal; yenisi uretilir).
    Task<bool> EkleAsync(string kod, DateTimeOffset zaman);

    Task<IReadOnlyList<DavetKoduKaydi>> HepsiniGetirAsync();
}
