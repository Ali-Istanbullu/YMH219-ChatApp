using ChatApp.Protocol.Modeller;

namespace ChatApp.Server.Data;

// Veritabanindaki bir mesaj. Herkesten silinmis olsa bile metin burada durur (yonetici kaniti icin);
// istemcilere giderken metin bosaltilir.
public sealed record KayitliMesaj(
    long Id,
    int GonderenId,
    string GonderenAd,
    int? AliciId,
    string Metin,
    DateTimeOffset GonderimZamani,
    DateTimeOffset? IletildiZamani,
    DateTimeOffset? OkunduZamani,
    DateTimeOffset? HerkestenSilindiZamani = null)
{
    public MesajDurumu Durum => OkunduZamani is not null ? MesajDurumu.Okundu
        : IletildiZamani is not null ? MesajDurumu.Iletildi
        : MesajDurumu.Gonderildi;

    public bool HerkestenSilindi => HerkestenSilindiZamani is not null;

    // Istemciye gidecek bicim: herkesten silinmis mesajin metni gonderilmez.
    public MesajBilgisi MesajBilgisineCevir() =>
        new(Id, GonderenId, GonderenAd, AliciId, HerkestenSilindi ? string.Empty : Metin, GonderimZamani, Durum, HerkestenSilindi);
}
