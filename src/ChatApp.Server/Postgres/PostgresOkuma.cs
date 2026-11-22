using ChatApp.Server.Data;
using Npgsql;
using NpgsqlTypes;

namespace ChatApp.Server.Postgres;

// Depolarin ortak kullandigi kucuk yardimcilar: satiri nesneye cevirme, null degerli parametre.
internal static class PostgresOkuma
{
    public const string KullaniciSutunlari =
        "id, kullanici_adi, gorunen_ad, sifre_hash, tuz, kayit_zamani, son_giris, yasakli, yasak_sebebi, yasak_zamani";

    public const string MesajSorgusu = """
        SELECT m.id, m.gonderen_id, k.gorunen_ad, m.alici_id, m.metin,
               m.gonderim_zamani, m.iletildi_zamani, m.okundu_zamani, m.herkesten_silindi_zamani
        FROM mesajlar m JOIN kullanicilar k ON k.id = m.gonderen_id
        """;

    public static KullaniciKaydi Kullanici(NpgsqlDataReader s) => new(
        s.GetInt32(0),
        s.GetString(1),
        s.GetString(2),
        s.GetFieldValue<byte[]>(3),
        s.GetFieldValue<byte[]>(4),
        s.GetFieldValue<DateTimeOffset>(5),
        BosOlabilirZaman(s, 6),
        s.GetBoolean(7),
        s.IsDBNull(8) ? null : s.GetString(8),
        BosOlabilirZaman(s, 9));

    public static KayitliMesaj Mesaj(NpgsqlDataReader s) => new(
        s.GetInt64(0),
        s.GetInt32(1),
        s.GetString(2),
        s.IsDBNull(3) ? null : s.GetInt32(3),
        s.GetString(4),
        s.GetFieldValue<DateTimeOffset>(5),
        BosOlabilirZaman(s, 6),
        BosOlabilirZaman(s, 7),
        BosOlabilirZaman(s, 8));

    public static DateTimeOffset? BosOlabilirZaman(NpgsqlDataReader s, int sira) =>
        s.IsDBNull(sira) ? null : s.GetFieldValue<DateTimeOffset>(sira);

    // null olabilen parametreler icin tip acikca verilmeli (PostgreSQL null'in tipini bilemez).
    public static NpgsqlParameter Parametre(object? deger, NpgsqlDbType tip) =>
        new() { Value = deger ?? DBNull.Value, NpgsqlDbType = tip };

    public static NpgsqlParameter Parametre(object deger) => new() { Value = deger };
}
