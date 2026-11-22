using ChatApp.Protocol.Paketler;
using ChatApp.Server.Data;
using ChatApp.Server.Network;
using ChatApp.Server.Security;
using ChatApp.Server.Services;
using ChatApp.Server.Sessions;

namespace ChatApp.UnitTests.Fakes;

// Sunucunun butun parcalarini sahte depolarla kurar (Program.cs'teki DI'nin test karsiligi).
// Testler buradan "Ali ve Ayse kayitli ve giris yapmis" gibi hazir durumlar olusturur.
public sealed class TestOrtami
{
    public const string Sifre = "gizli-sifre-123";

    public TestOrtami()
    {
        Davetler = new BellekDavetDeposu();
        Kullanicilar = new BellekKullaniciDeposu(Davetler);
        Mesajlar = new BellekMesajDeposu(Kullanicilar);
        Hashleyici = new SifreHashleyici(tekrar: 1000);   // testler hizli olsun
        HizSinirlayici = new HizSinirlayici(Saat);
        Hesaplar = new HesapServisi(Kullanicilar, Hashleyici, new GirisKilidi(Saat), Saat, Gunlukcu);
        Kisiler = new KisiServisi(Oturumlar, Kullanicilar, Mesajlar, Engeller, Saat, Gunlukcu);
        MesajServisi = new MesajServisi(Oturumlar, Kullanicilar, Mesajlar, Engeller, HizSinirlayici, Saat);
        Yonetim = new YonetimServisi(Oturumlar, Kullanicilar, Davetler, Mesajlar, Saat, Gunlukcu);
        Isleyici = new PaketIsleyici(Oturumlar, Hesaplar, MesajServisi, Kisiler, HizSinirlayici, Gunlukcu);
    }

    public SahteSaat Saat { get; } = new();
    public SahteGunlukcu Gunlukcu { get; } = new();
    public OturumYoneticisi Oturumlar { get; } = new();
    public BellekDavetDeposu Davetler { get; }
    public BellekKullaniciDeposu Kullanicilar { get; }
    public BellekMesajDeposu Mesajlar { get; }
    public BellekEngelDeposu Engeller { get; } = new();
    public SifreHashleyici Hashleyici { get; }
    public HizSinirlayici HizSinirlayici { get; }
    public HesapServisi Hesaplar { get; }
    public KisiServisi Kisiler { get; }
    public MesajServisi MesajServisi { get; }
    public YonetimServisi Yonetim { get; }
    public PaketIsleyici Isleyici { get; }

    // Davet kodu uretip hesap acar; numarayi dondurur.
    public async Task<int> HesapAcAsync(string kullaniciAdi, string gorunenAd)
    {
        string kod = await Yonetim.DavetKoduUretAsync();
        KayitSonucu sonuc = await Hesaplar.KayitOlAsync(new KayitIstegi(kullaniciAdi, gorunenAd, Sifre, kod), "10.0.0.1");
        Assert.IsTrue(sonuc.Basarili, $"Hesap acilamadi: {sonuc.Hata}");
        return sonuc.KullaniciId!.Value;
    }

    // Yeni bir sahte baglanti acar ve PaketIsleyici uzerinden giris yaptirir.
    public async Task<SahteOturum> GirisYapAsync(string kullaniciAdi)
    {
        var oturum = new SahteOturum();
        Oturumlar.BaglantiEkle(oturum);
        bool acik = await Isleyici.IsleAsync(oturum, new GirisIstegi(kullaniciAdi, Sifre));
        Assert.IsTrue(acik);
        Assert.IsNotNull(oturum.KullaniciId, $"Giris basarisiz: {kullaniciAdi}");
        return oturum;
    }

    public async Task MesajAtAsync(SahteOturum gonderen, int? aliciId, string metin) =>
        await Isleyici.IsleAsync(gonderen, new MesajGonder(Guid.NewGuid(), aliciId, metin));
}
