namespace ChatApp.Protocol.Paketler;

// Istemci -> Sunucu. Herkesten = false: mesaj sadece bende gizlenir.
// Herkesten = true (sadece kendi mesajim): karsi tarafta da "Bu mesaj silindi" gorunur.
// Iki durumda da mesaj veritabaninda kalir, sadece isaretlenir. Cevap: IslemSonucu(MesajSil).
public sealed record MesajSilIstegi(long MesajId, bool Herkesten) : Paket;
