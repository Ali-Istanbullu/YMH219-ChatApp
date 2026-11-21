using ChatApp.Server.Data;
using Npgsql;
using static ChatApp.Server.Postgres.PostgresOkuma;

namespace ChatApp.Server.Postgres;

public sealed class PostgresEngelDeposu : IEngelDeposu
{
    private readonly NpgsqlDataSource _kaynak;

    public PostgresEngelDeposu(PostgresVeritabani veritabani)
    {
        ArgumentNullException.ThrowIfNull(veritabani);
        _kaynak = veritabani.Kaynak;
    }

    public Task EngelleAsync(int engelleyenId, int engellenenId, DateTimeOffset zaman) => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand(
            "INSERT INTO engellemeler (engelleyen_id, engellenen_id, zaman) VALUES ($1, $2, $3) ON CONFLICT DO NOTHING");
        komut.Parameters.Add(Parametre(engelleyenId));
        komut.Parameters.Add(Parametre(engellenenId));
        komut.Parameters.Add(Parametre(zaman));
        await komut.ExecuteNonQueryAsync();
    });

    public Task EngeliKaldirAsync(int engelleyenId, int engellenenId) => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand(
            "DELETE FROM engellemeler WHERE engelleyen_id = $1 AND engellenen_id = $2");
        komut.Parameters.Add(Parametre(engelleyenId));
        komut.Parameters.Add(Parametre(engellenenId));
        await komut.ExecuteNonQueryAsync();
    });

    public Task<bool> EngelliMiAsync(int engelleyenId, int engellenenId) => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand(
            "SELECT EXISTS (SELECT 1 FROM engellemeler WHERE engelleyen_id = $1 AND engellenen_id = $2)");
        komut.Parameters.Add(Parametre(engelleyenId));
        komut.Parameters.Add(Parametre(engellenenId));
        return (bool)(await komut.ExecuteScalarAsync())!;
    });

    public Task<IReadOnlySet<int>> EngellediklerimAsync(int engelleyenId) =>
        KumeAsync("SELECT engellenen_id FROM engellemeler WHERE engelleyen_id = $1", engelleyenId);

    public Task<IReadOnlySet<int>> BeniEngelleyenlerAsync(int engellenenId) =>
        KumeAsync("SELECT engelleyen_id FROM engellemeler WHERE engellenen_id = $1", engellenenId);

    private Task<IReadOnlySet<int>> KumeAsync(string sql, int id) => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand(sql);
        komut.Parameters.Add(Parametre(id));
        await using NpgsqlDataReader satirlar = await komut.ExecuteReaderAsync();
        var kume = new HashSet<int>();
        while (await satirlar.ReadAsync())
        {
            kume.Add(satirlar.GetInt32(0));
        }
        return (IReadOnlySet<int>)kume;
    });
}
