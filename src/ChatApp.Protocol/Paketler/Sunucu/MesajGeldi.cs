using ChatApp.Protocol.Modeller;

namespace ChatApp.Protocol.Paketler;

// Sunucu -> Istemci. Yeni mesaj geldi (ozel ya da Genel oda).
public sealed record MesajGeldi(MesajBilgisi Mesaj) : Paket;
