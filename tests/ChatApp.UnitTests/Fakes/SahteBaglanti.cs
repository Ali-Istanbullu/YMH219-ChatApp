using System.Collections.Concurrent;
using System.Threading.Channels;
using ChatApp.Client.Network;
using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;

namespace ChatApp.UnitTests.Fakes;

// Istemcinin sunucuyla baglantisinin sahtesi. Cevaplayici: gonderilen pakete sunucunun ne cevap verecegi.
public sealed class SahteBaglanti : IBaglanti
{
    private readonly Channel<object> _gelen = Channel.CreateUnbounded<object>();

    public SahteBaglanti(Func<Paket, IEnumerable<Paket>> cevaplayici)
    {
        Cevaplayici = cevaplayici;
    }

    public Func<Paket, IEnumerable<Paket>> Cevaplayici { get; set; }
    public ConcurrentQueue<Paket> Gonderilenler { get; } = new();
    public bool Kapatildi { get; private set; }

    // Sunucudan paket geldi.
    public void Gelsin(Paket paket) => _gelen.Writer.TryWrite(paket);

    // Baglanti koptu (ag hatasi).
    public void Kopar() => _gelen.Writer.TryWrite(new IOException("sahte: baglanti koptu"));

    public async Task<Paket?> OkuAsync(CancellationToken iptal)
    {
        object oge = await _gelen.Reader.ReadAsync(iptal);
        return oge switch
        {
            Exception hata => throw hata,
            Paket paket => paket,
            _ => null
        };
    }

    public Task GonderAsync(Paket paket, CancellationToken iptal)
    {
        if (Kapatildi)
        {
            throw new IOException("sahte: baglanti kapali");
        }
        Gonderilenler.Enqueue(paket);
        foreach (Paket cevap in Cevaplayici(paket))
        {
            Gelsin(cevap);
        }
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Kapatildi = true;
        _gelen.Writer.TryWrite(new ObjectDisposedException("sahte baglanti"));
        return ValueTask.CompletedTask;
    }
}

// Her baglanma isteginde siradaki adimi calistirir: yeni sahte baglanti ya da hata.
public sealed class SahteBaglantiKurucu : IBaglantiKurucu
{
    private readonly ConcurrentQueue<Func<IBaglanti>> _siradakiler = new();

    public ConcurrentQueue<SahteBaglanti> Acilanlar { get; } = new();
    public int DenemeSayisi;

    // Sira bitince kullanilacak varsayilan: dogru sifreyi kabul eden sunucu.
    public Func<SahteBaglanti> Varsayilan { get; set; } = () => new SahteBaglanti(SahteSunucu.Cevapla);

    public void SonrakiHataVersin(Exception hata) => _siradakiler.Enqueue(() => throw hata);

    public void SonrakiBuOlsun(SahteBaglanti baglanti) => _siradakiler.Enqueue(() => baglanti);

    public Task<IBaglanti> BaglanAsync(SunucuAdresi adres, CancellationToken iptal)
    {
        Interlocked.Increment(ref DenemeSayisi);
        IBaglanti baglanti = _siradakiler.TryDequeue(out Func<IBaglanti>? adim) ? adim() : Varsayilan();
        if (baglanti is SahteBaglanti sahte)
        {
            Acilanlar.Enqueue(sahte);
        }
        return Task.FromResult(baglanti);
    }
}

// Basit sahte sunucu davranisi: "ali" her sifreyle girer (sifre "yanlis" haric).
public static class SahteSunucu
{
    public const int AliId = 123456;

    private static long _sonMesajId;

    public static IEnumerable<Paket> Cevapla(Paket paket) => paket switch
    {
        GirisIstegi { Sifre: "yanlis" } => [new GirisSonucu(false, null, null, HataKodu.KullaniciAdiVeyaSifreYanlis, null)],
        GirisIstegi => [new GirisSonucu(true, AliId, "Ali", null, null)],
        KullaniciSorgula sorgu => [new KullaniciBilgisi(sorgu.KullaniciId, true, "Kisi" + sorgu.KullaniciId, true)],
        GecmisIstegi gecmis => [new GecmisCevabi(gecmis.KarsiId, [], false)],
        MesajGonder mesaj => [new MesajKabulEdildi(mesaj.IstemciMesajId,
            new MesajBilgisi(Interlocked.Increment(ref _sonMesajId), AliId, "Ali", mesaj.AliciId, mesaj.Metin, DateTimeOffset.UnixEpoch, MesajDurumu.Gonderildi))],
        _ => []
    };
}
