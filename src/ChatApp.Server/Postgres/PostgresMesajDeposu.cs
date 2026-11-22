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

    // Gorunurluk kosulu: kullanici ($1) bu mesaji gizlemedi ve sildigi sohbetin eski kismi degil.
    private const string GorunurKosulu = """
        NOT EXISTS (SELECT 1 FROM mesaj_gizlemeleri g WHERE g.kullanici_id = $1 AND g.mesaj_id = m.id)
        AND (m.alici_id IS NULL OR m.id > COALESCE(
            (SELECT s.son_mesaj_id FROM sohbet_silmeleri s
             WHERE s.kullanici_id = $1
               AND s.karsi_id = CASE WHEN m.gonderen_id = $1 THEN m.alici_id ELSE m.gonderen_id END), 0))
        """;

    // Yonetici icin: mesaji kimler gizlemis (kendinden silerek ya da sohbeti silerek).
    private const string GizleyenlerSutunu = """
        ARRAY(SELECT g.kullanici_id FROM mesaj_gizlemeleri g WHERE g.mesaj_id = m.id
              UNION
              SELECT s.kullanici_id FROM sohbet_silmeleri s
              WHERE m.alici_id IS NOT NULL AND s.son_mesaj_id >= m.id
                AND ((s.kullanici_id = m.gonderen_id AND s.karsi_id = m.alici_id)
                  OR (s.kullanici_id = m.alici_id AND s.karsi_id = m.gonderen_id)))
        """;

    public Task<(long Id, bool Yeni)> KaydetAsync(int gonderenId, int? aliciId, string metin, DateTimeOffset zaman, Guid istemciMesajId) =>
        PostgresHataSarici.CalistirAsync(async () =>
        {
            await using NpgsqlCommand komut = _kaynak.CreateCommand("""
                INSERT INTO mesajlar (gonderen_id, alici_id, metin, gonderim_zamani, istemci_mesaj_id)
                VALUES ($1, $2, $3, $4, $5)
                ON CONFLICT (gonderen_id, istemci_mesaj_id) WHERE istemci_mesaj_id IS NOT NULL DO NOTHING
                RETURNING id
                """);
            komut.Parameters.Add(Parametre(gonderenId));
            komut.Parameters.Add(Parametre(aliciId, NpgsqlDbType.Integer));
            komut.Parameters.Add(Parametre(metin));
            komut.Parameters.Add(Parametre(zaman));
            komut.Parameters.Add(Parametre(istemciMesajId));
            if (await komut.ExecuteScalarAsync() is long yeniId)
            {
                return (yeniId, true);
            }

            // Ayni mesaj daha once kaydedilmis: onun numarasi.
            await using NpgsqlCommand eski = _kaynak.CreateCommand(
                "SELECT id FROM mesajlar WHERE gonderen_id = $1 AND istemci_mesaj_id = $2");
            eski.Parameters.Add(Parametre(gonderenId));
            eski.Parameters.Add(Parametre(istemciMesajId));
            return ((long)(await eski.ExecuteScalarAsync())!, false);
        });

    public async Task<KayitliMesaj?> MesajBulAsync(long mesajId) =>
        (await MesajlarAsync($"{MesajSorgusu} WHERE m.id = $1", Parametre(mesajId))).SingleOrDefault();

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
              AND {GorunurKosulu}
            ORDER BY m.id DESC LIMIT $4
            """,
            Parametre(kullaniciId), Parametre(karsiId), Parametre(oncekiMesajId, NpgsqlDbType.Bigint), Parametre(adet));

    public Task<IReadOnlyList<KayitliMesaj>> GenelGecmisAsync(int kullaniciId, long? oncekiMesajId, int adet) =>
        MesajlarAsync($"""
            {MesajSorgusu}
            WHERE m.alici_id IS NULL AND ($2::bigint IS NULL OR m.id < $2)
              AND {GorunurKosulu}
            ORDER BY m.id DESC LIMIT $3
            """,
            Parametre(kullaniciId), Parametre(oncekiMesajId, NpgsqlDbType.Bigint), Parametre(adet));

    public Task<IReadOnlyList<SohbetOzeti>> SohbetleriGetirAsync(int kullaniciId) => PostgresHataSarici.CalistirAsync(async () =>
    {
        // Sadece kullanicinin hala gordugu mesajlar sayilir: sildigi sohbet, yeni mesaj gelene kadar listede yok.
        await using NpgsqlCommand komut = _kaynak.CreateCommand($"""
            SELECT k.id, k.gorunen_ad,
                   (count(*) FILTER (WHERE m.alici_id = $1 AND m.okundu_zamani IS NULL))::int
            FROM mesajlar m
            JOIN kullanicilar k ON k.id = CASE WHEN m.gonderen_id = $1 THEN m.alici_id ELSE m.gonderen_id END
            WHERE m.alici_id IS NOT NULL AND (m.gonderen_id = $1 OR m.alici_id = $1)
              AND {GorunurKosulu}
            GROUP BY k.id, k.gorunen_ad
            ORDER BY max(m.id) DESC
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

    public Task GizleAsync(int kullaniciId, long mesajId, DateTimeOffset zaman) => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand(
            "INSERT INTO mesaj_gizlemeleri (kullanici_id, mesaj_id, zaman) VALUES ($1, $2, $3) ON CONFLICT DO NOTHING");
        komut.Parameters.Add(Parametre(kullaniciId));
        komut.Parameters.Add(Parametre(mesajId));
        komut.Parameters.Add(Parametre(zaman));
        await komut.ExecuteNonQueryAsync();
    });

    public Task HerkestenSilAsync(long mesajId, DateTimeOffset zaman) => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand(
            "UPDATE mesajlar SET herkesten_silindi_zamani = $2 WHERE id = $1 AND herkesten_silindi_zamani IS NULL");
        komut.Parameters.Add(Parametre(mesajId));
        komut.Parameters.Add(Parametre(zaman));
        await komut.ExecuteNonQueryAsync();
    });

    public Task SohbetiSilAsync(int kullaniciId, int karsiId, DateTimeOffset zaman) => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand("""
            INSERT INTO sohbet_silmeleri (kullanici_id, karsi_id, son_mesaj_id, zaman)
            SELECT $1, $2, COALESCE(max(id), 0), $3 FROM mesajlar
            WHERE (gonderen_id = $1 AND alici_id = $2) OR (gonderen_id = $2 AND alici_id = $1)
            ON CONFLICT (kullanici_id, karsi_id)
                DO UPDATE SET son_mesaj_id = EXCLUDED.son_mesaj_id, zaman = EXCLUDED.zaman
            """);
        komut.Parameters.Add(Parametre(kullaniciId));
        komut.Parameters.Add(Parametre(karsiId));
        komut.Parameters.Add(Parametre(zaman));
        await komut.ExecuteNonQueryAsync();
    });

    public Task<IReadOnlyList<YonetimSohbetOzeti>> YonetimSohbetleriAsync(int kullaniciId) => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand("""
            SELECT k.id, k.gorunen_ad, count(*)::int, max(m.gonderim_zamani)
            FROM mesajlar m
            JOIN kullanicilar k ON k.id = CASE WHEN m.gonderen_id = $1 THEN m.alici_id ELSE m.gonderen_id END
            WHERE m.alici_id IS NOT NULL AND (m.gonderen_id = $1 OR m.alici_id = $1)
            GROUP BY k.id, k.gorunen_ad
            ORDER BY max(m.id) DESC
            """);
        komut.Parameters.Add(Parametre(kullaniciId));
        await using NpgsqlDataReader satirlar = await komut.ExecuteReaderAsync();
        var liste = new List<YonetimSohbetOzeti>();
        while (await satirlar.ReadAsync())
        {
            liste.Add(new YonetimSohbetOzeti(satirlar.GetInt32(0), satirlar.GetString(1), satirlar.GetInt32(2),
                satirlar.GetFieldValue<DateTimeOffset>(3)));
        }
        return (IReadOnlyList<YonetimSohbetOzeti>)liste;
    });

    public Task<IReadOnlyList<YonetimMesajKaydi>> YonetimOzelKayitlarAsync(int kullaniciId, int karsiId, long? oncekiMesajId, int adet) =>
        YonetimKayitlariAsync("""
            ((m.gonderen_id = $1 AND m.alici_id = $2) OR (m.gonderen_id = $2 AND m.alici_id = $1))
            AND ($3::bigint IS NULL OR m.id < $3)
            ORDER BY m.id DESC LIMIT $4
            """,
            Parametre(kullaniciId), Parametre(karsiId), Parametre(oncekiMesajId, NpgsqlDbType.Bigint), Parametre(adet));

    public Task<IReadOnlyList<YonetimMesajKaydi>> YonetimGenelKayitlarAsync(long? oncekiMesajId, int adet) =>
        YonetimKayitlariAsync("""
            m.alici_id IS NULL AND ($1::bigint IS NULL OR m.id < $1)
            ORDER BY m.id DESC LIMIT $2
            """,
            Parametre(oncekiMesajId, NpgsqlDbType.Bigint), Parametre(adet));

    private Task<IReadOnlyList<YonetimMesajKaydi>> YonetimKayitlariAsync(string kosulVeSira, params NpgsqlParameter[] parametreler) =>
        PostgresHataSarici.CalistirAsync(async () =>
        {
            await using NpgsqlCommand komut = _kaynak.CreateCommand($"""
                SELECT m.id, m.gonderen_id, k.gorunen_ad, m.alici_id, m.metin,
                       m.gonderim_zamani, m.iletildi_zamani, m.okundu_zamani, m.herkesten_silindi_zamani,
                       {GizleyenlerSutunu}
                FROM mesajlar m JOIN kullanicilar k ON k.id = m.gonderen_id
                WHERE {kosulVeSira}
                """);
            komut.Parameters.AddRange(parametreler);
            await using NpgsqlDataReader satirlar = await komut.ExecuteReaderAsync();
            var liste = new List<YonetimMesajKaydi>();
            while (await satirlar.ReadAsync())
            {
                liste.Add(new YonetimMesajKaydi(Mesaj(satirlar), satirlar.GetFieldValue<int[]>(9)));
            }
            return (IReadOnlyList<YonetimMesajKaydi>)liste;
        });

    public Task<long> TabloBoyutuAsync() => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand("SELECT pg_total_relation_size('mesajlar')");
        return (long)(await komut.ExecuteScalarAsync())!;
    });

    public Task<long> MesajSayisiAsync() => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = _kaynak.CreateCommand("SELECT count(*) FROM mesajlar");
        return (long)(await komut.ExecuteScalarAsync())!;
    });

    public Task<long> EnEskileriSilAsync(long tutulacakAdet) => PostgresHataSarici.CalistirAsync(async () =>
    {
        // Yeniden eskiye sirali listede "tutulacakAdet"inci mesajin numarasi bulunur; ondan eskiler silinir.
        // Daha az mesaj varsa alt sorgu bos (NULL) doner ve hicbir sey silinmez.
        await using NpgsqlCommand komut = _kaynak.CreateCommand("""
            DELETE FROM mesajlar
            WHERE id < (SELECT id FROM mesajlar ORDER BY id DESC OFFSET $1 - 1 LIMIT 1)
            """);
        komut.Parameters.Add(Parametre(Math.Max(tutulacakAdet, 1)));
        return (long)await komut.ExecuteNonQueryAsync();
    });

    public Task SikistirAsync() => PostgresHataSarici.CalistirAsync(async () =>
    {
        // VACUUM FULL tabloyu yeniden yazar: dosya kuculur. Bu sirada tablo birkac saniye kilitlenir.
        await using NpgsqlCommand komut = _kaynak.CreateCommand("VACUUM FULL mesajlar");
        komut.CommandTimeout = 0;   // buyuk tabloda uzun surebilir; zaman asimi yok
        await komut.ExecuteNonQueryAsync();
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
