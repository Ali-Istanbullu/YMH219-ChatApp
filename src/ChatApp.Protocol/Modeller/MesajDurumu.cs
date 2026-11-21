namespace ChatApp.Protocol.Modeller;

// Ozel mesajin durumu: tek tik / cift tik / mavi cift tik.
public enum MesajDurumu
{
    Gonderildi,   // sunucuya ulasti (tek tik)
    Iletildi,     // karsi tarafa iletildi (cift tik)
    Okundu        // karsi taraf okudu (mavi cift tik)
}
