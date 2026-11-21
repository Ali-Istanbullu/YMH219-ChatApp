using ChatApp.Server.Postgres;
using Npgsql;

namespace ChatApp.SmokeTests;

// Her test icin gercek PostgreSQL'de GECICI bir sema (schema) acar, test bitince siler.
// Boylece testler gercek sohbet verisine hic dokunmaz.
//
// Baglanti bilgisi CHATAPP_TEST_DB ortam degiskeninden okunur (sadece testler icin, ornek):
//   $env:CHATAPP_TEST_DB = "Host=localhost;Port=5433;Database=chatapp;Username=chatapp;Password=..."
// Yoksa test "Inconclusive" (atlandi) olur. CI'da GitHub Actions bunu kendisi verir.
public sealed class TestVeritabani : IAsyncDisposable
{
    public const string OrtamDegiskeni = "CHATAPP_TEST_DB";

    private readonly string _anaBaglanti;
    private readonly string _sema;

    private TestVeritabani(string anaBaglanti, string sema, PostgresVeritabani veritabani)
    {
        _anaBaglanti = anaBaglanti;
        _sema = sema;
        Veritabani = veritabani;
    }

    public PostgresVeritabani Veritabani { get; }

    public static async Task<TestVeritabani> OlusturAsync()
    {
        string anaBaglanti = Environment.GetEnvironmentVariable(OrtamDegiskeni) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(anaBaglanti))
        {
            Assert.Inconclusive($"{OrtamDegiskeni} tanimli degil; PostgreSQL testleri atlandi.");
        }

        string sema = "test_" + Guid.NewGuid().ToString("N");
        await KomutAsync(anaBaglanti, $"CREATE SCHEMA {sema}");

        string baglanti = new NpgsqlConnectionStringBuilder(anaBaglanti) { SearchPath = sema }.ConnectionString;
        var veritabani = new PostgresVeritabani(baglanti);
        await veritabani.HazirlaAsync();
        return new TestVeritabani(anaBaglanti, sema, veritabani);
    }

    public async ValueTask DisposeAsync()
    {
        await Veritabani.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await KomutAsync(_anaBaglanti, $"DROP SCHEMA {_sema} CASCADE");
    }

    private static async Task KomutAsync(string baglantiDizesi, string sql)
    {
        await using var baglanti = new NpgsqlConnection(baglantiDizesi);
        await baglanti.OpenAsync();
        await using var komut = new NpgsqlCommand(sql, baglanti);
        await komut.ExecuteNonQueryAsync();
    }
}
