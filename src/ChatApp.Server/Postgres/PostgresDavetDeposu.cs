using ChatApp.Server.Data;
using Npgsql;
using static ChatApp.Server.Postgres.PostgresOkuma;

namespace ChatApp.Server.Postgres;

public sealed class PostgresDavetDeposu : IDavetDeposu
{
    private readonly NpgsqlDataSource _kaynak;

    public PostgresDavetDeposu(PostgresVeritabani veritabani)
    {
        ArgumentNullException.ThrowIfNull(veritabani);
        _kaynak = veritabani.Kaynak;
    }

    public Task<bool> EkleAsync(string kod, DateTimeOffset zaman) => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand(
            "INSERT INTO davet_kodlari (kod, olusturma_zamani) VALUES ($1, $2) ON CONFLICT DO NOTHING");
        komut.Parameters.Add(Parametre(kod));
        komut.Parameters.Add(Parametre(zaman));
        return await komut.ExecuteNonQueryAsync() == 1;
    });

    public Task<IReadOnlyList<DavetKoduKaydi>> HepsiniGetirAsync() => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand("""
            SELECT d.kod, d.olusturma_zamani, d.kullanan_id, k.kullanici_adi, d.kullanilma_zamani
            FROM davet_kodlari d LEFT JOIN kullanicilar k ON k.id = d.kullanan_id
            ORDER BY d.olusturma_zamani DESC
            """);
        await using NpgsqlDataReader satirlar = await komut.ExecuteReaderAsync();
        var liste = new List<DavetKoduKaydi>();
        while (await satirlar.ReadAsync())
        {
            liste.Add(new DavetKoduKaydi(
                satirlar.GetString(0),
                satirlar.GetFieldValue<DateTimeOffset>(1),
                satirlar.IsDBNull(2) ? null : satirlar.GetInt32(2),
                satirlar.IsDBNull(3) ? null : satirlar.GetString(3),
                BosOlabilirZaman(satirlar, 4)));
        }
        return (IReadOnlyList<DavetKoduKaydi>)liste;
    });
}
