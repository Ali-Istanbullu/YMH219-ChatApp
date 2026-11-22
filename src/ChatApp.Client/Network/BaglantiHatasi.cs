namespace ChatApp.Client.Network;

// Sunucuya baglanilamadi ya da baglanti koptu (BEKLENEN hata). Mesaj kullaniciya gosterilecek Turkce metindir.
public sealed class BaglantiHatasi : Exception
{
    public BaglantiHatasi(string kullaniciMesaji, Exception? icHata = null) : base(kullaniciMesaji, icHata)
    {
    }
}
