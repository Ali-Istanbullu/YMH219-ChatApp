using ChatApp.Protocol.Modeller;

namespace ChatApp.Protocol.Paketler;

// Sunucu -> Istemci. Giristen hemen sonra: konusulan kisiler, cevrimici durumlari, okunmamis sayilari.
public sealed record SohbetListesi(IReadOnlyList<KisiBilgisi> Kisiler) : Paket;
