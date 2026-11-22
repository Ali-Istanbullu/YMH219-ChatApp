namespace ChatApp.Server.Data;

// Yonetici ekrani ("Mesaj kayitlari") icin: silinmis ve gizlenmis mesajlar dahil HER SEY.

// Gizleyenler: bu mesaji kendinden silen ya da sohbeti silerek gizleyen kullanicilar.
public sealed record YonetimMesajKaydi(KayitliMesaj Mesaj, IReadOnlyList<int> Gizleyenler);

// Bir kullanicinin ozel mesajlastigi biri (silinen sohbetler dahil).
public sealed record YonetimSohbetOzeti(int KarsiId, string KarsiAd, int MesajSayisi, DateTimeOffset SonMesajZamani);
