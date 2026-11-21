namespace ChatApp.Server.Data;

// Veritabanina ulasilamadi (PostgreSQL kapali, baglanti koptu, zaman asimi...).
// BEKLENEN bir hatadir: istemciye "sunucuda gecici sorun" denir, sunucu calismaya devam eder.
// SQL yazim hatasi gibi KOD hatalari bu tipe sarilmaz; onlar bug'dir ve oldugu gibi yukari cikar.
public sealed class DepoHatasi : Exception
{
    public DepoHatasi(string mesaj, Exception icHata) : base(mesaj, icHata)
    {
    }
}
