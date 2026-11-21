namespace ChatApp.Protocol.Modeller;

// Sohbet listesindeki bir kisi.
public sealed record KisiBilgisi(
    int Id,
    string GorunenAd,
    bool Cevrimici,
    int OkunmamisSayisi,
    bool Engelli);
