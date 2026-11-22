using ChatApp.Protocol.Modeller;

namespace ChatApp.Client.State;

// Ekrandaki bir mesaj. Gonderdigim mesaj once Id'siz ("gonderiliyor") eklenir;
// sunucu kabul edince Id ve saat dolar, sonra tikler (Durum) guncellenir.
// Baglanti yokken yazilan mesaj BaglantiBekliyor olur; baglanti gelince kendiliginden gonderilir.
public sealed class MesajSatiri
{
    public long? Id { get; internal set; }
    public Guid? IstemciMesajId { get; init; }
    public required int GonderenId { get; init; }
    public required string GonderenAd { get; init; }
    public int? AliciId { get; init; }
    public required string Metin { get; init; }
    public DateTimeOffset Zaman { get; internal set; }
    public MesajDurumu Durum { get; internal set; }

    // Sunucu reddettiyse kullaniciya gosterilecek sebep ("Tekrar gönder" ile yeniden denenebilir).
    public string? Hata { get; internal set; }

    // Baglanti yok: baglanti gelince kendiliginden gonderilecek.
    public bool BaglantiBekliyor { get; internal set; }

    // Gonderen "herkesten sil" dedi: metin gosterilmez, "Bu mesaj silindi" yazar.
    public bool HerkestenSilindi { get; internal set; }

    public bool Gonderiliyor => Id is null && Hata is null;

    // Listedeki yeri: numarali mesaj numarasiyla; henuz numarasi olmayan (gonderiliyor) mesaj,
    // yazildigi andaki son mesajin hemen arkasinda (BeklemeSirasi: yazilma sirasi). Boylece sonradan
    // gelen/gonderilen mesajlar onun ustune gecmez.
    internal long OncekiMesajId { get; init; }
    internal long BeklemeSirasi { get; init; }

    internal static MesajSatiri Olustur(MesajBilgisi m) => new()
    {
        Id = m.Id,
        GonderenId = m.GonderenId,
        GonderenAd = m.GonderenAd,
        AliciId = m.AliciId,
        Metin = m.Metin,
        Zaman = m.Zaman,
        Durum = m.Durum,
        HerkestenSilindi = m.HerkestenSilindi
    };
}
