using System.Text.Json;
using ChatApp.Protocol.Paketler;

namespace ChatApp.Protocol.Iletisim;

// Akistan satir satir paket okur. Her satir bir JSON paketi.
// StreamReader.ReadLine kullanilmiyor, cunku onun satir uzunlugu siniri yok:
// biri satir sonu gondermeden veri basarsa bellek dolardi. Burada sinir asilinca ProtokolHatasi.
public sealed class PaketOkuyucu
{
    private readonly Stream _akis;
    private readonly int _satirSiniri;
    private readonly byte[] _tampon = new byte[8192];
    private readonly MemoryStream _satir = new();
    private int _baslangic;
    private int _bitis;

    public PaketOkuyucu(Stream akis, int satirSiniri)
    {
        ArgumentNullException.ThrowIfNull(akis);
        ArgumentOutOfRangeException.ThrowIfLessThan(satirSiniri, 1);
        _akis = akis;
        _satirSiniri = satirSiniri;
    }

    // Siradaki paketi dondurur. Karsi taraf baglantiyi duzgun kapattiysa null.
    public async Task<Paket?> OkuAsync(CancellationToken iptal)
    {
        while (true)
        {
            int satirSonu = Array.IndexOf(_tampon, (byte)'\n', _baslangic, _bitis - _baslangic);
            if (satirSonu >= 0)
            {
                SatiraEkle(_baslangic, satirSonu - _baslangic);
                _baslangic = satirSonu + 1;
                Paket paket = SatiriCoz();
                _satir.SetLength(0);
                return paket;
            }

            SatiraEkle(_baslangic, _bitis - _baslangic);
            _baslangic = 0;
            _bitis = await _akis.ReadAsync(_tampon, iptal);
            if (_bitis == 0)
            {
                if (_satir.Length > 0)
                {
                    throw new ProtokolHatasi("Baglanti bir satirin ortasinda kapandi.");
                }
                return null;
            }
        }
    }

    private void SatiraEkle(int baslangic, int uzunluk)
    {
        if (_satir.Length + uzunluk > _satirSiniri)
        {
            throw new ProtokolHatasi($"Satir {_satirSiniri} bayt sinirini asti.");
        }
        _satir.Write(_tampon, baslangic, uzunluk);
    }

    private Paket SatiriCoz()
    {
        ReadOnlySpan<byte> satir = _satir.GetBuffer().AsSpan(0, (int)_satir.Length);
        if (satir.Length > 0 && satir[^1] == (byte)'\r')
        {
            satir = satir[..^1];
        }

        try
        {
            return JsonSerializer.Deserialize<Paket>(satir, ProtokolAyarlari.Json)
                ?? throw new ProtokolHatasi("Bos paket geldi.");
        }
        catch (JsonException hata)
        {
            throw new ProtokolHatasi("Paket okunamadi (bozuk JSON ya da eksik alan).", hata);
        }
        catch (NotSupportedException hata)
        {
            // "tur" alani yok ya da bilinmeyen bir paket tipi.
            throw new ProtokolHatasi("Bilinmeyen paket tipi.", hata);
        }
    }
}
