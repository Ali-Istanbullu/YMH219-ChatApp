using ChatApp.Server.Data;

namespace ChatApp.UnitTests.Fakes;

public sealed class BellekDavetDeposu : IDavetDeposu
{
    private readonly Dictionary<string, DavetKoduKaydi> _kodlar = new();
    private readonly Lock _kilit = new();

    public Task<bool> EkleAsync(string kod, DateTimeOffset zaman)
    {
        lock (_kilit)
        {
            return Task.FromResult(_kodlar.TryAdd(kod, new DavetKoduKaydi(kod, zaman, null, null, null)));
        }
    }

    public Task<IReadOnlyList<DavetKoduKaydi>> HepsiniGetirAsync()
    {
        lock (_kilit)
        {
            return Task.FromResult<IReadOnlyList<DavetKoduKaydi>>(_kodlar.Values.ToList());
        }
    }

    // Kayit sirasinda kodu harcar. Kod yoksa ya da kullanilmissa false.
    public bool Harca(string kod, int kullananId, DateTimeOffset zaman)
    {
        lock (_kilit)
        {
            if (!_kodlar.TryGetValue(kod, out DavetKoduKaydi? kayit) || kayit.KullananId is not null)
            {
                return false;
            }
            _kodlar[kod] = kayit with { KullananId = kullananId, KullanilmaZamani = zaman };
            return true;
        }
    }
}
