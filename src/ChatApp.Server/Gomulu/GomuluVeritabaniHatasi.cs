namespace ChatApp.Server.Gomulu;

// ChatServer'in kendi PostgreSQL'i kurulamadi / baslatilamadi (BEKLENEN hata: pencere sebebiyle gosterir).
// Ayrinti: PostgreSQL programlarinin ciktisi ve log dosyasinin sonu (loga ve pencereye yazilir).
public sealed class GomuluVeritabaniHatasi : Exception
{
    public GomuluVeritabaniHatasi(string mesaj, string? ayrinti = null, Exception? icHata = null) : base(mesaj, icHata)
    {
        Ayrinti = ayrinti;
    }

    public string? Ayrinti { get; }
}
