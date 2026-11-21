namespace ChatApp.Protocol.Paketler;

// Istemci -> Sunucu. Mesaj gonderme. AliciId null ise Genel oda.
// IstemciMesajId: sunucu "aldim" derken bu numarayi geri yollar; istemci hangi mesajin onaylandigini bilir.
public sealed record MesajGonder(Guid IstemciMesajId, int? AliciId, string Metin) : Paket;
