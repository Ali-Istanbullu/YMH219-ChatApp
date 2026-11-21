namespace ChatApp.Server.Data;

// Kim kimi engelledi. Veritabanina ulasilamazsa DepoHatasi.
public interface IEngelDeposu
{
    Task EngelleAsync(int engelleyenId, int engellenenId, DateTimeOffset zaman);
    Task EngeliKaldirAsync(int engelleyenId, int engellenenId);
    Task<bool> EngelliMiAsync(int engelleyenId, int engellenenId);

    // Bu kisinin engelledikleri.
    Task<IReadOnlySet<int>> EngellediklerimAsync(int engelleyenId);

    // Bu kisiyi engelleyenler.
    Task<IReadOnlySet<int>> BeniEngelleyenlerAsync(int engellenenId);
}
