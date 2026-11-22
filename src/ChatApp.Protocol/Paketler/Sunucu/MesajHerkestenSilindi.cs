namespace ChatApp.Protocol.Paketler;

// Sunucu -> Istemci. Bir mesaj gonderen tarafindan herkesten silindi ("Bu mesaj silindi" gosterilir).
public sealed record MesajHerkestenSilindi(long MesajId) : Paket;
