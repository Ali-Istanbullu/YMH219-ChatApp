using ChatApp.Server.Data;
using Npgsql;
using NpgsqlTypes;
using static ChatApp.Server.Postgres.PostgresOkuma;

namespace ChatApp.Server.Postgres;

public sealed class PostgresKullaniciDeposu : IKullaniciDeposu
{
    private readonly NpgsqlDataSource _kaynak;

    public PostgresKullaniciDeposu(PostgresVeritabani veritabani)
    {
        ArgumentNullException.ThrowIfNull(veritabani);
        _kaynak = veritabani.Kaynak;
    }

    public Task<KayitDurumu> OlusturAsync(YeniKullanici k, string davetKodu) => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlConnection baglanti = await _kaynak.OpenConnectionAsync();
        await using NpgsqlTransaction islem = await baglanti.BeginTransactionAsync();

        try
        {
            await using var ekle = new NpgsqlCommand(
                $"INSERT INTO kullanicilar ({KullaniciSutunlari}) VALUES ($1, $2, $3, $4, $5, $6, NULL, false, NULL, NULL)",
                baglanti, islem);
            ekle.Parameters.Add(Parametre(k.Id));
            ekle.Parameters.Add(Parametre(k.KullaniciAdi));
            ekle.Parameters.Add(Parametre(k.GorunenAd));
            ekle.Parameters.Add(Parametre(k.SifreHash));
            ekle.Parameters.Add(Parametre(k.Tuz));
            ekle.Parameters.Add(Parametre(k.KayitZamani));
            await ekle.ExecuteNonQueryAsync();
        }
        catch (PostgresException hata) when (hata.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await islem.RollbackAsync();
            return hata.ConstraintName == "kullanicilar_pkey" ? KayitDurumu.IdCakisti : KayitDurumu.KullaniciAdiAlinmis;
        }

        // Kod sadece kullanilmamissa isaretlenir. Ayni kodla ayni anda iki kayit gelirse
        // satir kilidi sayesinde ikincisi 0 satir gunceller ve geri alinir.
        await using var davet = new NpgsqlCommand(
            "UPDATE davet_kodlari SET kullanan_id = $1, kullanilma_zamani = $2 WHERE kod = $3 AND kullanan_id IS NULL",
            baglanti, islem);
        davet.Parameters.Add(Parametre(k.Id));
        davet.Parameters.Add(Parametre(k.KayitZamani));
        davet.Parameters.Add(Parametre(davetKodu));
        if (await davet.ExecuteNonQueryAsync() == 0)
        {
            await islem.RollbackAsync();
            return KayitDurumu.DavetKoduGecersiz;
        }

        await islem.CommitAsync();
        return KayitDurumu.Basarili;
    });

    public Task<KullaniciKaydi?> KullaniciAdiylaBulAsync(string kullaniciAdi) =>
        TekKullaniciAsync($"SELECT {KullaniciSutunlari} FROM kullanicilar WHERE kullanici_adi = $1", kullaniciAdi);

    public Task<KullaniciKaydi?> IdIleBulAsync(int id) =>
        TekKullaniciAsync($"SELECT {KullaniciSutunlari} FROM kullanicilar WHERE id = $1", id);

    public Task SonGirisiYazAsync(int id, DateTimeOffset zaman) =>
        CalistirAsync("UPDATE kullanicilar SET son_giris = $2 WHERE id = $1", Parametre(id), Parametre(zaman));

    public Task SifreyiGuncelleAsync(int id, byte[] sifreHash, byte[] tuz) =>
        CalistirAsync("UPDATE kullanicilar SET sifre_hash = $2, tuz = $3 WHERE id = $1",
            Parametre(id), Parametre(sifreHash), Parametre(tuz));

    public Task YasakDurumunuYazAsync(int id, bool yasakli, string? sebep, DateTimeOffset? zaman) =>
        CalistirAsync("UPDATE kullanicilar SET yasakli = $2, yasak_sebebi = $3, yasak_zamani = $4 WHERE id = $1",
            Parametre(id), Parametre(yasakli),
            Parametre(sebep, NpgsqlDbType.Varchar), Parametre(zaman, NpgsqlDbType.TimestampTz));

    public Task<IReadOnlyList<KullaniciKaydi>> YasaklilariGetirAsync() => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand(
            $"SELECT {KullaniciSutunlari} FROM kullanicilar WHERE yasakli ORDER BY yasak_zamani DESC");
        await using NpgsqlDataReader satirlar = await komut.ExecuteReaderAsync();
        var liste = new List<KullaniciKaydi>();
        while (await satirlar.ReadAsync())
        {
            liste.Add(Kullanici(satirlar));
        }
        return (IReadOnlyList<KullaniciKaydi>)liste;
    });

    public Task<int> HesapSayisiAsync() => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand("SELECT count(*)::int FROM kullanicilar");
        return (int)(await komut.ExecuteScalarAsync())!;
    });

    private Task<KullaniciKaydi?> TekKullaniciAsync(string sql, object deger) => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand(sql);
        komut.Parameters.Add(Parametre(deger));
        await using NpgsqlDataReader satir = await komut.ExecuteReaderAsync();
        return await satir.ReadAsync() ? Kullanici(satir) : null;
    });

    private Task CalistirAsync(string sql, params NpgsqlParameter[] parametreler) => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand(sql);
        komut.Parameters.AddRange(parametreler);
        await komut.ExecuteNonQueryAsync();
    });
}
