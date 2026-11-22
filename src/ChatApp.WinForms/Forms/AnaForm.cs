using ChatApp.Client.Presentation;
using ChatApp.Client.Session;
using ChatApp.Client.State;
using ChatApp.WinForms.Platform;

namespace ChatApp.WinForms.Forms;

// ================================================================================
//  CHATAPP ANA PENCERESI
//
//  Tek pencere, iki ekran: giris paneli <-> sohbet paneli.
//  (Tek pencere olmasinin sebebi: arka plandaki baglanti, olaylarini hep ayni pencerenin
//   is parcacigina getirir; pencere degisince bu bag kopmasin.)
//  Ayrica: pencere one gelince/arkaya gecince yoneticiye haber verir, bildirimde yanip soner + ses.
// ================================================================================
public sealed class AnaForm : Form
{
    private readonly SohbetYoneticisi _yonetici;
    private readonly GirisPaneli _girisPaneli;
    private readonly SohbetPaneli _sohbetPaneli;
    private readonly PencereBildirici _bildirici;

    private bool _kapanabilir;

    public AnaForm(SohbetYoneticisi yonetici, GirisPaneli girisPaneli, SohbetPaneli sohbetPaneli, PencereBildirici bildirici)
    {
        ArgumentNullException.ThrowIfNull(yonetici);
        ArgumentNullException.ThrowIfNull(girisPaneli);
        ArgumentNullException.ThrowIfNull(sohbetPaneli);
        ArgumentNullException.ThrowIfNull(bildirici);
        _yonetici = yonetici;
        _girisPaneli = girisPaneli;
        _sohbetPaneli = sohbetPaneli;
        _bildirici = bildirici;

        Text = "ChatApp";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(900, 600);
        MinimumSize = new Size(700, 480);
        Font = new Font("Segoe UI", 9.75f);

        Controls.Add(_sohbetPaneli);
        Controls.Add(_girisPaneli);

        _girisPaneli.GirisYapildi += async () => await GirisYapildiAsync();
        _yonetici.BaglantiDegisti += BaglantiDegisti;
        _yonetici.BildirimGerekli += BildirimGerekli;
        _yonetici.Uyari += metin => ArayuzdeCalistir(() =>
            MessageBox.Show(this, metin, GenelHataYakalayici.Baslik, MessageBoxButtons.OK, MessageBoxIcon.Warning));

        Activated += async (_, _) => await _yonetici.PencereAktifligiDegistiAsync(true);
        Deactivate += async (_, _) => await _yonetici.PencereAktifligiDegistiAsync(false);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        GirisEkraniniGoster(null);
    }

    // Bagliyken kapatilirsa once cikis yapilir (sunucu "cevrimdisi" bilgisini hemen dagitsin).
    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (_kapanabilir || _yonetici.Durum is null)
        {
            return;
        }
        e.Cancel = true;
        Enabled = false;
        await _yonetici.CikisYapAsync();
        _kapanabilir = true;
        Close();
    }

    private async Task GirisYapildiAsync()
    {
        await _yonetici.OturumuBaslatAsync();
        Text = $"ChatApp - {_yonetici.Durum?.BenimAdim}";
        _girisPaneli.Visible = false;
        _sohbetPaneli.Visible = true;
        _sohbetPaneli.Hazirla();
    }

    private void GirisEkraniniGoster(string? mesaj)
    {
        Text = "ChatApp";
        _sohbetPaneli.Visible = false;
        _girisPaneli.Visible = true;
        _girisPaneli.Hazirla(mesaj);
    }

    private void BaglantiDegisti(BaglantiDurumu durum, string? aciklama) => ArayuzdeCalistir(() =>
    {
        switch (durum)
        {
            case BaglantiDurumu.Bagli:
                _sohbetPaneli.BaglantiDurumunuGoster("Bağlı", Color.DimGray);
                break;
            case BaglantiDurumu.YenidenBaglaniyor:
                _sohbetPaneli.BaglantiDurumunuGoster(aciklama ?? "Yeniden bağlanılıyor...", Color.DarkOrange);
                break;
            default:
                if (_kapanabilir)
                {
                    return;   // pencere kapaniyor
                }
                // Oturum bitti (cikis, atilma, yasak, baska yerden giris...): giris ekranina don.
                GirisEkraniniGoster(aciklama);
                if (aciklama is not null)
                {
                    MessageBox.Show(this, aciklama, GenelHataYakalayici.Baslik, MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                break;
        }
    });

    private void BildirimGerekli(MesajSatiri mesaj) => ArayuzdeCalistir(() => _bildirici.Bildir(this));

    // Olaylar normalde zaten arayuz is parcaciginda gelir; degilse oraya tasinir.
    private void ArayuzdeCalistir(Action islem)
    {
        if (IsDisposed)
        {
            return;
        }
        if (InvokeRequired)
        {
            BeginInvoke(islem);
            return;
        }
        islem();
    }
}
