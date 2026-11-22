namespace ChatApp.Client.State;

// Sol listedeki bir sohbet: "Genel" oda (KarsiId = null) ya da bir kisiyle ozel sohbet.
public sealed class Sohbet
{
    private readonly List<MesajSatiri> _mesajlar = [];

    internal Sohbet(int? karsiId, string ad)
    {
        KarsiId = karsiId;
        Ad = ad;
    }

    public int? KarsiId { get; }
    public bool GenelMi => KarsiId is null;
    public string Ad { get; internal set; }
    public bool Cevrimici { get; internal set; }
    public int Okunmamis { get; internal set; }
    public bool Engelli { get; internal set; }

    // Eskiden yeniye. Gonderiliyor olanlar en sonda.
    public IReadOnlyList<MesajSatiri> Mesajlar => _mesajlar;
    public bool GecmisYuklendi { get; internal set; }
    public bool DahaEskiVar { get; internal set; }

    // En son kacinci mesaja kadar "okundu" bildirildi (ayni bildirim tekrar gitmesin).
    internal long SonOkunduBildirilen { get; set; }

    // Liste siralamasi icin: en son mesajin zamani.
    public DateTimeOffset? SonMesajZamani => _mesajlar.Count == 0 ? null : _mesajlar[^1].Zaman;

    // Sunucunun numarasini verdigi en eski mesaj ("daha eski" isterken kullanilir).
    public long? EnEskiMesajId => _mesajlar.FirstOrDefault(m => m.Id is not null)?.Id;

    internal void Ekle(MesajSatiri satir)
    {
        if (satir.Id is long id && _mesajlar.Any(m => m.Id == id))
        {
            return;   // ayni mesaj ikinci kez gelmez (gecmis + canli mesaj cakisabilir)
        }
        _mesajlar.Add(satir);
        Sirala();
    }

    // En buyuk mesaj numarasi (yeni yazilan mesajin yerini belirlemek icin).
    internal long SonMesajId => _mesajlar.Where(m => m.Id is not null).Select(m => m.Id!.Value).DefaultIfEmpty(0).Max();

    internal void Sirala()
    {
        // Numarali mesaj: (numara, 0). Numarasiz (gonderiliyor/gonderilemedi): (yazildigindaki son numara, yazilma sirasi).
        // Ornek: 7 numarali mesajdan sonra yazilan gonderilemeyen mesaj (7, 3), sonra gelen 8 numarali mesajin ustunde kalir.
        List<MesajSatiri> sirali = [.. _mesajlar
            .OrderBy(m => m.Id ?? m.OncekiMesajId)
            .ThenBy(m => m.Id is null ? m.BeklemeSirasi : 0)];
        _mesajlar.Clear();
        _mesajlar.AddRange(sirali);
    }

    internal void Cikar(MesajSatiri satir) => _mesajlar.Remove(satir);
}
