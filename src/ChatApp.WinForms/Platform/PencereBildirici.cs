using System.Media;
using System.Runtime.InteropServices;

namespace ChatApp.WinForms.Platform;

// Yeni mesaj bildirimi (Ali karari): gorev cubugundaki simge yanip soner + ses.
// Pencere one gelince yanip sonme kendiliginden durur. Ses en fazla 2 sn'de bir calar (mesaj yagmurunda susar).
public sealed class PencereBildirici
{
    private static readonly TimeSpan SesAraligi = TimeSpan.FromSeconds(2);

    private const uint HepsiniYakSondur = 3;        // FLASHW_ALL: baslik + gorev cubugu
    private const uint OneGelenceyeKadar = 12;         // FLASHW_TIMERNOFG: pencere one gelene kadar

    private DateTime _sonSes = DateTime.MinValue;

    public void Bildir(Form pencere)
    {
        ArgumentNullException.ThrowIfNull(pencere);
        if (!pencere.IsHandleCreated)
        {
            return;
        }

        var bilgi = new YanipSonmeBilgisi
        {
            Boyut = (uint)Marshal.SizeOf<YanipSonmeBilgisi>(),
            Pencere = pencere.Handle,
            Bayraklar = HepsiniYakSondur | OneGelenceyeKadar,
            Sayi = 0,
            Aralik = 0
        };
        FlashWindowEx(ref bilgi);   // basarisiz olursa sadece yanip sonmez; onemli degil

        if (DateTime.UtcNow - _sonSes >= SesAraligi)
        {
            _sonSes = DateTime.UtcNow;
            SystemSounds.Asterisk.Play();
        }
    }

    // Windows'un FLASHWINFO yapisi.
    [StructLayout(LayoutKind.Sequential)]
    private struct YanipSonmeBilgisi
    {
        public uint Boyut;
        public IntPtr Pencere;
        public uint Bayraklar;
        public uint Sayi;
        public uint Aralik;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref YanipSonmeBilgisi bilgi);
}
