using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;
using ChatApp.UnitTests.Fakes;

namespace ChatApp.UnitTests.Network;

[TestClass]
public sealed class PaketIsleyiciTestleri
{
    private readonly TestOrtami _o = new();

    [TestMethod]
    public async Task GirisYapmadanMesaj_ReddedilirVeBaglantiKesilir()
    {
        var oturum = new SahteOturum();
        bool acik = await _o.Isleyici.IsleAsync(oturum, new MesajGonder(Guid.NewGuid(), null, "selam"));

        Assert.IsFalse(acik);
        Assert.AreEqual(HataKodu.GirisGerekli, oturum.Paketler<HataBildirimi>().Single().Kod);
        Assert.IsEmpty(_o.Mesajlar.Hepsi);
    }

    [TestMethod]
    public async Task IstemcininGondermemesiGerekenPaket_BaglantiyiKeser()
    {
        await _o.HesapAcAsync("ali", "Ali");
        SahteOturum ali = await _o.GirisYapAsync("ali");

        bool acik = await _o.Isleyici.IsleAsync(ali, new MesajGeldi(
            new MesajBilgisi(1, 1, "sahte", null, "sahte", DateTimeOffset.UnixEpoch, MesajDurumu.Okundu)));
        Assert.IsFalse(acik);
    }

    [TestMethod]
    public async Task Giris_SirasiylaSonucListeVeBekleyenMesajlarGelir()
    {
        int aliId = await _o.HesapAcAsync("ali", "Ali");
        int ayseId = await _o.HesapAcAsync("ayse", "Ayse");
        await _o.Mesajlar.KaydetAsync(ayseId, aliId, "bekleyen", _o.Saat.SimdiUtc);

        SahteOturum ali = await _o.GirisYapAsync("ali");

        var turler = ali.Gelenler.Select(p => p.GetType()).ToList();
        CollectionAssert.AreEqual(new[] { typeof(GirisSonucu), typeof(SohbetListesi), typeof(MesajGeldi) }, turler);
        Assert.IsTrue(_o.Oturumlar.CevrimiciMi(aliId));
    }

    [TestMethod]
    public async Task AyniHesapIkinciYerdenGirince_EskiOturumKapanir()
    {
        int aliId = await _o.HesapAcAsync("ali", "Ali");
        SahteOturum eski = await _o.GirisYapAsync("ali");
        SahteOturum yeni = await _o.GirisYapAsync("ali");

        Assert.IsTrue(eski.Kapatildi);
        Assert.AreEqual(OturumSonuSebebi.BaskaYerdenGiris, eski.KapanisSebebi!.Sebep);
        Assert.AreSame(yeni, _o.Oturumlar.Bul(aliId));

        // Eski baglantinin kapanmasi yeni oturumu cevrimdisi yapmamali.
        await _o.Isleyici.BaglantiKapandiAsync(eski);
        Assert.AreSame(yeni, _o.Oturumlar.Bul(aliId));
    }

    [TestMethod]
    public async Task AyniIptenSaatteBestenFazlaKayit_BaglantiKesilir()
    {
        var oturum = new SahteOturum("5.5.5.5");
        for (int i = 0; i < 5; i++)
        {
            Assert.IsTrue(await _o.Isleyici.IsleAsync(oturum, new KayitIstegi($"kisi{i}", "Kisi", TestOrtami.Sifre, "ABCD-EFGH")));
        }

        bool acik = await _o.Isleyici.IsleAsync(oturum, new KayitIstegi("kisi9", "Kisi", TestOrtami.Sifre, "ABCD-EFGH"));
        Assert.IsFalse(acik);
        Assert.AreEqual(HataKodu.HizSiniriAsildi, oturum.Paketler<HataBildirimi>().Single().Kod);
    }

    [TestMethod]
    public async Task VeritabaniKoparsa_KullaniciyaBildirilir_BaglantiAcikKalir()
    {
        await _o.HesapAcAsync("ali", "Ali");
        SahteOturum ali = await _o.GirisYapAsync("ali");
        _o.Kullanicilar.Kopuk = true;

        bool acik = await _o.Isleyici.IsleAsync(ali, new KullaniciSorgula(123_456));

        Assert.IsTrue(acik);
        Assert.AreEqual(HataKodu.SunucuHatasi, ali.Paketler<HataBildirimi>().Single().Kod);
        Assert.HasCount(1, _o.Gunlukcu.Hatalar);
    }

    [TestMethod]
    public async Task KoddakiBug_Yutulmaz()
    {
        // DepoHatasi disindaki hatalar (bug) PaketIsleyici'de yakalanmaz, yukari cikar:
        // TcpSunucu onu "beklenmeyen hata" olarak loglar ve sadece o baglantiyi kapatir.
        await _o.HesapAcAsync("ali", "Ali");
        SahteOturum ali = await _o.GirisYapAsync("ali");
        _o.Kullanicilar.BugOlsun = true;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _o.Isleyici.IsleAsync(ali, new KullaniciSorgula(123_456)));
        Assert.IsEmpty(_o.Gunlukcu.Hatalar);
    }
}
