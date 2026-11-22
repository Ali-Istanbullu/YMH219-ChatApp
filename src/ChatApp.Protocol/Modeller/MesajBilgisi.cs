namespace ChatApp.Protocol.Modeller;

// Ekranda gosterilen bir mesaj.
// AliciId null ise mesaj Genel odaya aittir. Zaman her zaman UTC'dir; ekranda yerel saate cevrilir.
// HerkestenSilindi: gonderen "herkesten sil" dedi; Metin bos gelir (asil metin sadece sunucuda kalir).
public sealed record MesajBilgisi(
    long Id,
    int GonderenId,
    string GonderenAd,
    int? AliciId,
    string Metin,
    DateTimeOffset Zaman,
    MesajDurumu Durum,
    bool HerkestenSilindi = false);
