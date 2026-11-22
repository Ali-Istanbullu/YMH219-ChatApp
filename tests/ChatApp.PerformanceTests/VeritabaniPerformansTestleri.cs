using System.Diagnostics;
using ChatApp.Server.Data;
using ChatApp.Server.Postgres;
using ChatApp.SmokeTests;

namespace ChatApp.PerformanceTests;

// Gercek PostgreSQL (CHATAPP_TEST_DB; yoksa atlanir), gecici sema.
// 1) Mesaj basina kac bayt? -> 500 MB siniri kac mesaja denk geliyor (temizlik ayarinin dayanagi).
// 2) Cok mesaj varken gecmis ve sohbet listesi sorgulari hizli mi (indeksler calisiyor mu)?
[TestClass]
[TestCategory("Performans")]
public sealed class VeritabaniPerformansTestleri
{
    private const int MesajSayisi = 10_000;
    private const int MesajBasinaEnFazlaBayt = 1000;
    private const int SorguEsikMs = 100;
    private static readonly DateTimeOffset Zaman = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task OnBinMesaj_MesajBasinaYer_VeSorguHizi()
    {
        await using TestVeritabani db = await TestVeritabani.OlusturAsync();
        var kullanicilar = new PostgresKullaniciDeposu(db.Veritabani);
        var davetler = new PostgresDavetDeposu(db.Veritabani);
        var mesajlar = new PostgresMesajDeposu(db.Veritabani);

        int[] kisiler = [100001, 100002, 100003, 100004, 100005];
        foreach (int id in kisiler)
        {
            string kod = $"P{id % 1000:000}-ABCD";
            await davetler.EkleAsync(kod, Zaman);
            Assert.AreEqual(KayitDurumu.Basarili,
                await kullanicilar.OlusturAsync(new YeniKullanici(id, $"kisi{id}", $"Kişi {id}", [1, 2, 3], [4, 5], Zaman), kod));
        }

        // Ortalama ~100 karakterlik Turkce mesajlar; 4'te 1'i Genel oda, gerisi kisiler arasi ozel.
        Stopwatch sure = Stopwatch.StartNew();
        for (int i = 0; i < MesajSayisi; i++)
        {
            int gonderen = kisiler[i % kisiler.Length];
            int? alici = i % 4 == 0 ? null : kisiler[(i + 1) % kisiler.Length];
            string metin = $"Merhaba, bu {i}. deneme mesajı; çay içelim mi, saat kaçta buluşuyoruz? Görüşürüz. {new string('ş', i % 40)}";
            await mesajlar.KaydetAsync(gonderen, alici, metin, Zaman.AddSeconds(i), Guid.NewGuid());
        }
        long eklemeMs = sure.ElapsedMilliseconds;

        long bayt = await mesajlar.TabloBoyutuAsync();
        double mesajBasina = (double)bayt / MesajSayisi;
        long sinirdaMesaj = (long)(500L * 1024 * 1024 / mesajBasina);

        sure.Restart();
        for (int i = 0; i < 20; i++)
        {
            await mesajlar.OzelGecmisAsync(kisiler[0], kisiler[1], null, 100);
        }
        double gecmisMs = sure.Elapsed.TotalMilliseconds / 20;

        sure.Restart();
        for (int i = 0; i < 20; i++)
        {
            await mesajlar.SohbetleriGetirAsync(kisiler[0]);
        }
        double listeMs = sure.Elapsed.TotalMilliseconds / 20;

        TestContext.WriteLine($"{MesajSayisi:N0} mesaj eklendi: {eklemeMs:N0} ms ({MesajSayisi * 1000.0 / Math.Max(eklemeMs, 1):N0} mesaj/sn).");
        TestContext.WriteLine($"Tablo {bayt / 1024.0 / 1024.0:N1} MB -> mesaj başına {mesajBasina:N0} bayt; " +
            $"500 MB sınırı ≈ {sinirdaMesaj:N0} mesaj.");
        TestContext.WriteLine($"Özel geçmiş (100 mesaj): {gecmisMs:N1} ms; sohbet listesi: {listeMs:N1} ms. Eşik {SorguEsikMs} ms.");

        Assert.IsLessThan(MesajBasinaEnFazlaBayt, mesajBasina);
        Assert.IsLessThan(SorguEsikMs, gecmisMs);
        Assert.IsLessThan(SorguEsikMs, listeMs);
    }
}
