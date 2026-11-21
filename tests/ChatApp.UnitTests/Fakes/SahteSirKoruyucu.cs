using ChatApp.Server.Abstractions;

namespace ChatApp.UnitTests.Fakes;

// Veriyi ters cevirir: "korundu" mu diye kontrol edilebilsin, ama gercek sifreleme yok.
public sealed class SahteSirKoruyucu : ISirKoruyucu
{
    public byte[] Koru(byte[] acikVeri) => acikVeri.Reverse().ToArray();
    public byte[] Coz(byte[] korunmusVeri) => korunmusVeri.Reverse().ToArray();
}
