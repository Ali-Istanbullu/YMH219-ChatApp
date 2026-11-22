using System.Diagnostics;
using ChatApp.Client.Abstractions;
using ChatApp.Client.Metinler;
using ChatApp.Client.Network;
using ChatApp.Protocol.Iletisim;
using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;

namespace ChatApp.Client.Session;

// ================================================================================
//  SUNUCUYLA OTURUM
//
//  Giris/kayit, paket okuma dongusu ve OTOMATIK YENIDEN BAGLANMA (Ali karari):
//  baglanti koparsa 2, 4, 8, 16, 30, 30... sn arayla ayni kullanici adi/sifreyle tekrar girer.
//  Sifre sadece bellekte tutulur (diske yazilmaz), oturum bitince silinir.
//
//  Is parcacigi: arayuz is parcacigindan cagrilir. "await"ler ayni is parcacigina doner,
//  bu yuzden olaylar (PaketGeldi, DurumDegisti) de arayuz is parcaciginda tetiklenir.
// ================================================================================
public sealed class SohbetIstemcisi : IAsyncDisposable
{
    private static readonly TimeSpan[] Beklemeler =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(30)];
    private static readonly TimeSpan CevapZamanAsimi = TimeSpan.FromSeconds(15);

    private readonly IBaglantiKurucu _kurucu;
    private readonly IGunlukcu _gunlukcu;
    private readonly Func<TimeSpan, CancellationToken, Task> _bekle;
    private readonly List<Bekleyen> _bekleyenler = [];
    private readonly Lock _kilit = new();

    private IBaglanti? _baglanti;
    private CancellationTokenSource? _oturumIptali;
    private Task? _dongu;
    private SunucuAdresi? _adres;
    private string? _kullaniciAdi;
    private string? _sifre;

    public SohbetIstemcisi(IBaglantiKurucu kurucu, IGunlukcu gunlukcu)
        : this(kurucu, gunlukcu, (sure, iptal) => Task.Delay(sure, iptal))
    {
    }

    // Testler beklemeyi aninda bitirebilsin diye.
    internal SohbetIstemcisi(IBaglantiKurucu kurucu, IGunlukcu gunlukcu, Func<TimeSpan, CancellationToken, Task> bekle)
    {
        ArgumentNullException.ThrowIfNull(kurucu);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        ArgumentNullException.ThrowIfNull(bekle);
        _kurucu = kurucu;
        _gunlukcu = gunlukcu;
        _bekle = bekle;
    }

    public BaglantiDurumu Durum { get; private set; } = BaglantiDurumu.Kapali;
    public int? KullaniciId { get; private set; }
    public string? GorunenAd { get; private set; }
    public SunucuAdresi? Adres => _adres;

    // Giristen sonra sunucudan gelen her paket.
    public event Action<Paket>? PaketGeldi;

    // Durum degisti. Ikinci deger: kullaniciya gosterilecek aciklama
    // (Kapali'da oturumun neden bittigi; cikis yapildiysa null).
    public event Action<BaglantiDurumu, string?>? DurumDegisti;

    // ============================ Giris / kayit ============================

    // Basarisizsa GirisSonucu.Basarili = false ve baglanti kapanir.
    // Baglanamazsa BaglantiHatasi, sertifika degistiyse SertifikaDegistiHatasi.
    public async Task<GirisSonucu> GirisYapAsync(SunucuAdresi adres, string kullaniciAdi, string sifre, CancellationToken iptal = default)
    {
        if (Durum != BaglantiDurumu.Kapali)
        {
            throw new InvalidOperationException("Zaten giris yapilmis.");
        }

        IBaglanti baglanti = await _kurucu.BaglanAsync(adres, iptal);
        GirisSonucu sonuc;
        try
        {
            sonuc = await GirisDeneAsync(baglanti, kullaniciAdi, sifre, iptal);
        }
        catch
        {
            await baglanti.DisposeAsync();
            throw;
        }
        if (!sonuc.Basarili)
        {
            await baglanti.DisposeAsync();
            return sonuc;
        }

        _adres = adres;
        _kullaniciAdi = kullaniciAdi;
        _sifre = sifre;
        KullaniciId = sonuc.KullaniciId;
        GorunenAd = sonuc.GorunenAd;
        _baglanti = baglanti;
        _oturumIptali = new CancellationTokenSource();
        DurumuDegistir(BaglantiDurumu.Bagli, null);
        _gunlukcu.Bilgi($"Giris yapildi: {adres}, kullanici {KullaniciId}.");

        _dongu = OturumDongusuAsync(baglanti, _oturumIptali.Token);
        return sonuc;
    }

    // Kayit icin ayri baglanti acilir, sonuc alinip kapanir (sonra GirisYapAsync cagrilir).
    public async Task<KayitSonucu> KayitOlAsync(SunucuAdresi adres, KayitIstegi istek, CancellationToken iptal = default)
    {
        await using IBaglanti baglanti = await _kurucu.BaglanAsync(adres, iptal);
        await GonderVeyaBaglantiHatasiAsync(baglanti, istek, iptal);
        Paket cevap = await CevapBekleAsync(baglanti, p => p is KayitSonucu or HataBildirimi, iptal);
        return cevap switch
        {
            KayitSonucu sonuc => sonuc,
            HataBildirimi hata => new KayitSonucu(false, null, hata.Kod),
            _ => throw new UnreachableException()
        };
    }

    public async Task CikisYapAsync()
    {
        await OturumuBitirAsync(null);
    }

    // Sifre degisince yeniden baglanmada yeni sifre kullanilsin.
    public void SifreDegisti(string yeniSifre)
    {
        if (_sifre is not null)
        {
            _sifre = yeniSifre;
        }
    }

    // ============================ Gonderme ============================

    // Bagli degilse ya da baglanti o an koptuysa false (arayuz "gonderilemedi" gosterir).
    public async Task<bool> GonderAsync(Paket paket)
    {
        IBaglanti? baglanti = _baglanti;
        if (baglanti is null || Durum != BaglantiDurumu.Bagli)
        {
            return false;
        }
        try
        {
            await baglanti.GonderAsync(paket, _oturumIptali?.Token ?? CancellationToken.None);
            return true;
        }
        catch (Exception hata) when (hata is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // Okuma dongusu kopmayi zaten fark edip yeniden baglanir.
            _gunlukcu.Uyari($"Paket gonderilemedi ({paket.GetType().Name}): {hata.Message}");
            return false;
        }
    }

    // Istek gonderir, eslesen cevabi bekler. Gonderilemezse, sunucu hata bildirirse
    // ya da 15 sn icinde cevap gelmezse null.
    public async Task<T?> IsteAsync<T>(Paket istek, Func<T, bool> eslesme) where T : Paket
    {
        var bekleyen = new Bekleyen(p => p is T t && eslesme(t));
        lock (_kilit)
        {
            _bekleyenler.Add(bekleyen);
        }

        try
        {
            if (!await GonderAsync(istek))
            {
                return null;
            }
            return await bekleyen.Tamamlama.Task.WaitAsync(CevapZamanAsimi) as T;
        }
        catch (TimeoutException)
        {
            _gunlukcu.Uyari($"Sunucu {istek.GetType().Name} istegine zamaninda cevap vermedi.");
            return null;
        }
        finally
        {
            lock (_kilit)
            {
                _bekleyenler.Remove(bekleyen);
            }
        }
    }

    public async ValueTask DisposeAsync() => await OturumuBitirAsync(null);

    // ============================ Okuma ve yeniden baglanma ============================

    private async Task OturumDongusuAsync(IBaglanti ilkBaglanti, CancellationToken iptal)
    {
        IBaglanti? baglanti = ilkBaglanti;
        try
        {
            while (baglanti is not null)
            {
                string? bitisSebebi = await OkuAsync(baglanti, iptal);
                if (iptal.IsCancellationRequested)
                {
                    return;
                }
                if (bitisSebebi is not null)
                {
                    await OturumuBitirAsync(bitisSebebi);
                    return;
                }
                baglanti = await YenidenBaglanAsync(iptal);   // null: oturum bitti
            }
        }
        catch (Exception hata) when (hata is not OperationCanceledException)
        {
            // Arka plan dongusundeki bug: kimse beklemiyor; loga yaz, debugger'da dur, oturumu kapat.
            _gunlukcu.Hata(hata, "Istemci okuma dongusunde beklenmeyen hata.");
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
            await OturumuBitirAsync("Beklenmeyen bir hata oluştu, oturum kapatıldı. Ayrıntı log dosyasında.");
        }
    }

    // Baglanti koparsa null, oturum bitmeliyse kullaniciya gosterilecek sebep.
    private async Task<string?> OkuAsync(IBaglanti baglanti, CancellationToken iptal)
    {
        while (true)
        {
            Paket? paket;
            try
            {
                paket = await baglanti.OkuAsync(iptal);
            }
            catch (OperationCanceledException) when (iptal.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception hata) when (hata is IOException or ProtokolHatasi or ObjectDisposedException)
            {
                if (!iptal.IsCancellationRequested)
                {
                    _gunlukcu.Uyari($"Baglanti koptu: {hata.Message}");
                }
                return null;
            }

            switch (paket)
            {
                case null:
                    return null;   // sunucu baglantiyi kapatti
                case OturumSonlandi { Sebep: OturumSonuSebebi.SunucuKapandi }:
                    _gunlukcu.Bilgi("Sunucu kapandi; yeniden baglanilacak.");
                    return null;
                case OturumSonlandi sonlandi:
                    _gunlukcu.Bilgi($"Oturum sunucu tarafindan sonlandirildi: {sonlandi.Sebep}.");
                    return HataMetinleri.Acikla(sonlandi.Sebep, sonlandi.Aciklama);
                default:
                    Dagit(paket);
                    break;
            }
        }
    }

    private void Dagit(Paket paket)
    {
        Bekleyen? bekleyen;
        lock (_kilit)
        {
            // HataBildirimi bekleyen en eski istege cevaptir (o istek basarisiz).
            bekleyen = paket is HataBildirimi
                ? _bekleyenler.FirstOrDefault()
                : _bekleyenler.FirstOrDefault(b => b.Eslesir(paket));
            if (bekleyen is not null)
            {
                _bekleyenler.Remove(bekleyen);
            }
        }
        bekleyen?.Tamamlama.TrySetResult(paket is HataBildirimi ? null : paket);
        PaketGeldi?.Invoke(paket);
    }

    private async Task<IBaglanti?> YenidenBaglanAsync(CancellationToken iptal)
    {
        await BaglantiyiBirakAsync();
        for (int deneme = 0; ; deneme++)
        {
            TimeSpan bekleme = Beklemeler[Math.Min(deneme, Beklemeler.Length - 1)];
            DurumuDegistir(BaglantiDurumu.YenidenBaglaniyor,
                $"Bağlantı koptu. {bekleme.TotalSeconds:0} sn sonra yeniden denenecek...");
            try
            {
                await _bekle(bekleme, iptal);
            }
            catch (OperationCanceledException)
            {
                return null;   // cikis yapildi
            }

            IBaglanti? yeni = null;
            try
            {
                yeni = await _kurucu.BaglanAsync(_adres!, iptal);
                GirisSonucu sonuc = await GirisDeneAsync(yeni, _kullaniciAdi!, _sifre!, iptal);
                if (sonuc.Basarili)
                {
                    _baglanti = yeni;
                    DurumuDegistir(BaglantiDurumu.Bagli, null);
                    _gunlukcu.Bilgi("Yeniden baglanildi.");
                    return yeni;
                }

                await yeni.DisposeAsync();
                if (sonuc.Hata is HataKodu.HizSiniriAsildi or HataKodu.SunucuHatasi)
                {
                    continue;   // gecici; tekrar dene
                }
                await OturumuBitirAsync("Yeniden giriş yapılamadı: " + HataMetinleri.GirisHatasi(sonuc.Hata, sonuc.Aciklama));
                return null;
            }
            catch (BaglantiHatasi hata)
            {
                _gunlukcu.Uyari($"Yeniden baglanma denemesi {deneme + 1} basarisiz: {hata.Message}");
                if (yeni is not null)
                {
                    await yeni.DisposeAsync();
                }
            }
            catch (SertifikaDegistiHatasi)
            {
                await OturumuBitirAsync("Sunucunun kimliği (sertifikası) değişti; güvenliğiniz için bağlantı kesildi. Tekrar giriş yapın.");
                return null;
            }
            catch (OperationCanceledException) when (iptal.IsCancellationRequested)
            {
                if (yeni is not null)
                {
                    await yeni.DisposeAsync();
                }
                return null;
            }
        }
    }

    // Oturumu kapatir. sebep: kullaniciya gosterilecek aciklama (cikis yapildiysa null).
    private async Task OturumuBitirAsync(string? sebep)
    {
        if (Durum == BaglantiDurumu.Kapali && _oturumIptali is null)
        {
            return;
        }

        CancellationTokenSource? iptal = _oturumIptali;
        _oturumIptali = null;
        iptal?.Cancel();
        await BaglantiyiBirakAsync();

        // Kullanici cikis yaptiysa arka plan dongusunun bitmesi beklenir (dongunun kendisi cagirdiysa beklenmez).
        Task? dongu = _dongu;
        _dongu = null;
        if (sebep is null && dongu is not null && !dongu.IsCompleted)
        {
            await dongu;
        }
        iptal?.Dispose();

        _adres = null;
        _kullaniciAdi = null;
        _sifre = null;
        KullaniciId = null;
        GorunenAd = null;
        DurumuDegistir(BaglantiDurumu.Kapali, sebep);
    }

    private async Task BaglantiyiBirakAsync()
    {
        IBaglanti? baglanti = _baglanti;
        _baglanti = null;
        if (baglanti is not null)
        {
            await baglanti.DisposeAsync();
        }

        // Cevap bekleyen istekler bos doner.
        Bekleyen[] bekleyenler;
        lock (_kilit)
        {
            bekleyenler = [.. _bekleyenler];
            _bekleyenler.Clear();
        }
        foreach (Bekleyen bekleyen in bekleyenler)
        {
            bekleyen.Tamamlama.TrySetResult(null);
        }
    }

    private void DurumuDegistir(BaglantiDurumu durum, string? aciklama)
    {
        Durum = durum;
        DurumDegisti?.Invoke(durum, aciklama);
    }

    // ============================ Yardimcilar ============================

    private async Task<GirisSonucu> GirisDeneAsync(IBaglanti baglanti, string kullaniciAdi, string sifre, CancellationToken iptal)
    {
        await GonderVeyaBaglantiHatasiAsync(baglanti, new GirisIstegi(kullaniciAdi, sifre), iptal);
        Paket cevap = await CevapBekleAsync(baglanti, p => p is GirisSonucu or HataBildirimi, iptal);
        return cevap switch
        {
            GirisSonucu sonuc => sonuc,
            HataBildirimi hata => new GirisSonucu(false, null, null, hata.Kod, null),
            _ => throw new UnreachableException()
        };
    }

    private static async Task GonderVeyaBaglantiHatasiAsync(IBaglanti baglanti, Paket paket, CancellationToken iptal)
    {
        try
        {
            await baglanti.GonderAsync(paket, iptal);
        }
        catch (IOException hata)
        {
            throw new BaglantiHatasi("Sunucuyla bağlantı koptu.", hata);
        }
    }

    // Giris/kayit cevabini bekler (araya baska paket girmez; girerse atlanir).
    private static async Task<Paket> CevapBekleAsync(IBaglanti baglanti, Func<Paket, bool> aranan, CancellationToken iptal)
    {
        using var zamanAsimi = CancellationTokenSource.CreateLinkedTokenSource(iptal);
        zamanAsimi.CancelAfter(CevapZamanAsimi);
        try
        {
            while (true)
            {
                Paket? paket = await baglanti.OkuAsync(zamanAsimi.Token)
                    ?? throw new BaglantiHatasi("Sunucu bağlantıyı kapattı.");
                if (aranan(paket))
                {
                    return paket;
                }
            }
        }
        catch (OperationCanceledException hata) when (!iptal.IsCancellationRequested)
        {
            throw new BaglantiHatasi("Sunucu zamanında cevap vermedi.", hata);
        }
        catch (IOException hata)
        {
            throw new BaglantiHatasi("Sunucuyla bağlantı koptu.", hata);
        }
        catch (ProtokolHatasi hata)
        {
            throw new BaglantiHatasi("Sunucudan anlaşılmayan bir cevap geldi. Adres bir ChatServer'a ait olmayabilir.", hata);
        }
    }

    private sealed class Bekleyen(Func<Paket, bool> eslesme)
    {
        public TaskCompletionSource<Paket?> Tamamlama { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Eslesir(Paket paket) => eslesme(paket);
    }
}
