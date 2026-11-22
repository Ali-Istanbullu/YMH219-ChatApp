using ChatApp.Server.Data;

namespace ChatApp.UnitTests.Fakes;

public sealed class BellekMesajDeposu : IMesajDeposu
{
    private readonly List<KayitliMesaj> _mesajlar = new();
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

    public async Task<long> KaydetAsync(int gonderenId, int? aliciId, string metin, DateTimeOffset zaman)
    {
        string ad = (await _kullanicilar.IdIleBulAsync(gonderenId))?.GorunenAd ?? "?";
        lock (_kilit)
        {
            long id = ++_sonId;
            _mesajlar.Add(new KayitliMesaj(id, gonderenId, ad, aliciId, metin, zaman, null, null));
            return id;
        }
    }

    public Task IletildiIsaretleAsync(long mesajId, DateTimeOffset zaman)
    {
        lock (_kilit)
        {
            int i = _mesajlar.FindIndex(m => m.Id == mesajId && m.IletildiZamani is null);
            if (i >= 0)
            {
                _mesajlar[i] = _mesajlar[i] with { IletildiZamani = zaman };
            }
        }
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
        lock (_kilit)
        {
            long? enBuyuk = null;
            for (int i = 0; i < _mesajlar.Count; i++)
            {
                KayitliMesaj m = _mesajlar[i];
                if (m.AliciId == okuyanId && m.GonderenId == gonderenId && m.Id <= sonMesajId && m.OkunduZamani is null)
                {
                    _mesajlar[i] = m with { OkunduZamani = zaman, IletildiZamani = m.IletildiZamani ?? zaman };
                    enBuyuk = Math.Max(enBuyuk ?? 0, m.Id);
                }
            }
            return Task.FromResult(enBuyuk);
        }
    }

    public Task<IReadOnlyList<KayitliMesaj>> OzelGecmisAsync(int kullaniciId, int karsiId, long? oncekiMesajId, int adet) =>
        Sec(m => ((m.GonderenId == kullaniciId && m.AliciId == karsiId) || (m.GonderenId == karsiId && m.AliciId == kullaniciId))
                 && (oncekiMesajId is null || m.Id < oncekiMesajId), adet);

    public Task<IReadOnlyList<KayitliMesaj>> GenelGecmisAsync(long? oncekiMesajId, int adet) =>
        Sec(m => m.AliciId is null && (oncekiMesajId is null || m.Id < oncekiMesajId), adet);

    public async Task<IReadOnlyList<SohbetOzeti>> SohbetleriGetirAsync(int kullaniciId)
    {
        List<(int Karsi, long Son, int Okunmamis)> ozet;
        lock (_kilit)
        {
            ozet = _mesajlar
                .Where(m => m.AliciId is not null && (m.GonderenId == kullaniciId || m.AliciId == kullaniciId))
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
            _mesajlar.RemoveRange(0, silinecek);   // liste eskiden yeniye sirali
            return Task.FromResult((long)silinecek);
        }
    }

    public Task SikistirAsync()
    {
        SikistirmaSayisi++;
        return Task.CompletedTask;
    }

    private Task<IReadOnlyList<KayitliMesaj>> Sec(Func<KayitliMesaj, bool> kosul, int adet)
    {
        lock (_kilit)
        {
            return Task.FromResult<IReadOnlyList<KayitliMesaj>>(
                _mesajlar.Where(kosul).OrderByDescending(m => m.Id).Take(adet).ToList());
        }
    }
}
