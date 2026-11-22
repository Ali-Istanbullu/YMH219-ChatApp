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
            throw new DepoHatasi("Veritabanina erisilemedi.", hata, SebebiBul(hata));
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
    // Kullaniciya dogru aciklama gosterilebilsin diye hatanin turu belirlenir.
    //   28P01 / 28000: kimlik dogrulama; 3D000: veritabani yok (PostgreSQL hata kodlari).
    internal static DepoHatasiSebebi SebebiBul(Exception hata)
    {
        if (hata is PostgresException { SqlState: "28P01" or "28000" })
        {
            return DepoHatasiSebebi.KimlikDogrulanamadi;
        }
        if (hata is PostgresException { SqlState: "3D000" })
        {
            return DepoHatasiSebebi.VeritabaniYok;
        }
        if (IcHata<SocketException>(hata) is { } soket)
        {
            return soket.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain
                ? DepoHatasiSebebi.SunucuBulunamadi
                : DepoHatasiSebebi.BaglantiKurulamadi;
        }
        if (IcHata<TimeoutException>(hata) is not null)
        {
            return DepoHatasiSebebi.ZamanAsimi;
        }
        return DepoHatasiSebebi.Diger;
    }

    // Hatanin kendisi ya da ic hatalarindan biri T turundeyse onu dondurur.
    private static T? IcHata<T>(Exception? hata) where T : Exception
    {
        for (; hata is not null; hata = hata.InnerException)
        {
            if (hata is T aranan)
            {
                return aranan;
            }
        }
        return null;
    }

    private static bool BeklenenMi(Exception hata) => hata switch
    {
        PostgresException p => !(p.SqlState.StartsWith("22") || p.SqlState.StartsWith("23") || p.SqlState.StartsWith("42")),
        NpgsqlException => true,
        SocketException or TimeoutException or IOException => true,
        _ => false
    };
}
