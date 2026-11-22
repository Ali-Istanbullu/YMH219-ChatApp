using ChatApp.Server.Abstractions;
using ChatApp.Server.Data;
using ChatApp.Server.Postgres;
using ChatApp.Server.Settings;
using ChatApp.Server.WinForms.Platform;

namespace ChatApp.Server.WinForms.Forms;

// PostgreSQL baglanti bilgilerinin girildigi pencere.
// "Bağlantıyı test et ve kaydet": once gercekten baglanir (tablolari da olusturur), basarisizsa kaydetmez.
// Kontroller designer ile degil kodla olusturulur.
public sealed class VeritabaniAyarFormu : Form
{
    private readonly AyarDeposu _ayarDeposu;
    private readonly IGunlukcu _gunlukcu;
    private readonly int _dinlemePortu;

    private readonly TextBox _sunucuKutusu;
    private readonly NumericUpDown _portKutusu;
    private readonly TextBox _veritabaniKutusu;
    private readonly TextBox _kullaniciKutusu;
    private readonly TextBox _sifreKutusu;
    private readonly Button _kaydetButonu;
    private readonly Button _iptalButonu;
    private readonly Label _durumYazisi;

    public VeritabaniAyarFormu(SunucuAyarlari baslangic, AyarDeposu ayarDeposu, IGunlukcu gunlukcu)
    {
        ArgumentNullException.ThrowIfNull(baslangic);
        ArgumentNullException.ThrowIfNull(ayarDeposu);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        _ayarDeposu = ayarDeposu;
        _gunlukcu = gunlukcu;
        _dinlemePortu = baslangic.DinlemePortu;

        Text = "ChatServer - Veritabanı ayarları";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 9.75f);

        _sunucuKutusu = new TextBox { Text = baslangic.VeritabaniSunucusu, Width = 260 };
        _portKutusu = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = baslangic.VeritabaniPortu, Width = 100 };
        _veritabaniKutusu = new TextBox { Text = baslangic.VeritabaniAdi, Width = 260 };
        _kullaniciKutusu = new TextBox { Text = baslangic.VeritabaniKullanicisi, Width = 260 };
        _sifreKutusu = new TextBox { Text = baslangic.VeritabaniSifresi, Width = 260, UseSystemPasswordChar = true };

        var tablo = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(12) };
        SatirEkle(tablo, "Sunucu:", _sunucuKutusu);
        SatirEkle(tablo, "Port:", _portKutusu);
        SatirEkle(tablo, "Veritabanı:", _veritabaniKutusu);
        SatirEkle(tablo, "Kullanıcı:", _kullaniciKutusu);
        SatirEkle(tablo, "Şifre:", _sifreKutusu);

        _durumYazisi = new Label { AutoSize = true, MaximumSize = new Size(400, 0), ForeColor = Color.Firebrick, Padding = new Padding(12, 0, 12, 0) };

        _kaydetButonu = new Button { Text = "Bağlantıyı test et ve kaydet", AutoSize = true };
        _iptalButonu = new Button { Text = "İptal", AutoSize = true, DialogResult = DialogResult.Cancel };
        _kaydetButonu.Click += KaydetTiklandi;

        var butonlar = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(12) };
        butonlar.Controls.AddRange([_iptalButonu, _kaydetButonu]);

        var dikey = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false };
        dikey.Controls.AddRange([tablo, _durumYazisi, butonlar]);
        Controls.Add(dikey);

        AcceptButton = _kaydetButonu;
        CancelButton = _iptalButonu;
    }

    // Test edilip kaydedilen ayarlar (DialogResult.OK ise dolu).
    public SunucuAyarlari? Ayarlar { get; private set; }

    private static void SatirEkle(TableLayoutPanel tablo, string etiket, Control kontrol)
    {
        tablo.Controls.Add(new Label { Text = etiket, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 3) });
        tablo.Controls.Add(kontrol);
    }

    private async void KaydetTiklandi(object? sender, EventArgs e)
    {
        var ayarlar = new SunucuAyarlari(
            _sunucuKutusu.Text.Trim(),
            (int)_portKutusu.Value,
            _veritabaniKutusu.Text.Trim(),
            _kullaniciKutusu.Text.Trim(),
            _sifreKutusu.Text,
            _dinlemePortu);

        if (ayarlar.VeritabaniSunucusu.Length == 0 || ayarlar.VeritabaniAdi.Length == 0 || ayarlar.VeritabaniKullanicisi.Length == 0)
        {
            DurumGoster("Sunucu, veritabanı ve kullanıcı boş olamaz.");
            return;
        }

        KontrolleriKilitle(true);
        DurumGoster("Bağlanılıyor...", Color.DimGray);
        try
        {
            string dize = PostgresVeritabani.BaglantiDizesi(ayarlar.VeritabaniSunucusu, ayarlar.VeritabaniPortu,
                ayarlar.VeritabaniAdi, ayarlar.VeritabaniKullanicisi, ayarlar.VeritabaniSifresi);
            await using (var veritabani = new PostgresVeritabani(dize))
            {
                await veritabani.HazirlaAsync();
            }

            _ayarDeposu.Kaydet(ayarlar);
            _gunlukcu.Bilgi($"Veritabani ayarlari kaydedildi ({ayarlar.VeritabaniSunucusu}:{ayarlar.VeritabaniPortu}/{ayarlar.VeritabaniAdi}).");
            Ayarlar = ayarlar;
            DialogResult = DialogResult.OK;   // pencere kapanir
        }
        catch (DepoHatasi hata)
        {
            // Sifre yanlis, PostgreSQL kapali, adres yanlis... PostgreSQL'in kendi aciklamasi gosterilir.
            _gunlukcu.Hata(hata, "Veritabani baglanti testi basarisiz.");
            DurumGoster("Bağlanılamadı: " + (hata.InnerException?.Message ?? hata.Message));
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            _gunlukcu.Hata(hata, "Ayar dosyasi yazilamadi.");
            DurumGoster("Bağlantı başarılı ama ayar dosyası yazılamadı: " + hata.Message);
        }
        finally
        {
            if (!IsDisposed)
            {
                KontrolleriKilitle(false);
            }
        }
    }

    private void KontrolleriKilitle(bool kilitli)
    {
        _kaydetButonu.Enabled = !kilitli;
        _iptalButonu.Enabled = !kilitli;
        UseWaitCursor = kilitli;
    }

    private void DurumGoster(string metin, Color? renk = null)
    {
        _durumYazisi.ForeColor = renk ?? Color.Firebrick;
        _durumYazisi.Text = metin;
    }
}
