namespace ChatApp.Protocol.Paketler;

// Istemci -> Sunucu. Bu kisiyle olan sohbet sadece bende gizlenir; karsi taraf gormeye devam eder.
// O kisi yeniden yazarsa sohbet yeni mesajla geri gelir. Cevap: IslemSonucu(SohbetSil).
public sealed record SohbetSilIstegi(int KarsiId) : Paket;
