using ChatApp.Client.Abstractions;
using ChatApp.Client.Metinler;
using ChatApp.Client.Session;
using ChatApp.Client.State;
using ChatApp.Protocol.Kurallar;
using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;

namespace ChatApp.Client.Presentation;

// ================================================================================
//  SOHBET EKRANININ MANTIGI (presenter)
//
//  Pencere sadece bunu cagirir ve olaylarini dinler; karar burada verilir:
//  hangi sohbet acik, ne zaman "okundu" bildirilir, ne zaman bildirim (yanip sonme + ses) gerekir,
//  yeniden baglaninca neler yeniden istenir.
//  Hatalar (gonderilemedi, kullanici yok...) Turkce metin olarak dondurulur; pencere gosterir.
// ================================================================================
public sealed class SohbetYoneticisi
{
    private readonly SohbetIstemcisi _istemci;
    private readonly IGunlukcu _gunlukcu;
    private readonly TimeProvider _saat;

    public SohbetYoneticisi(SohbetIstemcisi istemci, IGunlukcu gunlukcu, TimeProvider saat)
    {
        ArgumentNullException.ThrowIfNull(istemci);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        ArgumentNullException.ThrowIfNull(saat);
        _istemci = istemci;
        _gunlukcu = gunlukcu;
        _saat = saat;
        _istemci.PaketGeldi += PaketGeldi;
        _istemci.DurumDegisti += BaglantiDurumuDegisti;
    }

    // Giris yapildiginda dolar, cikista null olur.
    public SohbetDurumu? Durum { get; private set; }
    public Sohbet? AcikSohbet { get; private set; }

    // Pencere one gelince/arkaya gecince pencere bildirir.
    public bool PencereAktif { get; private set; } = true;

    // Ekran yeniden cizilsin.
    public event Action? Degisti;

    // Pencere arka plandayken ya da baska sohbet acikken yeni mesaj geldi (yanip sonme + ses).
    public event Action<MesajSatiri>? BildirimGerekli;

    // Kullaniciya gosterilecek uyari (sunucu gecici hata vb.).
    public event Action<string>? Uyari;

    // Baglanti durumu (alt satirda gosterilir). Kapali + aciklama: oturum bitti (giris ekranina donulur).
    public event Action<BaglantiDurumu, string?>? BaglantiDegisti;

    // GirisYapAsync basarili olduktan hemen sonra cagrilir.
    public async Task OturumuBaslatAsync()
    {
        Durum = new SohbetDurumu(_istemci.KullaniciId!.Value, _istemci.GorunenAd!);
        AcikSohbet = Durum.Genel;
        Degisti?.Invoke();
        await GecmisiYukleAsync(Durum.Genel);
    }

    public async Task CikisYapAsync()
    {
        await _istemci.CikisYapAsync();
    }

    // ============================ Kullanici islemleri ============================

    public async Task SohbetAcAsync(Sohbet sohbet)
    {
        ArgumentNullException.ThrowIfNull(sohbet);
        AcikSohbet = sohbet;
        Degisti?.Invoke();
        if (!sohbet.GecmisYuklendi)
        {
            await GecmisiYukleAsync(sohbet);
        }
        await OkunduBildirAsync();
    }

    public async Task PencereAktifligiDegistiAsync(bool aktif)
    {
        PencereAktif = aktif;
        if (aktif)
        {
            await OkunduBildirAsync();
        }
    }

    // Hata varsa Turkce metin, yoksa null.
    public async Task<string?> MesajGonderAsync(string metin)
    {
        if (Durum is null || AcikSohbet is not { } sohbet)
        {
            return null;
        }
        string temiz = metin.TrimEnd();
        if (!GirdiKurallari.MesajGecerliMi(temiz) || temiz.Trim().Length == 0)
        {
            return HataMetinleri.Acikla(HataKodu.MesajGecersiz);
        }
        if (sohbet.Engelli)
        {
            return HataMetinleri.Acikla(HataKodu.EngelledigimKisi);
        }

        var istemciMesajId = Guid.NewGuid();
        MesajSatiri satir = Durum.YerelMesajEkle(sohbet, istemciMesajId, temiz, _saat.GetUtcNow());
        Degisti?.Invoke();
        await GonderAsync(satir);
        return null;
    }

    // Sunucunun reddettigi mesaji (hiz siniri vb.) yeniden dener. Ayni istemci numarasiyla gider.
    public async Task TekrarGonderAsync(MesajSatiri satir)
    {
        ArgumentNullException.ThrowIfNull(satir);
        if (satir.Id is not null || satir.Hata is null)
        {
            return;
        }
        satir.Hata = null;
        Degisti?.Invoke();
        await GonderAsync(satir);
    }

    // herkesten = false: "Benden sil". herkesten = true: "Herkesten sil" (sadece kendi mesajim).
    // Hata varsa Turkce metin.
    public async Task<string?> MesajSilAsync(MesajSatiri satir, bool herkesten)
    {
        ArgumentNullException.ThrowIfNull(satir);
        if (Durum is null)
        {
            return null;
        }
        if (satir.Id is not long mesajId)
        {
            // Henuz sunucuya ulasmamis: sadece burada silinir (ve artik gonderilmez).
            Durum.MesajiKaldir(satir);
            Degisti?.Invoke();
            return null;
        }
        if (herkesten && satir.GonderenId != Durum.BenimId)
        {
            return HataMetinleri.Acikla(HataKodu.SadeceKendiMesajin);
        }

        IslemSonucu? sonuc = await _istemci.IsteAsync<IslemSonucu>(new MesajSilIstegi(mesajId, herkesten), s => s.Islem == IslemTuru.MesajSil);
        if (sonuc is null)
        {
            return "Sunucu cevap vermedi; mesaj silinmedi. Tekrar deneyin.";
        }
        if (!sonuc.Basarili)
        {
            return HataMetinleri.Acikla(sonuc.Hata);
        }

        if (herkesten)
        {
            satir.HerkestenSilindi = true;
        }
        else
        {
            Durum.MesajiKaldir(satir);
        }
        Degisti?.Invoke();
        return null;
    }

    // Acik ozel sohbeti sadece benden siler. Hata varsa Turkce metin.
    public async Task<string?> SohbetSilAsync()
    {
        if (Durum is null || AcikSohbet is not { KarsiId: int karsiId })
        {
            return null;
        }
        IslemSonucu? sonuc = await _istemci.IsteAsync<IslemSonucu>(new SohbetSilIstegi(karsiId), s => s.Islem == IslemTuru.SohbetSil);
        if (sonuc is null)
        {
            return "Sunucu cevap vermedi; sohbet silinmedi. Tekrar deneyin.";
        }
        if (!sonuc.Basarili)
        {
            return HataMetinleri.Acikla(sonuc.Hata);
        }

        Durum.SohbetiKaldir(karsiId);
        AcikSohbet = Durum.Genel;
        Degisti?.Invoke();
        return null;
    }

    public async Task<string?> DahaEskiYukleAsync()
    {
        if (AcikSohbet is not { DahaEskiVar: true, EnEskiMesajId: long enEski } sohbet)
        {
            return null;
        }
        GecmisCevabi? cevap = await _istemci.IsteAsync<GecmisCevabi>(
            new GecmisIstegi(sohbet.KarsiId, enEski), c => c.KarsiId == sohbet.KarsiId);
        return cevap is null ? "Eski mesajlar alınamadı; bağlantıyı kontrol edip tekrar deneyin." : null;
    }

    // ID ile yeni ozel sohbet. Hata varsa Turkce metin.
    public async Task<string?> YeniSohbetAsync(string idMetni)
    {
        if (Durum is null)
        {
            return null;
        }
        if (!int.TryParse(idMetni.Trim(), out int id) || id is < 100000 or > 999999)
        {
            return "ID 6 haneli bir sayı olmalı.";
        }
        if (id == Durum.BenimId)
        {
            return HataMetinleri.Acikla(HataKodu.KendineMesaj);
        }
        if (Durum.Bul(id) is { } mevcut)
        {
            await SohbetAcAsync(mevcut);
            return null;
        }

        KullaniciBilgisi? bilgi = await _istemci.IsteAsync<KullaniciBilgisi>(new KullaniciSorgula(id), k => k.KullaniciId == id);
        if (bilgi is null)
        {
            return "Sunucu cevap vermedi; bağlantıyı kontrol edip tekrar deneyin.";
        }
        if (!bilgi.Var)
        {
            return $"{id} numaralı bir kullanıcı yok.";
        }

        Sohbet sohbet = Durum.SohbetGetir(id, bilgi.GorunenAd ?? id.ToString());
        sohbet.Cevrimici = bilgi.Cevrimici;
        await SohbetAcAsync(sohbet);
        return null;
    }

    public async Task<string?> EngelleAsync(bool engelle)
    {
        if (AcikSohbet is not { KarsiId: int karsiId } sohbet)
        {
            return null;
        }
        IslemTuru islem = engelle ? IslemTuru.Engelle : IslemTuru.EngelKaldir;
        IslemSonucu? sonuc = await _istemci.IsteAsync<IslemSonucu>(new EngelleIstegi(karsiId, engelle), s => s.Islem == islem);
        if (sonuc is null)
        {
            return "Sunucu cevap vermedi; tekrar deneyin.";
        }
        if (!sonuc.Basarili)
        {
            return HataMetinleri.Acikla(sonuc.Hata);
        }
        sohbet.Engelli = engelle;
        Degisti?.Invoke();
        return null;
    }

    public async Task<string?> SifreDegistirAsync(string eskiSifre, string yeniSifre)
    {
        if (!GirdiKurallari.SifreGecerliMi(yeniSifre))
        {
            return HataMetinleri.Acikla(HataKodu.SifreGecersiz);
        }
        IslemSonucu? sonuc = await _istemci.IsteAsync<IslemSonucu>(
            new SifreDegistirIstegi(eskiSifre, yeniSifre), s => s.Islem == IslemTuru.SifreDegistir);
        if (sonuc is null)
        {
            return "Sunucu cevap vermedi; şifre değişmedi. Tekrar deneyin.";
        }
        if (!sonuc.Basarili)
        {
            return HataMetinleri.Acikla(sonuc.Hata);
        }
        _istemci.SifreDegisti(yeniSifre);   // yeniden baglanirken yeni sifre kullanilsin
        _gunlukcu.Bilgi("Sifre degistirildi.");
        return null;
    }

    // ============================ Sunucudan gelenler ============================

    private async void PaketGeldi(Paket paket)
    {
        if (Durum is null)
        {
            return;
        }
        if (paket is HataBildirimi hata)
        {
            Uyari?.Invoke(HataMetinleri.Acikla(hata.Kod));
            return;
        }

        Degisiklik degisiklik = Durum.Uygula(paket);
        Degisti?.Invoke();

        if (degisiklik.SorgulanacakKisi is int kisi)
        {
            await _istemci.GonderAsync(new KullaniciSorgula(kisi));   // cevabi SohbetDurumu isler
        }
        if (degisiklik.YeniMesaj is { } yeni)
        {
            bool goruluyor = PencereAktif && ReferenceEquals(degisiklik.Sohbet, AcikSohbet);
            if (goruluyor)
            {
                await OkunduBildirAsync();
            }
            else
            {
                BildirimGerekli?.Invoke(yeni);
            }
        }
    }

    private async void BaglantiDurumuDegisti(BaglantiDurumu durum, string? aciklama)
    {
        if (durum == BaglantiDurumu.Kapali)
        {
            Durum = null;
            AcikSohbet = null;
        }
        BaglantiDegisti?.Invoke(durum, aciklama);

        // Yeniden baglandi: kopukken kacanlar icin acik sohbetin ve Genel'in gecmisi yeniden istenir.
        // (Ozel mesajlar zaten "bekleyen" olarak gelir.)
        if (durum == BaglantiDurumu.Bagli && Durum is not null)
        {
            Durum.GecmisleriBayatla();
            await GecmisiYukleAsync(Durum.Genel);
            if (AcikSohbet is { GenelMi: false } acik)
            {
                await GecmisiYukleAsync(acik);
                await OkunduBildirAsync();
            }

            // Kopukken yazilan / cevabi gelmeyen mesajlar, yazildiklari sirayla. Cevabi kaybolup aslinda
            // sunucuya ulasmis olan mesaji sunucu tanir, ikinci kez kaydetmez.
            if (Durum is not null)
            {
                foreach (MesajSatiri bekleyen in Durum.GonderilmeyiBekleyenler())
                {
                    if (!await GonderAsync(bekleyen))
                    {
                        break;   // baglanti yine koptu; bir sonraki baglantida devam
                    }
                }
            }
        }
    }

    // Baglanti yoksa mesaj "bağlantı bekleniyor" olur ve baglanti gelince gonderilir.
    private async Task<bool> GonderAsync(MesajSatiri satir)
    {
        bool gitti = await _istemci.GonderAsync(new MesajGonder(satir.IstemciMesajId!.Value, satir.AliciId, satir.Metin));
        if (satir.BaglantiBekliyor == gitti)
        {
            satir.BaglantiBekliyor = !gitti;
            Degisti?.Invoke();
        }
        return gitti;
    }

    // ============================ Yardimcilar ============================

    private async Task GecmisiYukleAsync(Sohbet sohbet)
    {
        GecmisCevabi? cevap = await _istemci.IsteAsync<GecmisCevabi>(
            new GecmisIstegi(sohbet.KarsiId, null), c => c.KarsiId == sohbet.KarsiId);
        if (cevap is null)
        {
            _gunlukcu.Uyari("Sohbet gecmisi alinamadi.");
        }
        // Basariliysa paket PaketGeldi ile SohbetDurumu'na zaten islendi.
    }

    private async Task OkunduBildirAsync()
    {
        if (Durum is null || AcikSohbet is not { } sohbet || !PencereAktif)
        {
            return;
        }
        long? son = Durum.OkunduIsaretle(sohbet);
        Degisti?.Invoke();
        if (son is long id && sohbet.KarsiId is int karsiId)
        {
            await _istemci.GonderAsync(new OkunduBildir(karsiId, id));
        }
    }
}
