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

    // Baglantiyi dener ve tablolari olusturur. Ulasilamazsa DepoHatasi.
    public Task HazirlaAsync() => PostgresHataSarici.CalistirAsync(async () =>
    {
        await using NpgsqlCommand komut = Kaynak.CreateCommand(TablolarSql);
        await komut.ExecuteNonQueryAsync();
    });

    public ValueTask DisposeAsync() => Kaynak.DisposeAsync();
}
