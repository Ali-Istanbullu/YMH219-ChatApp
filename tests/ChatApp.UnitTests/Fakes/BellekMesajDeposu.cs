using ChatApp.Server.Data;

namespace ChatApp.UnitTests.Fakes;

// IMesajDeposu'nun bellekte calisan sahtesi (PostgreSQL deposuyla ayni kurallar).
public sealed class BellekMesajDeposu : IMesajDeposu
{
    private readonly List<KayitliMesaj> _mesajlar = new();
    private readonly Dictionary<(int Gonderen, Guid IstemciId), long> _istemciIdleri = new();
    private readonly HashSet<(int Kullanici, long Mesaj)> _gizlemeler = new();
    private readonly Dictionary<(int Kullanici, int Karsi), long> _sohbetSilmeleri = new();
    private readonly BellekKullaniciDeposu _kullanicilar;
    private readonly Lock _kilit = new();
    private long _sonId;

    public BellekMesajDeposu(BellekKullaniciDeposu kullanicilar) => _kullanicilar = kullanicilar;

    public IReadOnlyList<KayitliMesaj> Hepsi
    {
        get
        {
            lock (_kilit)
            {
                return _mesajlar.ToList();
            }
        }
    }

    // Testlerin eski cagrilari icin: her seferinde yeni istemci numarasi.
    public async Task<long> KaydetAsync(int gonderenId, int? aliciId, string metin, DateTimeOffset zaman) =>
        (await KaydetAsync(gonderenId, aliciId, metin, zaman, Guid.NewGuid())).Id;

    public async Task<(long Id, bool Yeni)> KaydetAsync(int gonderenId, int? aliciId, string metin, DateTimeOffset zaman, Guid istemciMesajId)
    {
        string ad = (await _kullanicilar.IdIleBulAsync(gonderenId))?.GorunenAd ?? "?";
        lock (_kilit)
        {
            if (_istemciIdleri.TryGetValue((gonderenId, istemciMesajId), out long eski))
            {
                return (eski, false);
            }
            long id = ++_sonId;
            _mesajlar.Add(new KayitliMesaj(id, gonderenId, ad, aliciId, metin, zaman, null, null));
            _istemciIdleri[(gonderenId, istemciMesajId)] = id;
            return (id, true);
        }
    }

    public Task<KayitliMesaj?> MesajBulAsync(long mesajId)
    {
        lock (_kilit)
        {
            return Task.FromResult(_mesajlar.FirstOrDefault(m => m.Id == mesajId));
        }
    }

    public Task IletildiIsaretleAsync(long mesajId, DateTimeOffset zaman)
    {
        Degistir(m => m.Id == mesajId && m.IletildiZamani is null, m => m with { IletildiZamani = zaman });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<KayitliMesaj>> IletilmemisleriGetirAsync(int aliciId)
    {
        lock (_kilit)
        {
            return Task.FromResult<IReadOnlyList<KayitliMesaj>>(
                _mesajlar.Where(m => m.AliciId == aliciId && m.IletildiZamani is null).OrderBy(m => m.Id).ToList());
        }
    }

    public Task<long?> OkunduIsaretleAsync(int okuyanId, int gonderenId, long sonMesajId, DateTimeOffset zaman)
    {
        long? enBuyuk = null;
        Degistir(m => m.AliciId == okuyanId && m.GonderenId == gonderenId && m.Id <= sonMesajId && m.OkunduZamani is null,
            m =>
            {
                enBuyuk = Math.Max(enBuyuk ?? 0, m.Id);
                return m with { OkunduZamani = zaman, IletildiZamani = m.IletildiZamani ?? zaman };
            });
        return Task.FromResult(enBuyuk);
    }

    public Task<IReadOnlyList<KayitliMesaj>> OzelGecmisAsync(int kullaniciId, int karsiId, long? oncekiMesajId, int adet) =>
        Sec(m => Ikili(m, kullaniciId, karsiId) && (oncekiMesajId is null || m.Id < oncekiMesajId) && Gorunur(m, kullaniciId), adet);

    public Task<IReadOnlyList<KayitliMesaj>> GenelGecmisAsync(int kullaniciId, long? oncekiMesajId, int adet) =>
        Sec(m => m.AliciId is null && (oncekiMesajId is null || m.Id < oncekiMesajId) && Gorunur(m, kullaniciId), adet);

    public async Task<IReadOnlyList<SohbetOzeti>> SohbetleriGetirAsync(int kullaniciId)
    {
        List<(int Karsi, long Son, int Okunmamis)> ozet;
        lock (_kilit)
        {
            ozet = _mesajlar
                .Where(m => m.AliciId is not null && (m.GonderenId == kullaniciId || m.AliciId == kullaniciId) && Gorunur(m, kullaniciId))
                .GroupBy(m => m.GonderenId == kullaniciId ? m.AliciId!.Value : m.GonderenId)
                .Select(g => (g.Key, g.Max(m => m.Id), g.Count(m => m.AliciId == kullaniciId && m.OkunduZamani is null)))
                .OrderByDescending(x => x.Item2)
                .ToList();
        }

        var liste = new List<SohbetOzeti>();
        foreach ((int karsi, _, int okunmamis) in ozet)
        {
            liste.Add(new SohbetOzeti(karsi, (await _kullanicilar.IdIleBulAsync(karsi))!.GorunenAd, okunmamis));
        }
        return liste;
    }

    public Task GizleAsync(int kullaniciId, long mesajId, DateTimeOffset zaman)
    {
        lock (_kilit)
        {
            _gizlemeler.Add((kullaniciId, mesajId));
        }
        return Task.CompletedTask;
    }

    public Task HerkestenSilAsync(long mesajId, DateTimeOffset zaman)
    {
        Degistir(m => m.Id == mesajId && m.HerkestenSilindiZamani is null, m => m with { HerkestenSilindiZamani = zaman });
        return Task.CompletedTask;
    }

    public Task SohbetiSilAsync(int kullaniciId, int karsiId, DateTimeOffset zaman)
    {
        lock (_kilit)
        {
            _sohbetSilmeleri[(kullaniciId, karsiId)] =
                _mesajlar.Where(m => Ikili(m, kullaniciId, karsiId)).Select(m => m.Id).DefaultIfEmpty(0).Max();
        }
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<YonetimSohbetOzeti>> YonetimSohbetleriAsync(int kullaniciId)
    {
        List<(int Karsi, long Son, int Sayi, DateTimeOffset Zaman)> ozet;
        lock (_kilit)
        {
            ozet = _mesajlar
                .Where(m => m.AliciId is not null && (m.GonderenId == kullaniciId || m.AliciId == kullaniciId))
                .GroupBy(m => m.GonderenId == kullaniciId ? m.AliciId!.Value : m.GonderenId)
                .Select(g => (g.Key, g.Max(m => m.Id), g.Count(), g.Max(m => m.GonderimZamani)))
                .OrderByDescending(x => x.Item2)
                .ToList();
        }
        var liste = new List<YonetimSohbetOzeti>();
        foreach ((int karsi, _, int sayi, DateTimeOffset zaman) in ozet)
        {
            liste.Add(new YonetimSohbetOzeti(karsi, (await _kullanicilar.IdIleBulAsync(karsi))!.GorunenAd, sayi, zaman));
        }
        return liste;
    }

    public Task<IReadOnlyList<YonetimMesajKaydi>> YonetimOzelKayitlarAsync(int kullaniciId, int karsiId, long? oncekiMesajId, int adet) =>
        YonetimSec(m => Ikili(m, kullaniciId, karsiId) && (oncekiMesajId is null || m.Id < oncekiMesajId), adet);

    public Task<IReadOnlyList<YonetimMesajKaydi>> YonetimGenelKayitlarAsync(long? oncekiMesajId, int adet) =>
        YonetimSec(m => m.AliciId is null && (oncekiMesajId is null || m.Id < oncekiMesajId), adet);

    // Sahte boyut: her mesaj bu kadar bayt sayilir.
    public const long MesajBasinaBayt = 250;

    public int SikistirmaSayisi { get; private set; }

    public Task<long> TabloBoyutuAsync()
    {
        lock (_kilit)
        {
            return Task.FromResult(_mesajlar.Count * MesajBasinaBayt);
        }
    }

    public Task<long> MesajSayisiAsync()
    {
        lock (_kilit)
        {
            return Task.FromResult((long)_mesajlar.Count);
        }
    }

    public Task<long> EnEskileriSilAsync(long tutulacakAdet)
    {
        lock (_kilit)
        {
            int silinecek = (int)Math.Max(0, _mesajlar.Count - Math.Max(tutulacakAdet, 1));
            var silinenler = _mesajlar.Take(silinecek).Select(m => m.Id).ToHashSet();
            _mesajlar.RemoveRange(0, silinecek);   // liste eskiden yeniye sirali
            _gizlemeler.RemoveWhere(g => silinenler.Contains(g.Mesaj));
            return Task.FromResult((long)silinecek);
        }
    }

    public Task SikistirAsync()
    {
        SikistirmaSayisi++;
        return Task.CompletedTask;
    }

    private static bool Ikili(KayitliMesaj m, int a, int b) =>
        (m.GonderenId == a && m.AliciId == b) || (m.GonderenId == b && m.AliciId == a);

    // Kilit icinde cagrilir.
    private bool Gorunur(KayitliMesaj m, int kullaniciId)
    {
        if (_gizlemeler.Contains((kullaniciId, m.Id)))
        {
            return false;
        }
        if (m.AliciId is int alici)
        {
            int karsi = m.GonderenId == kullaniciId ? alici : m.GonderenId;
            return m.Id > _sohbetSilmeleri.GetValueOrDefault((kullaniciId, karsi));
        }
        return true;
    }

    // Kilit icinde cagrilir.
    private List<int> Gizleyenler(KayitliMesaj m)
    {
        var kimler = _gizlemeler.Where(g => g.Mesaj == m.Id).Select(g => g.Kullanici).ToHashSet();
        if (m.AliciId is int alici)
        {
            if (m.Id <= _sohbetSilmeleri.GetValueOrDefault((m.GonderenId, alici)))
            {
                kimler.Add(m.GonderenId);
            }
            if (m.Id <= _sohbetSilmeleri.GetValueOrDefault((alici, m.GonderenId)))
            {
                kimler.Add(alici);
            }
        }
        return [.. kimler.Order()];
    }

    private void Degistir(Func<KayitliMesaj, bool> kosul, Func<KayitliMesaj, KayitliMesaj> degisiklik)
    {
        lock (_kilit)
        {
            for (int i = 0; i < _mesajlar.Count; i++)
            {
                if (kosul(_mesajlar[i]))
                {
                    _mesajlar[i] = degisiklik(_mesajlar[i]);
                }
            }
        }
    }

    private Task<IReadOnlyList<KayitliMesaj>> Sec(Func<KayitliMesaj, bool> kosul, int adet)
    {
        lock (_kilit)
        {
            return Task.FromResult<IReadOnlyList<KayitliMesaj>>(
                _mesajlar.Where(kosul).OrderByDescending(m => m.Id).Take(adet).ToList());
        }
    }

    private Task<IReadOnlyList<YonetimMesajKaydi>> YonetimSec(Func<KayitliMesaj, bool> kosul, int adet)
    {
        lock (_kilit)
        {
            return Task.FromResult<IReadOnlyList<YonetimMesajKaydi>>(
                _mesajlar.Where(kosul).OrderByDescending(m => m.Id).Take(adet)
                    .Select(m => new YonetimMesajKaydi(m, Gizleyenler(m))).ToList());
        }
    }
}
