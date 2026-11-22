using ChatApp.Client.Presentation;

namespace ChatApp.WinForms.Forms;

// Sifre degistirme penceresi: mevcut sifre + yeni sifre (iki kez).
public sealed class SifreDegistirFormu : Form
{
    private readonly SohbetYoneticisi _yonetici;
    private readonly TextBox _eskiKutusu = new() { Width = 240, UseSystemPasswordChar = true };
    private readonly TextBox _yeniKutusu = new() { Width = 240, UseSystemPasswordChar = true };
    private readonly TextBox _tekrarKutusu = new() { Width = 240, UseSystemPasswordChar = true };
    private readonly Button _tamamButonu = new() { Text = "Şifreyi değiştir", AutoSize = true };
    private readonly Label _durumYazisi = new() { AutoSize = true, MaximumSize = new Size(380, 0), ForeColor = Color.Firebrick };

    public SifreDegistirFormu(SohbetYoneticisi yonetici)
    {
        ArgumentNullException.ThrowIfNull(yonetici);
        _yonetici = yonetici;

        Text = "Şifre değiştir";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 9.75f);

        var tablo = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };
        SatirEkle(tablo, "Mevcut şifre:", _eskiKutusu);
        SatirEkle(tablo, "Yeni şifre:", _yeniKutusu);
        SatirEkle(tablo, "Yeni şifre (tekrar):", _tekrarKutusu);

        var iptal = new Button { Text = "İptal", AutoSize = true, DialogResult = DialogResult.Cancel };
        _tamamButonu.Click += async (_, _) => await DegistirAsync();
        var butonlar = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Width = 380 };
        butonlar.Controls.AddRange([iptal, _tamamButonu]);

        var dikey = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Padding = new Padding(12) };
        dikey.Controls.AddRange([tablo, _durumYazisi, butonlar]);
        Controls.Add(dikey);
        AcceptButton = _tamamButonu;
        CancelButton = iptal;
    }

    private async Task DegistirAsync()
    {
        if (_yeniKutusu.Text != _tekrarKutusu.Text)
        {
            _durumYazisi.Text = "Yeni şifreler aynı değil.";
            return;
        }

        _tamamButonu.Enabled = false;
        string? hata = await _yonetici.SifreDegistirAsync(_eskiKutusu.Text, _yeniKutusu.Text);
        if (IsDisposed)
        {
            return;
        }
        _tamamButonu.Enabled = true;
        if (hata is not null)
        {
            _durumYazisi.Text = hata;
            return;
        }

        MessageBox.Show(this, "Şifreniz değiştirildi.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
    }

    private static void SatirEkle(TableLayoutPanel tablo, string etiket, Control kontrol)
    {
        tablo.Controls.Add(new Label { Text = etiket, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 3) });
        tablo.Controls.Add(kontrol);
    }
}
