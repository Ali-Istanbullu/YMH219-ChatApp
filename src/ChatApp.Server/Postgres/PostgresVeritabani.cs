using Npgsql;

namespace ChatApp.Server.Postgres;

// PostgreSQL baglanti havuzu ve tablolarin olusturulmasi.
// Tablolar yoksa ilk acilista olusturulur (CREATE TABLE IF NOT EXISTS): elle bir sey kurmaya gerek yok.
public sealed class PostgresVeritabani : IAsyncDisposable
{
    private const string TablolarSql = """
        CREATE TABLE IF NOT EXISTS kullanicilar (
            id            integer      PRIMARY KEY,
            kullanici_adi varchar(20)  NOT NULL UNIQUE,
            gorunen_ad    varchar(40)  NOT NULL,
            sifre_hash    bytea        NOT NULL,
            tuz           bytea        NOT NULL,
            kayit_zamani  timestamptz  NOT NULL,
            son_giris     timestamptz,
            yasakli       boolean      NOT NULL DEFAULT false,
            yasak_sebebi  varchar(200),
            yasak_zamani  timestamptz
        );

        CREATE TABLE IF NOT EXISTS davet_kodlari (
            kod               varchar(9)  PRIMARY KEY,
            olusturma_zamani  timestamptz NOT NULL,
            kullanan_id       integer     REFERENCES kullanicilar(id),
            kullanilma_zamani timestamptz
        );

        CREATE TABLE IF NOT EXISTS mesajlar (
            id              bigint        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            gonderen_id     integer       NOT NULL REFERENCES kullanicilar(id),
            alici_id        integer       REFERENCES kullanicilar(id),
            metin           varchar(2000) NOT NULL,
            gonderim_zamani timestamptz   NOT NULL,
            iletildi_zamani timestamptz,
            okundu_zamani   timestamptz
        );
        CREATE INDEX IF NOT EXISTS ix_mesajlar_ikili ON mesajlar (gonderen_id, alici_id, id);
        CREATE INDEX IF NOT EXISTS ix_mesajlar_ikili_ters ON mesajlar (alici_id, gonderen_id, id);
        CREATE INDEX IF NOT EXISTS ix_mesajlar_iletilmemis ON mesajlar (alici_id)
            WHERE iletildi_zamani IS NULL AND alici_id IS NOT NULL;
        CREATE INDEX IF NOT EXISTS ix_mesajlar_genel ON mesajlar (id) WHERE alici_id IS NULL;

        -- Silme ozellikleri (22.11). Eski veritabaninda da calissin diye ALTER ... IF NOT EXISTS.
        ALTER TABLE mesajlar ADD COLUMN IF NOT EXISTS istemci_mesaj_id uuid;
        ALTER TABLE mesajlar ADD COLUMN IF NOT EXISTS herkesten_silindi_zamani timestamptz;
        -- Ayni mesaj (baglanti kopunca tekrar gonderilen) ikinci kez kaydedilmez.
        CREATE UNIQUE INDEX IF NOT EXISTS ux_mesajlar_istemci ON mesajlar (gonderen_id, istemci_mesaj_id)
            WHERE istemci_mesaj_id IS NOT NULL;

        -- "Benden sil": hangi kullanici hangi mesaji kendinden gizledi. Mesaj temizlikte silinirse bu da silinir.
        CREATE TABLE IF NOT EXISTS mesaj_gizlemeleri (
            kullanici_id integer     NOT NULL REFERENCES kullanicilar(id),
            mesaj_id     bigint      NOT NULL REFERENCES mesajlar(id) ON DELETE CASCADE,
            zaman        timestamptz NOT NULL,
            PRIMARY KEY (kullanici_id, mesaj_id)
        );
        CREATE INDEX IF NOT EXISTS ix_gizlemeler_mesaj ON mesaj_gizlemeleri (mesaj_id);

        -- "Sohbeti sil": kullanici, karsi kisiyle olan sohbetin son_mesaj_id'ye kadarki kismini gizledi.
        CREATE TABLE IF NOT EXISTS sohbet_silmeleri (
            kullanici_id integer     NOT NULL REFERENCES kullanicilar(id),
            karsi_id     integer     NOT NULL REFERENCES kullanicilar(id),
            son_mesaj_id bigint      NOT NULL,
            zaman        timestamptz NOT NULL,
            PRIMARY KEY (kullanici_id, karsi_id)
        );

        CREATE TABLE IF NOT EXISTS engellemeler (
            engelleyen_id integer     NOT NULL REFERENCES kullanicilar(id),
            engellenen_id integer     NOT NULL REFERENCES kullanicilar(id),
            zaman         timestamptz NOT NULL,
            PRIMARY KEY (engelleyen_id, engellenen_id)
        );
        """;

    public PostgresVeritabani(string baglantiDizesi)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baglantiDizesi);
        Kaynak = NpgsqlDataSource.Create(baglantiDizesi);
    }

    internal NpgsqlDataSource Kaynak { get; }

    // Pencereden girilen bilgilerden Npgsql baglanti dizesi kurar.
    // Elle "Host=...;Password=..." birlestirilmez: sifrede ; veya = olursa bozulurdu.
    public static string BaglantiDizesi(string sunucu, int port, string veritabani, string kullanici, string sifre) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = sunucu,
            Port = port,
            Database = veritabani,
            Username = kullanici,
            Password = sifre,
            Timeout = 5   // saniye: PostgreSQL kapaliysa pencere uzun beklemesin
        }.ConnectionString;

    // Baglantiyi dener ve tablolari olusturur. Ulasilamazsa DepoHatasi.
    public Task HazirlaAsync() => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = Kaynak.CreateCommand(TablolarSql);
        await komut.ExecuteNonQueryAsync();
    });

    public ValueTask DisposeAsync() => Kaynak.DisposeAsync();
}
