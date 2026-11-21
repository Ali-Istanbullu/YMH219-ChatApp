using ChatApp.Protocol.Paketler;

namespace ChatApp.Server.Abstractions;

// Sunucuya bagli bir istemci. Gercegi: Network/IstemciBaglantisi; testlerde sahtesi var.
public interface IIstemciOturumu
{
    Guid BaglantiId { get; }
    string IpAdresi { get; }
    DateTimeOffset BaglanmaZamani { get; }

    // Giris yapilana kadar null.
    int? KullaniciId { get; }
    string? GorunenAd { get; }

    void GirisYapildi(int kullaniciId, string gorunenAd);

    // Paketi gonderir. Baglanti kopmussa (beklenen ag hatasi) sessizce kapanir, exception firlatmaz.
    Task GonderAsync(Paket paket);

    // Istenirse once sebep paketini gonderir, sonra baglantiyi kapatir. Birden cok cagrilabilir.
    Task KapatAsync(OturumSonlandi? sebep);
}
