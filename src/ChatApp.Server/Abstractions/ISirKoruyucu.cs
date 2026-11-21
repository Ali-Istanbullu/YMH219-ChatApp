namespace ChatApp.Server.Abstractions;

// Diske yazilan sirlari (sertifikanin ozel anahtari) sifreler.
// Windows'ta DPAPI ile yapilir (ChatServer.exe); testlerde sahtesi kullanilir.
public interface ISirKoruyucu
{
    byte[] Koru(byte[] acikVeri);
    byte[] Coz(byte[] korunmusVeri);
}
