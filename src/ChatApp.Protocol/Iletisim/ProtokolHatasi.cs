namespace ChatApp.Protocol.Iletisim;

// Karsi taraf kurallara uymayan bir sey gonderdi: bozuk JSON, bilinmeyen paket, cok uzun satir.
// Bu bir BEKLENEN hatadir (karsi taraf bozuk ya da kotu niyetli olabilir): baglanti kapatilir.
public sealed class ProtokolHatasi : Exception
{
    public ProtokolHatasi(string mesaj, Exception? icHata = null) : base(mesaj, icHata)
    {
    }
}
