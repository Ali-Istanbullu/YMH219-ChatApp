using ChatApp.Protocol.Modeller;

namespace ChatApp.Protocol.Paketler;

// Sunucu -> Istemci. Karsi taraf, gonderdigin mesajlari bu numaraya kadar okudu (mavi tik).
public sealed record MesajlarOkundu(int OkuyanId, long SonMesajId) : Paket;
