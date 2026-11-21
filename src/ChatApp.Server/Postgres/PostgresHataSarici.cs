using System.Net.Sockets;
using ChatApp.Server.Data;
using Npgsql;

namespace ChatApp.Server.Postgres;

// Veritabani cagrilarini sarar: BEKLENEN hatalari (PostgreSQL kapali, baglanti koptu, zaman asimi,
// sifre yanlis) DepoHatasi'na cevirir. KOD hatalari (SQL yazim hatasi, olmayan sutun, cok uzun deger,
// kural ihlali) oldugu gibi yukari cikar: onlar bug'dir, gizlenmemeli.
internal static class PostgresHataSarici
{
    public static async Task<T> CalistirAsync<T>(Func<Task<T>> islem)
    {
        try
        {
            return await islem();
        }
        catch (Exception hata) when (BeklenenMi(hata))
        {
            throw new DepoHatasi("Veritabanina erisilemedi.", hata);
        }
    }

    public static async Task CalistirAsync(Func<Task> islem) =>
        await CalistirAsync(async () =>
        {
            await islem();
            return 0;
        });

    // PostgreSQL hata kodlari (SqlState) siniflari:
    //   22 veri hatasi, 23 kural ihlali, 42 SQL yazim/tanim hatasi  -> bizim kodumuzdaki hata
    //   08 baglanti, 28 kimlik dogrulama, 53 kaynak yetersiz, 57 sunucu kapaniyor, ... -> dis dunya
    private static bool BeklenenMi(Exception hata) => hata switch
    {
        PostgresException p => !(p.SqlState.StartsWith("22") || p.SqlState.StartsWith("23") || p.SqlState.StartsWith("42")),
        NpgsqlException => true,
        SocketException or TimeoutException or IOException => true,
        _ => false
    };
}
