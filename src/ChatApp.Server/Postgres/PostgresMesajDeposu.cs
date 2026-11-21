using ChatApp.Server.Data;
using Npgsql;
using NpgsqlTypes;
using static ChatApp.Server.Postgres.PostgresOkuma;

namespace ChatApp.Server.Postgres;

public sealed class PostgresMesajDeposu : IMesajDeposu
{
    private readonly NpgsqlDataSource _kaynak;

    public PostgresMesajDeposu(PostgresVeritabani veritabani)
    {
        ArgumentNullException.ThrowIfNull(veritabani);
        _kaynak = veritabani.Kaynak;
    }

    public Task<long> KaydetAsync(int gonderenId, int? aliciId, string metin, DateTimeOffset zaman) =>
        PostgresHataSarici.CalistirAsync(async () =>
        {
            await using NpgsqlCommand komut = _kaynak.CreateCommand(
                "INSERT INTO mesajlar (gonderen_id, alici_id, metin, gonderim_zamani) VALUES ($1, $2, $3, $4) RETURNING id");
            komut.Parameters.Add(Parametre(gonderenId));
            komut.Parameters.Add(Parametre(aliciId, NpgsqlDbType.Integer));
            komut.Parameters.Add(Parametre(metin));
            komut.Parameters.Add(Parametre(zaman));
            return (long)(await komut.ExecuteScalarAsync())!;
        });

    public Task IletildiIsaretleAsync(long mesajId, DateTimeOffset zaman) => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand(
            "UPDATE mesajlar SET iletildi_zamani = $2 WHERE id = $1 AND iletildi_zamani IS NULL");
        komut.Parameters.Add(Parametre(mesajId));
        komut.Parameters.Add(Parametre(zaman));
        await komut.ExecuteNonQueryAsync();
    });

    public Task<IReadOnlyList<KayitliMesaj>> IletilmemisleriGetirAsync(int aliciId) =>
        MesajlarAsync($"{MesajSorgusu} WHERE m.alici_id = $1 AND m.iletildi_zamani IS NULL ORDER BY m.id",
            Parametre(aliciId));

    public Task<long?> OkunduIsaretleAsync(int okuyanId, int gonderenId, long sonMesajId, DateTimeOffset zaman) =>
        PostgresHataSarici.CalistirAsync(async () =>
        {
            // Okunan mesaj iletilmis de sayilir (ileti zamani bossa doldurulur).
            await using NpgsqlCommand komut = _kaynak.CreateCommand("""
                WITH guncellenen AS (
                    UPDATE mesajlar
                    SET okundu_zamani = $4, iletildi_zamani = COALESCE(iletildi_zamani, $4)
                    WHERE alici_id = $1 AND gonderen_id = $2 AND id <= $3 AND okundu_zamani IS NULL
                    RETURNING id)
                SELECT max(id) FROM guncellenen
                """);
            komut.Parameters.Add(Parametre(okuyanId));
            komut.Parameters.Add(Parametre(gonderenId));
            komut.Parameters.Add(Parametre(sonMesajId));
            komut.Parameters.Add(Parametre(zaman));
            object? sonuc = await komut.ExecuteScalarAsync();
            return sonuc is long son ? son : (long?)null;
        });

    public Task<IReadOnlyList<KayitliMesaj>> OzelGecmisAsync(int kullaniciId, int karsiId, long? oncekiMesajId, int adet) =>
        MesajlarAsync($"""
            {MesajSorgusu}
            WHERE ((m.gonderen_id = $1 AND m.alici_id = $2) OR (m.gonderen_id = $2 AND m.alici_id = $1))
              AND ($3::bigint IS NULL OR m.id < $3)
            ORDER BY m.id DESC LIMIT $4
            """,
            Parametre(kullaniciId), Parametre(karsiId), Parametre(oncekiMesajId, NpgsqlDbType.Bigint), Parametre(adet));

    public Task<IReadOnlyList<KayitliMesaj>> GenelGecmisAsync(long? oncekiMesajId, int adet) =>
        MesajlarAsync($"""
            {MesajSorgusu}
            WHERE m.alici_id IS NULL AND ($1::bigint IS NULL OR m.id < $1)
            ORDER BY m.id DESC LIMIT $2
            """,
            Parametre(oncekiMesajId, NpgsqlDbType.Bigint), Parametre(adet));

    public Task<IReadOnlyList<SohbetOzeti>> SohbetleriGetirAsync(int kullaniciId) => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand("""
            SELECT k.id, k.gorunen_ad, COALESCE(o.sayi, 0)
            FROM (SELECT CASE WHEN gonderen_id = $1 THEN alici_id ELSE gonderen_id END AS karsi, max(id) AS son
                  FROM mesajlar
                  WHERE alici_id IS NOT NULL AND (gonderen_id = $1 OR alici_id = $1)
                  GROUP BY 1) s
            JOIN kullanicilar k ON k.id = s.karsi
            LEFT JOIN (SELECT gonderen_id, count(*)::int AS sayi
                       FROM mesajlar
                       WHERE alici_id = $1 AND okundu_zamani IS NULL
                       GROUP BY gonderen_id) o ON o.gonderen_id = s.karsi
            ORDER BY s.son DESC
            """);
        komut.Parameters.Add(Parametre(kullaniciId));
        await using NpgsqlDataReader satirlar = await komut.ExecuteReaderAsync();
        var liste = new List<SohbetOzeti>();
        while (await satirlar.ReadAsync())
        {
            liste.Add(new SohbetOzeti(satirlar.GetInt32(0), satirlar.GetString(1), satirlar.GetInt32(2)));
        }
        return (IReadOnlyList<SohbetOzeti>)liste;
    });

    private Task<IReadOnlyList<KayitliMesaj>> MesajlarAsync(string sql, params NpgsqlParameter[] parametreler) =>
        PostgresHataSarici.CalistirAsync(async () =>
        {
            await using NpgsqlCommand komut = _kaynak.CreateCommand(sql);
            komut.Parameters.AddRange(parametreler);
            await using NpgsqlDataReader satirlar = await komut.ExecuteReaderAsync();
            var liste = new List<KayitliMesaj>();
            while (await satirlar.ReadAsync())
            {
                liste.Add(Mesaj(satirlar));
            }
            return (IReadOnlyList<KayitliMesaj>)liste;
        });
}
