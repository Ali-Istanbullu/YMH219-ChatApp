using ChatApp.Protocol.Modeller;

namespace ChatApp.Server.Data;

// mesajlar tablosundaki bir satir (gonderenin gorunen adiyla birlikte).
public sealed record KayitliMesaj(
    long Id,
    int GonderenId,
    string GonderenAd,
    int? AliciId,
    string Metin,
    DateTimeOffset GonderimZamani,
    DateTimeOffset? IletildiZamani,
    DateTimeOffset? OkunduZamani)
{
    public MesajBilgisi MesajBilgisineCevir()
    {
        MesajDurumu durum = OkunduZamani is not null ? MesajDurumu.Okundu
            : IletildiZamani is not null ? MesajDurumu.Iletildi
            : MesajDurumu.Gonderildi;
        return new MesajBilgisi(Id, GonderenId, GonderenAd, AliciId, Metin, GonderimZamani, durum);
    }
}
