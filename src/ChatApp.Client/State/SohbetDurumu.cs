using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;

namespace ChatApp.Client.State;

// Sunucudan gelen paketlerin ekrandaki karsiligi: sohbetler, mesajlar, tikler, okunmamis sayilari.
// Ag ve pencere bilmez; sadece "su paket geldi, durum nasil degisir" sorusunu cevaplar (kolay test edilir).
public sealed class SohbetDurumu
{
    private readonly Dictionary<int, Sohbet> _ozel = [];
    private long _beklemeSayaci;

    public SohbetDurumu(int benimId, string benimAdim)
    {
        BenimId = benimId;
        BenimAdim = benimAdim;
    }

    public int BenimId { get; }
    public string BenimAdim { get; }
    public Sohbet Genel { get; } = new(null, "Genel");

    // Son mesaji en yeni olan ustte; mesaji olmayanlar ada gore.
    public IReadOnlyList<Sohbet> OzelSohbetler =>
        [.. _ozel.Values.OrderByDescending(s => s.SonMesajZamani ?? DateTimeOffset.MinValue).ThenBy(s => s.Ad)];

    public Sohbet? Bul(int? karsiId) =>
        karsiId is int id ? _ozel.GetValueOrDefault(id) : Genel;

    public Sohbet SohbetGetir(int karsiId, string ad)
    {
        if (!_ozel.TryGetValue(karsiId, out Sohbet? sohbet))
        {
            sohbet = new Sohbet(karsiId, ad);
            _ozel[karsiId] = sohbet;
        }
        return sohbet;
    }

    // Gonderdigim mesaj, sunucu kabul edene kadar "gonderiliyor" olarak gorunur.
    public MesajSatiri YerelMesajEkle(Sohbet sohbet, Guid istemciMesajId, string metin, DateTimeOffset simdi)
    {
        var satir = new MesajSatiri
        {
            IstemciMesajId = istemciMesajId,
            GonderenId = BenimId,
            GonderenAd = BenimAdim,
            AliciId = sohbet.KarsiId,
            Metin = metin,
            Zaman = simdi,
            OncekiMesajId = sohbet.SonMesajId,
            BeklemeSirasi = ++_beklemeSayaci
        };
        sohbet.Ekle(satir);
        return satir;
    }

    // Sohbet acikken okundu sayilir. Okundu bildirimi gerekiyorsa karsidan gelen son mesajin numarasini dondurur.
    public long? OkunduIsaretle(Sohbet sohbet)
    {
        sohbet.Okunmamis = 0;
        if (sohbet.KarsiId is not int karsiId)
        {
            return null;   // Genel odada okundu bilgisi yok
        }
        long? son = sohbet.Mesajlar.LastOrDefault(m => m.GonderenId == karsiId && m.Id is not null)?.Id;
        if (son is long id && id > sohbet.SonOkunduBildirilen)
        {
            sohbet.SonOkunduBildirilen = id;
            return id;
        }
        return null;
    }

    // Henuz sunucuya ulasmamis mesajlarim (hangi sohbette olursa olsun), yazilma sirasiyla.
    // Baglanti gelince bu sirayla yeniden gonderilir. Sunucunun reddettikleri (Hata) dahil degil.
    public IReadOnlyList<MesajSatiri> GonderilmeyiBekleyenler() =>
        [.. TumSohbetler().SelectMany(s => s.Mesajlar).Where(m => m.Gonderiliyor).OrderBy(m => m.BeklemeSirasi)];

    // "Benden sil" basarili ya da henuz gonderilmemis mesaj silindi.
    public void MesajiKaldir(MesajSatiri satir)
    {
        foreach (Sohbet sohbet in TumSohbetler())
        {
            sohbet.Cikar(satir);
        }
    }

    // "Sohbeti sil" basarili: sohbet listeden kalkar. O kisi yeniden yazarsa yeni mesajla geri gelir.
    public void SohbetiKaldir(int karsiId) => _ozel.Remove(karsiId);

    // Yeniden baglaninca: gecmisler bayat olabilir, acildiklarinda yeniden istenir.
    public void GecmisleriBayatla()
    {
        Genel.GecmisYuklendi = false;
        foreach (Sohbet sohbet in _ozel.Values)
        {
            sohbet.GecmisYuklendi = false;
        }
    }

    // ============================ Paketler ============================

    public Degisiklik Uygula(Paket paket) => paket switch
    {
        SohbetListesi liste => SohbetListesiUygula(liste),
        MesajGeldi geldi => MesajGeldiUygula(geldi.Mesaj),
        MesajKabulEdildi kabul => MesajKabulUygula(kabul),
        MesajReddedildi red => MesajRedUygula(red),
        MesajDurumuDegisti durum => MesajDurumuUygula(durum.MesajId, durum.Durum),
        MesajlarOkundu okundu => MesajlarOkunduUygula(okundu),
        GecmisCevabi gecmis => GecmisUygula(gecmis),
        CevrimiciDurumu cevrimici => CevrimiciUygula(cevrimici),
        KullaniciBilgisi bilgi => KullaniciBilgisiUygula(bilgi),
        MesajHerkestenSilindi silindi => HerkestenSilindiUygula(silindi.MesajId),
        _ => Degisiklik.Yok
    };

    private Degisiklik SohbetListesiUygula(SohbetListesi liste)
    {
        foreach (KisiBilgisi kisi in liste.Kisiler)
        {
            Sohbet sohbet = SohbetGetir(kisi.Id, kisi.GorunenAd);
            sohbet.Ad = kisi.GorunenAd;
            sohbet.Cevrimici = kisi.Cevrimici;
            sohbet.Okunmamis = kisi.OkunmamisSayisi;
            sohbet.Engelli = kisi.Engelli;
        }
        return new Degisiklik(null, null, null);
    }

    private Degisiklik MesajGeldiUygula(MesajBilgisi mesaj)
    {
        Sohbet sohbet;
        int? sorgulanacak = null;
        if (mesaj.AliciId is null)
        {
            sohbet = Genel;
        }
        else
        {
            bool yeniKisi = !_ozel.ContainsKey(mesaj.GonderenId);
            sohbet = SohbetGetir(mesaj.GonderenId, mesaj.GonderenAd);
            if (yeniKisi)
            {
                sorgulanacak = mesaj.GonderenId;   // cevrimici mi? sunucuya sorulsun
            }
        }

        int onceki = sohbet.Mesajlar.Count;
        var satir = MesajSatiri.Olustur(mesaj);
        sohbet.Ekle(satir);
        if (sohbet.Mesajlar.Count == onceki)
        {
            return Degisiklik.Yok;   // zaten vardi
        }
        sohbet.Okunmamis++;
        return new Degisiklik(sohbet, satir, sorgulanacak);
    }

    private Degisiklik MesajKabulUygula(MesajKabulEdildi kabul)
    {
        Sohbet? sohbet = Bul(kabul.Mesaj.AliciId);
        if (sohbet is null)
        {
            return Degisiklik.Yok;
        }

        MesajSatiri? bekleyen = sohbet.Mesajlar.FirstOrDefault(m => m.IstemciMesajId == kabul.IstemciMesajId);
        if (bekleyen is null)
        {
            sohbet.Ekle(MesajSatiri.Olustur(kabul.Mesaj));
        }
        else if (sohbet.Mesajlar.Any(m => m.Id == kabul.Mesaj.Id))
        {
            sohbet.Cikar(bekleyen);   // gecmisle once gelmis; cift olmasin
        }
        else
        {
            bekleyen.Id = kabul.Mesaj.Id;
            bekleyen.Zaman = kabul.Mesaj.Zaman;
            bekleyen.Durum = kabul.Mesaj.Durum;
            bekleyen.BaglantiBekliyor = false;
            bekleyen.HerkestenSilindi = kabul.Mesaj.HerkestenSilindi;
            sohbet.Sirala();
        }
        return new Degisiklik(sohbet, null, null);
    }

    private Degisiklik MesajRedUygula(MesajReddedildi red)
    {
        foreach (Sohbet sohbet in TumSohbetler())
        {
            if (sohbet.Mesajlar.FirstOrDefault(m => m.IstemciMesajId == red.IstemciMesajId) is { } satir)
            {
                satir.Hata = Metinler.HataMetinleri.Acikla(red.Kod);
                satir.BaglantiBekliyor = false;
                return new Degisiklik(sohbet, null, null);
            }
        }
        return Degisiklik.Yok;
    }

    private Degisiklik MesajDurumuUygula(long mesajId, MesajDurumu durum)
    {
        foreach (Sohbet sohbet in _ozel.Values)
        {
            if (sohbet.Mesajlar.FirstOrDefault(m => m.Id == mesajId) is { } satir)
            {
                if (durum > satir.Durum)
                {
                    satir.Durum = durum;   // tikler sadece ileri gider
                }
                return new Degisiklik(sohbet, null, null);
            }
        }
        return Degisiklik.Yok;
    }

    private Degisiklik MesajlarOkunduUygula(MesajlarOkundu okundu)
    {
        if (!_ozel.TryGetValue(okundu.OkuyanId, out Sohbet? sohbet))
        {
            return Degisiklik.Yok;
        }
        foreach (MesajSatiri satir in sohbet.Mesajlar.Where(m => m.GonderenId == BenimId && m.Id <= okundu.SonMesajId))
        {
            satir.Durum = MesajDurumu.Okundu;
        }
        return new Degisiklik(sohbet, null, null);
    }

    private Degisiklik GecmisUygula(GecmisCevabi gecmis)
    {
        Sohbet? sohbet = Bul(gecmis.KarsiId);
        if (sohbet is null)
        {
            return Degisiklik.Yok;
        }

        long? oncekiEnEski = sohbet.EnEskiMesajId;
        foreach (MesajBilgisi mesaj in gecmis.Mesajlar)
        {
            sohbet.Ekle(MesajSatiri.Olustur(mesaj));
        }

        // "Daha eski var" bilgisi sadece bu sayfa listenin en basina ulasiyorsa gecerli.
        if (gecmis.Mesajlar.Count == 0)
        {
            sohbet.DahaEskiVar = false;
        }
        else if (oncekiEnEski is null || gecmis.Mesajlar[0].Id <= oncekiEnEski)
        {
            sohbet.DahaEskiVar = gecmis.DahaVar;
        }
        sohbet.GecmisYuklendi = true;
        return new Degisiklik(sohbet, null, null);
    }

    private Degisiklik HerkestenSilindiUygula(long mesajId)
    {
        foreach (Sohbet sohbet in TumSohbetler())
        {
            if (sohbet.Mesajlar.FirstOrDefault(m => m.Id == mesajId) is { } satir)
            {
                satir.HerkestenSilindi = true;
                return new Degisiklik(sohbet, null, null);
            }
        }
        return Degisiklik.Yok;   // bende olmayan mesaj (ornegin engelli kisinin): yok say
    }

    private Degisiklik CevrimiciUygula(CevrimiciDurumu cevrimici)
    {
        if (!_ozel.TryGetValue(cevrimici.KullaniciId, out Sohbet? sohbet))
        {
            return Degisiklik.Yok;
        }
        sohbet.Cevrimici = cevrimici.Cevrimici;
        return new Degisiklik(sohbet, null, null);
    }

    private Degisiklik KullaniciBilgisiUygula(KullaniciBilgisi bilgi)
    {
        if (!bilgi.Var || !_ozel.TryGetValue(bilgi.KullaniciId, out Sohbet? sohbet))
        {
            return Degisiklik.Yok;
        }
        sohbet.Ad = bilgi.GorunenAd ?? sohbet.Ad;
        sohbet.Cevrimici = bilgi.Cevrimici;
        return new Degisiklik(sohbet, null, null);
    }

    private IEnumerable<Sohbet> TumSohbetler() => _ozel.Values.Prepend(Genel);
}

// Bir paketin etkisi. Sohbet: degisen sohbet (null: liste genel olarak degisti).
// YeniMesaj: baskasindan gelen yeni mesaj (bildirim icin). SorgulanacakKisi: ilk kez yazan kisi.
public sealed record Degisiklik(Sohbet? Sohbet, MesajSatiri? YeniMesaj, int? SorgulanacakKisi)
{
    public static Degisiklik Yok { get; } = new(null, null, null);
}
