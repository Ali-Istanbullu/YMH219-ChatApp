namespace ChatApp.Protocol.Paketler;

// Istemci -> Sunucu. Gecmis isteme. KarsiId null ise Genel oda; OncekiMesajId verilirse ondan eskiler gelir.
public sealed record GecmisIstegi(int? KarsiId, long? OncekiMesajId) : Paket;
