namespace ChatApp.Server.Data;

// Veritabanina ulasilamadi (PostgreSQL kapali, baglanti koptu, zaman asimi, sifre yanlis...).
// BEKLENEN bir hatadir: istemciye "sunucuda gecici sorun" denir, sunucu calismaya devam eder.
// SQL yazim hatasi gibi KOD hatalari bu tipe sarilmaz; onlar bug'dir ve oldugu gibi yukari cikar.
public sealed class DepoHatasi : Exception
{
    public DepoHatasi(string mesaj, Exception icHata, DepoHatasiSebebi sebep = DepoHatasiSebebi.Diger)
        : base(mesaj, icHata)
    {
        Sebep = sebep;
    }

    public DepoHatasiSebebi Sebep { get; }
}
