using System.Runtime.InteropServices;
using System.Security.Cryptography;
using ChatApp.Server.Abstractions;

namespace ChatApp.Server.WinForms.Platform;

// Windows DPAPI: veriyi o anki Windows kullanicisinin anahtariyla sifreler.
// Dosya calinsa bile baska bilgisayarda / baska Windows kullanicisinda cozulemez.
// .NET'te DPAPI icin ayri NuGet paketi gerekiyor; yeni paket eklememek icin Windows'un
// crypt32.dll fonksiyonlari dogrudan cagrilir (P/Invoke).
// Cozulemezse CryptographicException (cagiranlar bunu bekler).
public sealed class DpapiSirKoruyucu : ISirKoruyucu
{
    private const int ArayuzGosterme = 0x1;   // CRYPTPROTECT_UI_FORBIDDEN: ekrana soru cikarmasin

    public byte[] Koru(byte[] acikVeri) => Calistir(acikVeri, koru: true);

    public byte[] Coz(byte[] korunmusVeri) => Calistir(korunmusVeri, koru: false);

    private static byte[] Calistir(byte[] veri, bool koru)
    {
        ArgumentNullException.ThrowIfNull(veri);
        var giris = new VeriBlogu { Uzunluk = veri.Length, Isaretci = Marshal.AllocHGlobal(Math.Max(veri.Length, 1)) };
        var cikis = new VeriBlogu();
        try
        {
            Marshal.Copy(veri, 0, giris.Isaretci, veri.Length);
            bool basarili = koru
                ? CryptProtectData(ref giris, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ArayuzGosterme, out cikis)
                : CryptUnprotectData(ref giris, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ArayuzGosterme, out cikis);
            if (!basarili)
            {
                throw new CryptographicException(Marshal.GetLastPInvokeError());
            }

            var sonuc = new byte[cikis.Uzunluk];
            Marshal.Copy(cikis.Isaretci, sonuc, 0, cikis.Uzunluk);
            return sonuc;
        }
        finally
        {
            Marshal.FreeHGlobal(giris.Isaretci);
            if (cikis.Isaretci != IntPtr.Zero)
            {
                LocalFree(cikis.Isaretci);   // Windows'un ayirdigi bellek Windows'a geri verilir
            }
        }
    }

    // Windows'un DATA_BLOB yapisi: uzunluk + bellek adresi.
    [StructLayout(LayoutKind.Sequential)]
    private struct VeriBlogu
    {
        public int Uzunluk;
        public IntPtr Isaretci;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref VeriBlogu giris, string? aciklama, IntPtr ekEntropi,
        IntPtr ayrilmis, IntPtr soru, int bayraklar, out VeriBlogu cikis);

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CryptUnprotectData(ref VeriBlogu giris, IntPtr aciklama, IntPtr ekEntropi,
        IntPtr ayrilmis, IntPtr soru, int bayraklar, out VeriBlogu cikis);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr bellek);
}
