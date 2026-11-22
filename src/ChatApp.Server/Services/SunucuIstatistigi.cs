namespace ChatApp.Server.Services;

// Sunucu penceresinin altinda gosterilen sayilar.
public sealed record SunucuIstatistigi(int HesapSayisi, long MesajSayisi, long MesajTablosuBayt);
