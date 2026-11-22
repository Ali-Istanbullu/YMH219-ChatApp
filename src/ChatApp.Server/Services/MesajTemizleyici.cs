using ChatApp.Server.Abstractions;
using ChatApp.Server.Data;

namespace ChatApp.Server.Services;

// Veritabani sisip diski doldurmasin: mesaj tablosu 500 MB'i gecince en eski mesajlar silinir,
// tablo 250 MB'a dusurulur. Sadece mesajlar silinir; hesaplar, davet kodlari, engellemeler kalir.
//
// Kac mesaj kalacagi orantiyla hesaplanir: 300.000 mesaj 600 MB tutuyorsa 250 MB icin 125.000 mesaj kalir.
// Silmeden sonra tablo sikistirilir (VACUUM FULL); yoksa PostgreSQL dosyayi kucultmez.
public sealed class MesajTemizleyici
{
    public const long VarsayilanUstSinir = 500L * 1024 * 1024;
    public const long VarsayilanHedef = 250L * 1024 * 1024;

    private readonly IMesajDeposu _mesajlar;
    private readonly IGunlukcu _gunlukcu;
    private readonly long _ustSinir;
    private readonly long _hedef;

    public MesajTemizleyici(IMesajDeposu mesajlar, IGunlukcu gunlukcu)
        : this(mesajlar, gunlukcu, VarsayilanUstSinir, VarsayilanHedef)
    {
    }

    // Testler kucuk sinirlarla calissin diye.
    internal MesajTemizleyici(IMesajDeposu mesajlar, IGunlukcu gunlukcu, long ustSinir, long hedef)
    {
        ArgumentNullException.ThrowIfNull(mesajlar);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        ArgumentOutOfRangeException.ThrowIfLessThan(hedef, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ustSinir, hedef);
        _mesajlar = mesajlar;
        _gunlukcu = gunlukcu;
        _ustSinir = ustSinir;
        _hedef = hedef;
    }

    // Gerekirse temizler; silinen mesaj sayisini dondurur. Veritabanina ulasilamazsa DepoHatasi.
    public async Task<long> GerekirseTemizleAsync()
    {
        long boyut = await _mesajlar.TabloBoyutuAsync();
        if (boyut <= _ustSinir)
        {
            return 0;
        }

        long sayi = await _mesajlar.MesajSayisiAsync();
        long tutulacak = (long)(sayi * ((double)_hedef / boyut));
        long silinen = await _mesajlar.EnEskileriSilAsync(tutulacak);
        await _mesajlar.SikistirAsync();

        long yeniBoyut = await _mesajlar.TabloBoyutuAsync();
        _gunlukcu.Bilgi($"Mesaj temizligi: {silinen} eski mesaj silindi, tablo {boyut / 1024 / 1024} MB -> {yeniBoyut / 1024 / 1024} MB.");
        return silinen;
    }
}
