using ChatApp.Protocol.Paketler;

namespace ChatApp.Client.Network;

// Sunucuyla acik bir baglanti. Gercegi TlsBaglanti; testlerde sahtesi var.
public interface IBaglanti : IAsyncDisposable
{
    // Siradaki paket. Sunucu baglantiyi kapattiysa null.
    // Baglanti koptuysa IOException, anlasilmayan veri geldiyse ProtokolHatasi.
    Task<Paket?> OkuAsync(CancellationToken iptal);

    // Baglanti koptuysa IOException.
    Task GonderAsync(Paket paket, CancellationToken iptal);
}

// Yeni baglanti acar. Gercegi TlsBaglantiKurucu; testlerde sahtesi var.
public interface IBaglantiKurucu
{
    // Baglanamazsa BaglantiHatasi, sertifika degistiyse SertifikaDegistiHatasi.
    Task<IBaglanti> BaglanAsync(SunucuAdresi adres, CancellationToken iptal);
}
