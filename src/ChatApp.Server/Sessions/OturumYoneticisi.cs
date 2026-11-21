using System.Collections.Concurrent;
using ChatApp.Server.Abstractions;

namespace ChatApp.Server.Sessions;

// Kim bagli, kim giris yapti? Butun baglantilarin listesi burada tutulur.
// Ayni anda bircok baglantidan cagrilir; ConcurrentDictionary bu yuzden.
public sealed class OturumYoneticisi
{
    private readonly ConcurrentDictionary<Guid, IIstemciOturumu> _baglantilar = new();
    private readonly ConcurrentDictionary<int, IIstemciOturumu> _girisYapanlar = new();

    // Liste degisti (sunucu penceresi yenilensin).
    public event EventHandler? Degisti;

    public void BaglantiEkle(IIstemciOturumu oturum)
    {
        _baglantilar[oturum.BaglantiId] = oturum;
        Degisti?.Invoke(this, EventArgs.Empty);
    }

    // Baglanti kapaninca. Giris yapmissa ve hala o hesabin guncel oturumuysa onu da cikarir.
    // Cikarildiysa true (yani bu kullanici artik cevrimdisi).
    public bool BaglantiCikar(IIstemciOturumu oturum)
    {
        _baglantilar.TryRemove(oturum.BaglantiId, out _);
        bool cevrimdisiOldu = oturum.KullaniciId is int id
            && _girisYapanlar.TryRemove(new KeyValuePair<int, IIstemciOturumu>(id, oturum));
        Degisti?.Invoke(this, EventArgs.Empty);
        return cevrimdisiOldu;
    }

    // Hesabi bu oturuma baglar. Ayni hesap baska yerden zaten girmisse o eski oturumu dondurur
    // (cagiran onu kapatir: "hesabiniza baska yerden giris yapildi").
    public IIstemciOturumu? GirisYap(int kullaniciId, IIstemciOturumu oturum)
    {
        IIstemciOturumu? eski = null;
        _girisYapanlar.AddOrUpdate(kullaniciId, oturum, (_, mevcut) =>
        {
            eski = mevcut;
            return oturum;
        });
        Degisti?.Invoke(this, EventArgs.Empty);
        return ReferenceEquals(eski, oturum) ? null : eski;
    }

    public IIstemciOturumu? Bul(int kullaniciId) =>
        _girisYapanlar.TryGetValue(kullaniciId, out IIstemciOturumu? oturum) ? oturum : null;

    public bool CevrimiciMi(int kullaniciId) => _girisYapanlar.ContainsKey(kullaniciId);

    public IReadOnlyList<IIstemciOturumu> GirisYapanlar() => _girisYapanlar.Values.ToList();

    public IReadOnlyList<IIstemciOturumu> TumBaglantilar() => _baglantilar.Values.ToList();
}
