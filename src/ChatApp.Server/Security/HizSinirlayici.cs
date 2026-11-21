using ChatApp.Server.Abstractions;

namespace ChatApp.Server.Security;

// "Su sure icinde en fazla su kadar" kurali (kayan pencere).
// Ornek: anahtar "mesaj:482913", en fazla 5, pencere 1 saniye -> saniyede 5'ten fazla mesaj reddedilir.
// Spam'i ve deneme-yanilma saldirilarini yavaslatir.
public sealed class HizSinirlayici
{
    private const int TemizlikAraligi = 1000;   // her 1000 istekte bir eski kayitlar silinir

    private readonly ISaat _saat;
    private readonly Dictionary<string, Queue<DateTimeOffset>> _kayitlar = new();
    private readonly Lock _kilit = new();
    private int _istekSayaci;

    public HizSinirlayici(ISaat saat)
    {
        ArgumentNullException.ThrowIfNull(saat);
        _saat = saat;
    }

    // Izin verilirse istegi sayar ve true dondurur; sinir asildiysa false (istek sayilmaz).
    public bool IzinVer(string anahtar, int enFazla, TimeSpan pencere)
    {
        lock (_kilit)
        {
            DateTimeOffset simdi = _saat.SimdiUtc;
            if (++_istekSayaci % TemizlikAraligi == 0)
            {
                BosKalanlariSil(simdi, pencere);
            }

            if (!_kayitlar.TryGetValue(anahtar, out Queue<DateTimeOffset>? zamanlar))
            {
                zamanlar = new Queue<DateTimeOffset>();
                _kayitlar[anahtar] = zamanlar;
            }

            while (zamanlar.Count > 0 && zamanlar.Peek() <= simdi - pencere)
            {
                zamanlar.Dequeue();
            }

            if (zamanlar.Count >= enFazla)
            {
                return false;
            }

            zamanlar.Enqueue(simdi);
            return true;
        }
    }

    private void BosKalanlariSil(DateTimeOffset simdi, TimeSpan pencere)
    {
        // En uzun pencere 1 saat; ondan eski kaydi olmayan anahtarlar silinir.
        TimeSpan enUzun = pencere > TimeSpan.FromHours(1) ? pencere : TimeSpan.FromHours(1);
        foreach (string anahtar in _kayitlar.Where(k => k.Value.Count == 0 || k.Value.Last() <= simdi - enUzun)
                     .Select(k => k.Key).ToList())
        {
            _kayitlar.Remove(anahtar);
        }
    }
}
