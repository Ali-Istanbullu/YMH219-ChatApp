using ChatApp.Server.Abstractions;
using ChatApp.Server.Gomulu;
using ChatApp.Server.Settings;
using ChatApp.Server.WinForms.Platform;

namespace ChatApp.Server.WinForms.Forms;

// ChatServer ilk kez acildiginda: veritabani nereden gelsin?
//  - "Hazir veritabanini kur" (onerilen): ChatServer kendi PostgreSQL'ini kurar; kullanici hicbir sey yazmaz.
//  - "Kendi PostgreSQL'imi kullanacagim": baglanti bilgileri elle girilir (VeritabaniAyarFormu).
public sealed class IlkKurulumFormu : Form
{
    private readonly AyarDeposu _ayarDeposu;
    private readonly IGunlukcu _gunlukcu;
    private readonly string? _binKlasoru;

    private readonly Button _hazirButonu;
    private readonly Button _kendiButonu;
    private readonly Label _durumYazisi;
    private readonly ProgressBar _ilerleme;

    public IlkKurulumFormu(AyarDeposu ayarDeposu, IGunlukcu gunlukcu, string? binKlasoru)
    {
        ArgumentNullException.ThrowIfNull(ayarDeposu);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        _ayarDeposu = ayarDeposu;
        _gunlukcu = gunlukcu;
        _binKlasoru = binKlasoru;

        Text = "ChatServer - İlk kurulum";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 9.75f);

        var aciklama = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(460, 0),
            Margin = new Padding(3, 3, 3, 12),
            Text = "ChatServer'ın hesapları ve mesajları saklayacağı bir veritabanı gerekiyor. Nasıl olsun?"
        };

        _hazirButonu = new Button { Text = "Hazır veritabanını kur (önerilen)", Width = 460, Height = 40, Font = new Font(Font, FontStyle.Bold) };
        var hazirAciklama = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(460, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(3, 2, 3, 12),
            Text = _binKlasoru is null
                ? "Kullanılamıyor: PostgreSQL dosyaları bulunamadı. ChatServer'ı kurulum dosyasıyla kurun ya da kendi PostgreSQL'inizi kullanın."
                : "ChatServer kendi PostgreSQL'ini kurar ve yönetir; hiçbir ayar ya da şifre girmeniz gerekmez. " +
                  "Veriler bu bilgisayarda saklanır, ChatServer kaldırılsa da silinmez."
        };
        _hazirButonu.Enabled = _binKlasoru is not null;
        _hazirButonu.Click += async (_, _) => await HazirKurAsync();

        _kendiButonu = new Button { Text = "Kendi PostgreSQL'imi kullanacağım", Width = 460, Height = 34 };
        var kendiAciklama = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(460, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(3, 2, 3, 12),
            Text = "Bilgisayarınızda zaten çalışan bir PostgreSQL varsa sunucu, port, kullanıcı ve şifresini siz girersiniz."
        };
        _kendiButonu.Click += (_, _) => KendiVeritabani();

        _ilerleme = new ProgressBar { Width = 460, Style = ProgressBarStyle.Marquee, Visible = false };
        _durumYazisi = new Label { AutoSize = true, MaximumSize = new Size(460, 0), ForeColor = Color.Firebrick };

        var dikey = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Padding = new Padding(16) };
        dikey.Controls.AddRange([aciklama, _hazirButonu, hazirAciklama, _kendiButonu, kendiAciklama, _ilerleme, _durumYazisi]);
        Controls.Add(dikey);
    }

    // Secilen/kurulan ayarlar (DialogResult.OK ise dolu; diske zaten yazildi).
    public SunucuAyarlari? Ayarlar { get; private set; }

    private async Task HazirKurAsync()
    {
        var postgres = new GomuluPostgres(_binKlasoru!, UygulamaYollari.VeritabaniKlasoru(),
            UygulamaYollari.PostgresLogDosyasi(), _gunlukcu);

        if (postgres.KuruluMu && !EskisiYedeklensinMi(postgres))
        {
            return;
        }

        Kilitle(true);
        _durumYazisi.ForeColor = Color.DimGray;
        _durumYazisi.Text = "Veritabanı kuruluyor, bu bir dakika kadar sürebilir...";
        try
        {
            if (postgres.KuruluMu)
            {
                postgres.EskisiniYedekle();
            }
            SunucuAyarlari ayarlar = await postgres.KurAsync();
            _ayarDeposu.Kaydet(ayarlar);
            Ayarlar = ayarlar;
            DialogResult = DialogResult.OK;   // pencere kapanir; veritabani calisir durumda
        }
        catch (GomuluVeritabaniHatasi hata)
        {
            _gunlukcu.Hata(hata, $"Hazir veritabani kurulamadi. {hata.Ayrinti}");
            await DurdurmayiDeneAsync(postgres);
            HataGoster("Veritabanı kurulamadı: " + hata.Message, hata.Ayrinti);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            _gunlukcu.Hata(hata, "Hazir veritabani kurulurken dosya hatasi.");
            await DurdurmayiDeneAsync(postgres);
            HataGoster("Veritabanı kurulamadı (dosya hatası): " + hata.Message, null);
        }
        finally
        {
            if (!IsDisposed)
            {
                Kilitle(false);
            }
        }
    }

    // Veritabani klasoru var ama ayarlar (sifre) yok: ayarlar silinmis olabilir. Eskisi silinmez, kenara konur.
    private bool EskisiYedeklensinMi(GomuluPostgres postgres) =>
        MessageBox.Show(this,
            "Daha önce kurulmuş bir veritabanı bulundu ama şifresi bu bilgisayarda kayıtlı değil " +
            "(ayar dosyası silinmiş olabilir).\n\n" +
            $"Eski veritabanı SİLİNMEDEN \"{Path.GetFileName(postgres.VeriKlasoru)}-yedek-<tarih>\" adıyla kenara konup " +
            "yeni, boş bir veritabanı kurulsun mu?",
            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;

    private void KendiVeritabani()
    {
        using var form = new VeritabaniAyarFormu(SunucuAyarlari.Varsayilan, _ayarDeposu, _gunlukcu);
        if (form.ShowDialog(this) == DialogResult.OK && form.Ayarlar is not null)
        {
            Ayarlar = form.Ayarlar;
            DialogResult = DialogResult.OK;
        }
    }

    private async Task DurdurmayiDeneAsync(GomuluPostgres postgres)
    {
        try
        {
            await postgres.DurdurAsync();
        }
        catch (GomuluVeritabaniHatasi hata)
        {
            _gunlukcu.Hata(hata, "Yarim kalan kurulumdan sonra veritabani durdurulamadi.");
        }
    }

    private void HataGoster(string mesaj, string? ayrinti)
    {
        _durumYazisi.ForeColor = Color.Firebrick;
        _durumYazisi.Text = mesaj + "\nAyrıntı log dosyasında (logs\\chatserver.log, logs\\postgres.log).";
        if (!string.IsNullOrWhiteSpace(ayrinti))
        {
            _durumYazisi.Text += "\n\n" + string.Join('\n', ayrinti.Split('\n').TakeLast(6));
        }
    }

    private void Kilitle(bool kilitli)
    {
        _hazirButonu.Enabled = !kilitli && _binKlasoru is not null;
        _kendiButonu.Enabled = !kilitli;
        _ilerleme.Visible = kilitli;
        UseWaitCursor = kilitli;
        ControlBox = !kilitli;   // kurulum yarida kesilmesin
    }
}
