using ChatApp.Server.Gomulu;
using ChatApp.Server.Settings;

namespace ChatApp.SmokeTests;

// ChatServer'in kendi PostgreSQL'ini GERCEKTEN kurar, baslatir, durdurur (gecici klasorde).
// PostgreSQL programlari bulunamazsa (CHATAPP_PG_BIN ya da Program Files\PostgreSQL) testler atlanir.
[TestClass]
public sealed class GomuluPostgresTestleri
{
    private string _klasor = string.Empty;
    private GomuluPostgres _postgres = null!;

    [TestInitialize]
    public void Hazirla()
    {
        string? bin = GomuluPostgres.BinKlasoruBul(AppContext.BaseDirectory);
        if (bin is null)
        {
            Assert.Inconclusive("PostgreSQL programlari bulunamadi; gomulu veritabani testleri atlandi.");
        }
        _klasor = Path.Combine(Path.GetTempPath(), "chatserver-gomulu-" + Guid.NewGuid().ToString("N"));
        _postgres = new GomuluPostgres(bin!, Path.Combine(_klasor, "veritabani"), Path.Combine(_klasor, "logs", "postgres.log"),
            new SahteGunlukcu());
    }

    [TestCleanup]
    public async Task Temizle()
    {
        if (_postgres is not null)
        {
            await _postgres.DurdurAsync();
        }
        if (Directory.Exists(_klasor))
        {
            Directory.Delete(_klasor, recursive: true);
        }
    }

    [TestMethod]
    public async Task Kur_ChatappKullanicisiyleBaglanilir_DurdurBaslatSonrasiVeriKalir()
    {
        Assert.IsFalse(_postgres.KuruluMu);

        SunucuAyarlari ayarlar = await _postgres.KurAsync();

        Assert.IsTrue(ayarlar.GomuluVeritabani);
        Assert.AreEqual("chatapp", ayarlar.VeritabaniKullanicisi);
        Assert.IsGreaterThanOrEqualTo(GomuluPostgres.IlkPort, ayarlar.VeritabaniPortu);
        Assert.HasCount(32, ayarlar.VeritabaniSifresi);
        Assert.IsTrue(_postgres.KuruluMu);
        Assert.IsTrue(await _postgres.CalisiyorMuAsync());
        Assert.AreEqual("1", await ChatappIleSorgulaAsync(ayarlar, "SELECT 1"));
        Assert.AreEqual("UTF8", await ChatappIleSorgulaAsync(ayarlar, "SHOW server_encoding"));

        await _postgres.DurdurAsync();
        Assert.IsFalse(await _postgres.CalisiyorMuAsync());
        await _postgres.BaslatAsync();
        await _postgres.BaslatAsync();   // zaten calisiyorsa sorun cikarmaz
        Assert.IsTrue(await _postgres.CalisiyorMuAsync());
        Assert.AreEqual("1", await ChatappIleSorgulaAsync(ayarlar, "SELECT 1"), "yeniden baslayinca ayni sifre gecerli");

        string conf = await File.ReadAllTextAsync(Path.Combine(_postgres.VeriKlasoru, "postgresql.conf"));
        Assert.Contains("listen_addresses = 'localhost'", conf);
    }

    [TestMethod]
    public async Task IkinciKezKurulamaz_EskisiYedeklenince_YenidenKurulur()
    {
        await _postgres.KurAsync();
        await _postgres.DurdurAsync();

        await Assert.ThrowsExactlyAsync<GomuluVeritabaniHatasi>(() => _postgres.KurAsync());

        string yedek = _postgres.EskisiniYedekle();
        Assert.IsTrue(Directory.Exists(yedek));
        Assert.IsFalse(_postgres.KuruluMu);
        await _postgres.KurAsync();
        Assert.IsTrue(_postgres.KuruluMu);
    }

    [TestMethod]
    public async Task KurulmadanBaslatilirsa_AnlasilirHata()
    {
        await Assert.ThrowsExactlyAsync<GomuluVeritabaniHatasi>(() => _postgres.BaslatAsync());
    }

    // Uretilen chatapp sifresiyle gercekten baglanilabildigini psql ile dener.
    private static async Task<string> ChatappIleSorgulaAsync(SunucuAyarlari ayarlar, string sql)
    {
        string bin = GomuluPostgres.BinKlasoruBul(AppContext.BaseDirectory)!;
        var bilgi = new System.Diagnostics.ProcessStartInfo(Path.Combine(bin, OperatingSystem.IsWindows() ? "psql.exe" : "psql"))
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (string a in new[] { "-h", "localhost", "-p", ayarlar.VeritabaniPortu.ToString(), "-U", "chatapp",
                     "-d", "chatapp", "-t", "-A", "-c", sql })
        {
            bilgi.ArgumentList.Add(a);
        }
        bilgi.Environment["PGPASSWORD"] = ayarlar.VeritabaniSifresi;
        using var surec = System.Diagnostics.Process.Start(bilgi)!;
        string cikti = await surec.StandardOutput.ReadToEndAsync();
        string hata = await surec.StandardError.ReadToEndAsync();
        await surec.WaitForExitAsync();
        Assert.AreEqual(0, surec.ExitCode, hata);
        return cikti.Trim();
    }

    private sealed class SahteGunlukcu : ChatApp.Server.Abstractions.IGunlukcu
    {
        public void Bilgi(string mesaj) { }
        public void Uyari(string mesaj) { }
        public void Hata(Exception hata, string mesaj) { }
    }
}
