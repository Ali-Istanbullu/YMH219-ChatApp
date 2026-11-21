namespace ChatApp.Server.Data;

// Sohbet listesindeki bir kisi: kim, adi ne, bana kac okunmamis mesaji var.
public sealed record SohbetOzeti(int KarsiId, string KarsiAd, int Okunmamis);
