using ChatApp.Protocol.Modeller;
using ChatApp.Server.Data;
using ChatApp.Server.Postgres;

namespace ChatApp.SmokeTests;

// Depolarin GERCEK PostgreSQL ile dogru calistigini dogrular (SQL, tipler, transaction, kurallar).
[TestClass]
[TestCategory("Smoke")]
public sealed class PostgresDepoTestleri
{
    private static readonly DateTimeOffset Zaman = new(2026, 11, 21, 12, 0, 0, TimeSpan.Zero);

    private TestVeritabani _db = null!;
    private PostgresKullaniciDeposu _kullanicilar = null!;
    private PostgresMesajDeposu _mesajlar = null!;
    private PostgresEngelDeposu _engeller = null!;
    private PostgresDavetDeposu _davetler = null!;

    [TestInitialize]
    public async Task Hazirla()
    {
        _db = await TestVeritabani.OlusturAsync();
        _kullanicilar = new PostgresKullaniciDeposu(_db.Veritabani);
        _mesajlar = new PostgresMesajDeposu(_db.Veritabani);
        _engeller = new PostgresEngelDeposu(_db.Veritabani);
        _davetler = new PostgresDavetDeposu(_db.Veritabani);
    }

    [TestCleanup]
    public async Task Temizle()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }

    private async Task<int> HesapAcAsync(int id, string ad)
    {
        string kod = $"KOD{id % 10}-{id % 10}ABC"[..9];
        await _davetler.EkleAsync(kod, Zaman);
        Assert.AreEqual(KayitDurumu.Basarili,
            await _kullanicilar.OlusturAsync(new YeniKullanici(id, ad, ad.ToUpperInvariant(), [1, 2, 3], [4, 5], Zaman), kod));
        return id;
    }

    [TestMethod]
    public async Task Kayit_KullaniciyiEkler_DavetKodunuHarcar()
    {
        await _davetler.EkleAsync("ABCD-EFGH", Zaman);
        KayitDurumu durum = await _kullanicilar.OlusturAsync(
            new YeniKullanici(482913, "ali", "Ali İstanbullu", [1, 2, 3], [9, 9], Zaman), "ABCD-EFGH");

        Assert.AreEqual(KayitDurumu.Basarili, durum);
        KullaniciKaydi ali = (await _kullanicilar.KullaniciAdiylaBulAsync("ali"))!;
        Assert.AreEqual(482913, ali.Id);
        Assert.AreEqual("Ali İstanbullu", ali.GorunenAd);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, ali.SifreHash);
        Assert.AreEqual(Zaman, ali.KayitZamani);

        DavetKoduKaydi davet = (await _davetler.HepsiniGetirAsync()).Single();
        Assert.AreEqual(482913, davet.KullananId);
        Assert.AreEqual("ali", davet.KullananAdi);
    }

    [TestMethod]
    public async Task Kayit_KullanilmisKod_GeriAlinir_HesapOlusmaz()
    {
        await HesapAcAsync(111111, "ali");
        string kullanilmisKod = (await _davetler.HepsiniGetirAsync()).Single().Kod;

        KayitDurumu durum = await _kullanicilar.OlusturAsync(new YeniKullanici(222222, "veli", "Veli", [1], [1], Zaman), kullanilmisKod);

        Assert.AreEqual(KayitDurumu.DavetKoduGecersiz, durum);
        Assert.IsNull(await _kullanicilar.IdIleBulAsync(222222));   // transaction geri alindi
        Assert.AreEqual(1, await _kullanicilar.HesapSayisiAsync());
    }

    [TestMethod]
    public async Task Kayit_AlinmisAd_VeDoluNumara_AyriAyriTaninir()
    {
        await HesapAcAsync(111111, "ali");
        await _davetler.EkleAsync("WXYZ-2345", Zaman);

        Assert.AreEqual(KayitDurumu.KullaniciAdiAlinmis,
            await _kullanicilar.OlusturAsync(new YeniKullanici(333333, "ali", "Ali", [1], [1], Zaman), "WXYZ-2345"));
        Assert.AreEqual(KayitDurumu.IdCakisti,
            await _kullanicilar.OlusturAsync(new YeniKullanici(111111, "veli", "Veli", [1], [1], Zaman), "WXYZ-2345"));
        Assert.IsNull((await _davetler.HepsiniGetirAsync()).Single(d => d.Kod == "WXYZ-2345").KullananId);
    }

    [TestMethod]
    public async Task Yasak_YazilirVeKaldirilir()
    {
        int id = await HesapAcAsync(111111, "ali");
        await _kullanicilar.YasakDurumunuYazAsync(id, true, "spam", Zaman);

        KullaniciKaydi yasakli = (await _kullanicilar.YasaklilariGetirAsync()).Single();
        Assert.AreEqual("spam", yasakli.YasakSebebi);
        Assert.AreEqual(Zaman, yasakli.YasakZamani);

        await _kullanicilar.YasakDurumunuYazAsync(id, false, null, null);
        Assert.IsEmpty(await _kullanicilar.YasaklilariGetirAsync());
    }

    [TestMethod]
    public async Task Mesaj_KaydetIletOkuGecmisVeSohbetler()
    {
        int ali = await HesapAcAsync(111111, "ali");
        int ayse = await HesapAcAsync(222222, "ayse");

        long m1 = await _mesajlar.KaydetAsync(ali, ayse, "bir", Zaman);
        long m2 = await _mesajlar.KaydetAsync(ali, ayse, "iki ğüşıöç", Zaman.AddSeconds(1));
        long genel = await _mesajlar.KaydetAsync(ayse, null, "genel", Zaman.AddSeconds(2));
        Assert.IsTrue(m1 < m2 && m2 < genel);

        Assert.HasCount(2, await _mesajlar.IletilmemisleriGetirAsync(ayse));
        await _mesajlar.IletildiIsaretleAsync(m1, Zaman.AddSeconds(5));
        Assert.HasCount(1, await _mesajlar.IletilmemisleriGetirAsync(ayse));

        SohbetOzeti ozet = (await _mesajlar.SohbetleriGetirAsync(ayse)).Single();
        Assert.AreEqual(new SohbetOzeti(ali, "ALI", 2), ozet);

        Assert.AreEqual(m2, await _mesajlar.OkunduIsaretleAsync(ayse, ali, m2, Zaman.AddSeconds(10)));
        Assert.IsNull(await _mesajlar.OkunduIsaretleAsync(ayse, ali, m2, Zaman.AddSeconds(11)));   // zaten okundu
        Assert.AreEqual(0, (await _mesajlar.SohbetleriGetirAsync(ayse)).Single().Okunmamis);

        IReadOnlyList<KayitliMesaj> ozel = await _mesajlar.OzelGecmisAsync(ayse, ali, null, 10);
        Assert.AreEqual("iki ğüşıöç", ozel[0].Metin);   // en yeni basta
        Assert.AreEqual(MesajDurumu.Okundu, ozel[0].MesajBilgisineCevir().Durum);
        Assert.HasCount(1, await _mesajlar.OzelGecmisAsync(ayse, ali, m2, 10));

        KayitliMesaj genelMesaj = (await _mesajlar.GenelGecmisAsync(null, 10)).Single();
        Assert.IsNull(genelMesaj.AliciId);
        Assert.AreEqual("AYSE", genelMesaj.GonderenAd);
    }

    [TestMethod]
    public async Task Engel_EkleSorgulaKaldir()
    {
        int ali = await HesapAcAsync(111111, "ali");
        int ayse = await HesapAcAsync(222222, "ayse");

        await _engeller.EngelleAsync(ali, ayse, Zaman);
        await _engeller.EngelleAsync(ali, ayse, Zaman);   // ikinci kez: hata vermez

        Assert.IsTrue(await _engeller.EngelliMiAsync(ali, ayse));
        Assert.IsFalse(await _engeller.EngelliMiAsync(ayse, ali));
        CollectionAssert.AreEqual(new[] { ayse }, (await _engeller.EngellediklerimAsync(ali)).ToArray());
        CollectionAssert.AreEqual(new[] { ali }, (await _engeller.BeniEngelleyenlerAsync(ayse)).ToArray());

        await _engeller.EngeliKaldirAsync(ali, ayse);
        Assert.IsFalse(await _engeller.EngelliMiAsync(ali, ayse));
    }

    [TestMethod]
    public async Task DavetKodu_AyniKodIkinciKezEklenemez()
    {
        Assert.IsTrue(await _davetler.EkleAsync("ABCD-EFGH", Zaman));
        Assert.IsFalse(await _davetler.EkleAsync("ABCD-EFGH", Zaman));
    }

    [TestMethod]
    public async Task Temizlik_EnEskileriSiler_TabloyuSikistirir_BoyutOlculur()
    {
        int ali = await HesapAcAsync(111111, "ali");
        for (int i = 1; i <= 50; i++)
        {
            await _mesajlar.KaydetAsync(ali, null, $"mesaj {i}", Zaman.AddSeconds(i));
        }
        Assert.AreEqual(50, await _mesajlar.MesajSayisiAsync());
        Assert.IsGreaterThan(0L, await _mesajlar.TabloBoyutuAsync());

        Assert.AreEqual(30, await _mesajlar.EnEskileriSilAsync(20));
        await _mesajlar.SikistirAsync();

        IReadOnlyList<KayitliMesaj> kalan = await _mesajlar.GenelGecmisAsync(null, 100);
        Assert.HasCount(20, kalan);
        Assert.AreEqual("mesaj 50", kalan[0].Metin);
        Assert.AreEqual("mesaj 31", kalan[^1].Metin);
        Assert.AreEqual(0, await _mesajlar.EnEskileriSilAsync(1000));   // daha az mesaj varsa silmez
    }

    [TestMethod]
    public async Task UlasilamayanVeritabani_DepoHatasiVerir()
    {
        // Bos bir porta baglanmaya calis: beklenen hata DepoHatasi'na sarilmali.
        await using var kapali = new PostgresVeritabani("Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2");
        await Assert.ThrowsExactlyAsync<DepoHatasi>(() => kapali.HazirlaAsync());
    }
}
