namespace ChatApp.Protocol.Kurallar;

// Ag uzerindeki boyut sinirlari. Sinirsiz satir okumak, kotu niyetli birinin sunucunun
// bellegini doldurmasina izin verirdi.
public static class ProtokolSinirlari
{
    // Sunucunun istemciden kabul ettigi en uzun satir. 2000 karakterlik mesaj rahat sigar.
    public const int SunucununOkuyacagiSatir = 64 * 1024;

    // Istemcinin sunucudan kabul ettigi en uzun satir (gecmis: 100 mesaj x 2000 karakter sigmali).
    public const int IstemcininOkuyacagiSatir = 4 * 1024 * 1024;

    // Gecmis istenince bir seferde gelen mesaj sayisi.
    public const int GecmisSayfaBoyutu = 100;
}
