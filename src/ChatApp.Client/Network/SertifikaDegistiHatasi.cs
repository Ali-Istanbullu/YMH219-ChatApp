namespace ChatApp.Client.Network;

// Sunucunun sertifikasi, bu bilgisayarin daha once hatirladigindan farkli.
// Ya sunucu sertifikasini yeniledi ya da araya biri girdi. Kullanici karar verir.
public sealed class SertifikaDegistiHatasi : Exception
{
    public SertifikaDegistiHatasi(SunucuAdresi adres, string eskiParmakIzi, string yeniParmakIzi)
        : base("Sunucunun sertifikasi degisti.")
    {
        Adres = adres;
        EskiParmakIzi = eskiParmakIzi;
        YeniParmakIzi = yeniParmakIzi;
    }

    public SunucuAdresi Adres { get; }
    public string EskiParmakIzi { get; }
    public string YeniParmakIzi { get; }
}
