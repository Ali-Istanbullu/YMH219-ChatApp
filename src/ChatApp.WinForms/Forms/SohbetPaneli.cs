using System.ComponentModel;
using ChatApp.Client.Presentation;
using ChatApp.Client.State;
using ChatApp.Protocol.Modeller;
using ChatApp.WinForms.Platform;

namespace ChatApp.WinForms.Forms;

// ================================================================================
//  SOHBET EKRANI (pasif gorunum)
//
//  Sol: benim adim ve ID'm, sohbet listesi (Genel + kisiler; yesil nokta = cevrimici, sayi = okunmamis).
//  Sag: acik sohbetin basligi, mesajlar, yazma kutusu. Mesaja sag tik: tekrar gonder / benden sil / herkesten sil.
//  Karar vermez: her sey SohbetYoneticisi'na iletilir, o "Degisti" deyince ekran yeniden cizilir.
// ================================================================================
public sealed class SohbetPaneli : UserControl
{
    private static readonly Color CevrimiciRenk = Color.ForestGreen;
    private static readonly Color CevrimdisiRenk = Color.Silver;
    private static readonly Color MaviTik = Color.DodgerBlue;
    private static readonly Color GriYazi = Color.DimGray;

    private readonly SohbetYoneticisi _yonetici;

    private readonly Label _benYazisi;
    private readonly ListBox _sohbetListesi;
    private readonly Label _baslik;
    private readonly Button _engelButonu;
    private readonly Button _sohbetSilButonu;
    private readonly Button _dahaEskiButonu;
    private readonly RichTextBox _mesajKutusu;
    private readonly TextBox _yaziKutusu;
    private readonly Button _gonderButonu;
    private readonly Label _uyariYazisi;
    private readonly ToolStripStatusLabel _baglantiYazisi;

    // Sag tik menusu ve hangi metin araliginin hangi mesaja ait oldugu.
    private readonly ContextMenuStrip _mesajMenusu = new();
    private readonly ToolStripMenuItem _tekrarGonderOgesi = new("Tekrar gönder");
    private readonly ToolStripMenuItem _bendenSilOgesi = new("Benden sil");
    private readonly ToolStripMenuItem _herkestenSilOgesi = new("Herkesten sil");
    private readonly List<(int Bas, int Son, MesajSatiri Satir)> _mesajAraliklari = [];
    private MesajSatiri? _sagTiklanan;

    private readonly Font _normalYazi = new("Segoe UI", 10f);
    private readonly Font _kalinYazi = new("Segoe UI", 10f, FontStyle.Bold);
    private readonly Font _egikYazi = new("Segoe UI", 10f, FontStyle.Italic);

    private bool _listeGuncelleniyor;
    private string _sonCizilen = string.Empty;

    public SohbetPaneli(SohbetYoneticisi yonetici)
    {
        ArgumentNullException.ThrowIfNull(yonetici);
        _yonetici = yonetici;
        Dock = DockStyle.Fill;

        // ---- Sol ----
        _benYazisi = new Label { Dock = DockStyle.Top, Height = 70, Padding = new Padding(8, 6, 8, 0), Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
        _sohbetListesi = new ListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 36,
            IntegralHeight = false,
            BorderStyle = BorderStyle.None
        };
        _sohbetListesi.DrawItem += SohbetCiz;
        _sohbetListesi.SelectedIndexChanged += async (_, _) => await SecilenSohbetiAcAsync();

        var solButonlar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        solButonlar.Controls.AddRange([
            Buton("Yeni sohbet (ID ile)...", async () => await YeniSohbetAsync(), 200),
            Buton("Şifre değiştir...", SifreDegistirAsync, 200),
            Buton("Çıkış yap", async () => await _yonetici.CikisYapAsync(), 200)
        ]);

        var sol = new Panel { Dock = DockStyle.Left, Width = 240 };
        sol.Controls.Add(_sohbetListesi);
        sol.Controls.Add(_benYazisi);
        sol.Controls.Add(solButonlar);

        // ---- Sag ----
        _baslik = new Label { AutoSize = true, Font = new Font("Segoe UI", 12f, FontStyle.Bold), Margin = new Padding(3, 6, 12, 3) };
        _engelButonu = Buton("Engelle", async () => await EngelleAsync());
        _sohbetSilButonu = Buton("Sohbeti sil", async () => await SohbetSilAsync());
        _dahaEskiButonu = Buton("Daha eski mesajlar", async () => await DahaEskiAsync());
        var sagUst = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
        sagUst.Controls.AddRange([_baslik, _engelButonu, _sohbetSilButonu, _dahaEskiButonu]);

        _mesajKutusu = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.White,
            BorderStyle = BorderStyle.None,
            DetectUrls = false,
            Font = new Font("Segoe UI", 10f)
        };
        _mesajMenusu.Items.AddRange([_tekrarGonderOgesi, _bendenSilOgesi, _herkestenSilOgesi]);
        _mesajMenusu.Opening += MesajMenusuAciliyor;
        _tekrarGonderOgesi.Click += async (_, _) => await TekrarGonderAsync();
        _bendenSilOgesi.Click += async (_, _) => await MesajSilAsync(herkesten: false);
        _herkestenSilOgesi.Click += async (_, _) => await MesajSilAsync(herkesten: true);
        _mesajKutusu.ContextMenuStrip = _mesajMenusu;

        _yaziKutusu = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical,
            MaxLength = GirdiSiniri,
            Font = new Font("Segoe UI", 10f),
            PlaceholderText = "Mesaj yazın (Enter: gönder, Shift+Enter: yeni satır). Silmek için mesaja sağ tıklayın."
        };
        _yaziKutusu.KeyDown += YaziKutusuTusBasildi;
        _gonderButonu = new Button { Text = "Gönder", Dock = DockStyle.Right, Width = 90 };
        _gonderButonu.Click += async (_, _) => await GonderAsync();
        _uyariYazisi = new Label { Dock = DockStyle.Bottom, AutoSize = false, Height = 22, ForeColor = Color.Firebrick, Padding = new Padding(4, 3, 4, 0) };
        var yazmaAlani = new Panel { Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(4) };
        yazmaAlani.Controls.Add(_yaziKutusu);
        yazmaAlani.Controls.Add(_gonderButonu);

        var sag = new Panel { Dock = DockStyle.Fill };
        sag.Controls.Add(_mesajKutusu);
        sag.Controls.Add(sagUst);
        sag.Controls.Add(yazmaAlani);
        sag.Controls.Add(_uyariYazisi);

        var ayirici = new Label { Dock = DockStyle.Left, Width = 1, BackColor = SystemColors.ControlDark };

        _baglantiYazisi = new ToolStripStatusLabel();
        var durumCubugu = new StatusStrip();
        durumCubugu.Items.Add(_baglantiYazisi);

        // Dock sirasi: once eklenen en ice yerlesir (sag dolgu en son kalan alani alir).
        Controls.Add(sag);
        Controls.Add(ayirici);
        Controls.Add(sol);
        Controls.Add(durumCubugu);

        _yonetici.Degisti += Ciz;
    }

    // Mesaj siniri 2000 karakter (sunucu da kontrol eder); kutu fazlasini yazdirmaz.
    private const int GirdiSiniri = 2000;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _yonetici.Degisti -= Ciz;
            _mesajMenusu.Dispose();
            _normalYazi.Dispose();
            _kalinYazi.Dispose();
            _egikYazi.Dispose();
        }
        base.Dispose(disposing);
    }

    public void Hazirla()
    {
        _sonCizilen = string.Empty;
        _uyariYazisi.Text = string.Empty;
        _yaziKutusu.Clear();
        BaglantiDurumunuGoster("Bağlı", GriYazi);
        Ciz();
        _yaziKutusu.Focus();
        if (FindForm() is { } form)
        {
            form.AcceptButton = null;   // Enter yazma kutusunda "gonder" demek
        }
    }

    public void BaglantiDurumunuGoster(string metin, Color renk)
    {
        _baglantiYazisi.Text = metin;
        _baglantiYazisi.ForeColor = renk;
    }

    // ============================ Cizim ============================

    private void Ciz()
    {
        if (IsDisposed || _yonetici.Durum is not { } durum)
        {
            return;
        }

        _benYazisi.Text = $"{durum.BenimAdim}\nID: {durum.BenimId}\n(bu numarayı paylaşın)";   // tek satirda sigmiyordu
        ListeyiCiz(durum);

        Sohbet? acik = _yonetici.AcikSohbet;
        if (acik is null)
        {
            return;
        }
        _baslik.Text = acik.GenelMi
            ? "Genel (herkes)"
            : $"{acik.Ad}  ·  {acik.KarsiId}  ·  {(acik.Engelli ? "engellendi" : acik.Cevrimici ? "çevrimiçi" : "çevrimdışı")}";
        _engelButonu.Visible = !acik.GenelMi;
        _sohbetSilButonu.Visible = !acik.GenelMi;
        _engelButonu.Text = acik.Engelli ? "Engeli kaldır" : "Engelle";
        _dahaEskiButonu.Visible = acik.DahaEskiVar;
        MesajlariCiz(acik, durum.BenimId);
    }

    private void ListeyiCiz(SohbetDurumu durum)
    {
        List<Sohbet> sohbetler = [durum.Genel, .. durum.OzelSohbetler];
        _listeGuncelleniyor = true;
        _sohbetListesi.BeginUpdate();
        try
        {
            _sohbetListesi.Items.Clear();
            foreach (Sohbet sohbet in sohbetler)
            {
                _sohbetListesi.Items.Add(sohbet);
            }
            _sohbetListesi.SelectedItem = _yonetici.AcikSohbet;
        }
        finally
        {
            _sohbetListesi.EndUpdate();
            _listeGuncelleniyor = false;
        }
    }

    private void SohbetCiz(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || _sohbetListesi.Items[e.Index] is not Sohbet sohbet)
        {
            return;
        }
        e.DrawBackground();
        bool secili = (e.State & DrawItemState.Selected) != 0;
        Color yaziRengi = secili ? SystemColors.HighlightText : SystemColors.ControlText;
        Rectangle alan = e.Bounds;
        Font font = e.Font ?? Font;

        // Sol: yesil/gri nokta (Genel icin #)
        string isaret = sohbet.GenelMi ? "#" : "●";
        Color isaretRengi = sohbet.GenelMi ? yaziRengi : sohbet.Cevrimici && !sohbet.Engelli ? CevrimiciRenk : CevrimdisiRenk;
        TextRenderer.DrawText(e.Graphics, isaret, font, new Rectangle(alan.X + 6, alan.Y, 18, alan.Height), isaretRengi,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

        // Orta: ad (+ engelli)
        string ad = sohbet.Engelli ? $"{sohbet.Ad} (engelli)" : sohbet.Ad;
        using var kalin = new Font(font, sohbet.Okunmamis > 0 ? FontStyle.Bold : FontStyle.Regular);
        TextRenderer.DrawText(e.Graphics, ad, kalin, new Rectangle(alan.X + 26, alan.Y, alan.Width - 70, alan.Height), yaziRengi,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);

        // Sag: okunmamis sayisi
        if (sohbet.Okunmamis > 0)
        {
            string sayi = sohbet.Okunmamis > 99 ? "99+" : sohbet.Okunmamis.ToString();
            var rozet = new Rectangle(alan.Right - 40, alan.Y + (alan.Height - 20) / 2, 34, 20);
            using var firca = new SolidBrush(MaviTik);
            e.Graphics.FillRectangle(firca, rozet);
            TextRenderer.DrawText(e.Graphics, sayi, kalin, rozet, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        e.DrawFocusRectangle();
    }

    private void MesajlariCiz(Sohbet sohbet, int benimId)
    {
        // Degisiklik yoksa yeniden cizme (titremesin, kaydirma yeri kaybolmasin).
        string imza = $"{sohbet.KarsiId}|" + string.Join(',', sohbet.Mesajlar.Select(m =>
            $"{m.Id}:{(int)m.Durum}:{m.Hata}:{m.BaglantiBekliyor}:{m.HerkestenSilindi}"));
        if (imza == _sonCizilen)
        {
            return;
        }
        _sonCizilen = imza;

        _mesajKutusu.SuspendLayout();
        _mesajKutusu.Clear();
        _mesajAraliklari.Clear();
        if (sohbet.Mesajlar.Count == 0)
        {
            Yaz(sohbet.GecmisYuklendi ? "Henüz mesaj yok. İlk mesajı siz yazın." : "Yükleniyor...", GriYazi, FontStyle.Italic, HorizontalAlignment.Center);
        }

        foreach (MesajSatiri mesaj in sohbet.Mesajlar)
        {
            bool benim = mesaj.GonderenId == benimId;
            HorizontalAlignment hiza = benim ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            int bas = _mesajKutusu.TextLength;

            string kim = benim ? "Siz" : mesaj.GonderenAd;
            Yaz($"{kim}  {Saat(mesaj.Zaman)}", GriYazi, FontStyle.Bold, hiza);
            if (mesaj.HerkestenSilindi)
            {
                Yaz(benim ? "⊘ Bu mesajı herkesten sildiniz" : "⊘ Bu mesaj silindi", GriYazi, FontStyle.Italic, hiza);
            }
            else
            {
                Yaz(mesaj.Metin, SystemColors.ControlText, FontStyle.Regular, hiza);
                if (benim)
                {
                    (string tik, Color renk) = Tik(mesaj, sohbet.GenelMi);
                    Yaz(tik, renk, FontStyle.Regular, hiza);
                }
            }
            _mesajAraliklari.Add((bas, _mesajKutusu.TextLength, mesaj));
            Yaz(string.Empty, GriYazi, FontStyle.Regular, hiza);
        }

        _mesajKutusu.SelectionStart = _mesajKutusu.TextLength;
        _mesajKutusu.ScrollToCaret();
        _mesajKutusu.ResumeLayout();
    }

    // ✓ sunucuya ulasti, ✓✓ iletildi, mavi ✓✓ okundu (Genel odada sadece ✓).
    private static (string Tik, Color Renk) Tik(MesajSatiri mesaj, bool genel)
    {
        if (mesaj.Hata is not null)
        {
            return ("⚠ Gönderilemedi: " + mesaj.Hata + "  (sağ tık → Tekrar gönder)", Color.Firebrick);
        }
        if (mesaj.BaglantiBekliyor)
        {
            return ("bağlantı bekleniyor… (bağlanınca kendiliğinden gönderilecek)", Color.DarkOrange);
        }
        if (mesaj.Gonderiliyor)
        {
            return ("gönderiliyor…", GriYazi);
        }
        if (genel)
        {
            return ("✓", GriYazi);
        }
        return mesaj.Durum switch
        {
            MesajDurumu.Okundu => ("✓✓ okundu", MaviTik),
            MesajDurumu.Iletildi => ("✓✓", GriYazi),
            _ => ("✓", GriYazi)
        };
    }

    private void Yaz(string metin, Color renk, FontStyle stil, HorizontalAlignment hiza)
    {
        _mesajKutusu.SelectionStart = _mesajKutusu.TextLength;
        _mesajKutusu.SelectionLength = 0;
        _mesajKutusu.SelectionAlignment = hiza;
        _mesajKutusu.SelectionColor = renk;
        _mesajKutusu.SelectionFont = stil switch
        {
            FontStyle.Bold => _kalinYazi,
            FontStyle.Italic => _egikYazi,
            _ => _normalYazi
        };
        _mesajKutusu.AppendText(metin + "\n");
    }

    private static string Saat(DateTimeOffset zaman)
    {
        DateTimeOffset yerel = zaman.ToLocalTime();
        return yerel.Date == DateTime.Today ? yerel.ToString("HH:mm") : yerel.ToString("dd.MM.yyyy HH:mm");
    }

    // ============================ Kullanici islemleri ============================

    private async Task SecilenSohbetiAcAsync()
    {
        if (_listeGuncelleniyor || _sohbetListesi.SelectedItem is not Sohbet sohbet || ReferenceEquals(sohbet, _yonetici.AcikSohbet))
        {
            return;
        }
        _uyariYazisi.Text = string.Empty;
        await _yonetici.SohbetAcAsync(sohbet);
        _yaziKutusu.Focus();
    }

    private void YaziKutusuTusBasildi(object? sender, KeyEventArgs e)
    {
        // Enter: gonder. Shift+Enter: yeni satir.
        if (e.KeyCode == Keys.Enter && !e.Shift)
        {
            e.SuppressKeyPress = true;
            _gonderButonu.PerformClick();
        }
    }

    private async Task GonderAsync()
    {
        if (_yaziKutusu.TextLength == 0)
        {
            return;
        }
        string? hata = await _yonetici.MesajGonderAsync(_yaziKutusu.Text);
        _uyariYazisi.Text = hata ?? string.Empty;
        if (hata is null)
        {
            _yaziKutusu.Clear();
        }
        _yaziKutusu.Focus();
    }

    private async Task YeniSohbetAsync()
    {
        string? id = MetinSorFormu.Sor(this, "Yeni sohbet", "Konuşmak istediğiniz kişinin 6 haneli ID'si:", 6);
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }
        _uyariYazisi.Text = await _yonetici.YeniSohbetAsync(id) ?? string.Empty;
    }

    private async Task EngelleAsync()
    {
        if (_yonetici.AcikSohbet is not { GenelMi: false } sohbet)
        {
            return;
        }
        bool engelle = !sohbet.Engelli;
        if (engelle && MessageBox.Show(FindForm(),
                $"{sohbet.Ad} engellensin mi?\n\nEngellediğiniz kişinin mesajları size gelmez, Genel'deki mesajlarını görmezsiniz " +
                "ve o sizi çevrimdışı görür. Engeli istediğiniz zaman kaldırabilirsiniz.",
                GenelHataYakalayici.Baslik, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }
        _uyariYazisi.Text = await _yonetici.EngelleAsync(engelle) ?? string.Empty;
    }

    // Sag tiklanan yerdeki mesaj bulunur; menude sadece o mesaj icin gecerli secenekler gorunur.
    private void MesajMenusuAciliyor(object? sender, CancelEventArgs e)
    {
        int indeks = _mesajKutusu.GetCharIndexFromPosition(_mesajKutusu.PointToClient(Cursor.Position));
        _sagTiklanan = null;
        foreach ((int bas, int son, MesajSatiri satir) in _mesajAraliklari)
        {
            if (indeks >= bas && indeks < son)
            {
                _sagTiklanan = satir;
                break;
            }
        }
        if (_sagTiklanan is not { } secili || _yonetici.Durum is not { } durum)
        {
            e.Cancel = true;
            return;
        }

        bool benim = secili.GonderenId == durum.BenimId;
        _tekrarGonderOgesi.Visible = secili.Hata is not null;
        _herkestenSilOgesi.Visible = benim && secili.Id is not null && !secili.HerkestenSilindi;
        _bendenSilOgesi.Visible = true;
    }

    private async Task TekrarGonderAsync()
    {
        if (_sagTiklanan is { } satir)
        {
            _uyariYazisi.Text = string.Empty;
            await _yonetici.TekrarGonderAsync(satir);
        }
    }

    private async Task MesajSilAsync(bool herkesten)
    {
        if (_sagTiklanan is not { } satir)
        {
            return;
        }
        if (herkesten && MessageBox.Show(FindForm(),
                "Bu mesaj herkesten silinsin mi?\n\nKarşı tarafta \"Bu mesaj silindi\" yazacak.",
                GenelHataYakalayici.Baslik, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }
        _uyariYazisi.Text = await _yonetici.MesajSilAsync(satir, herkesten) ?? string.Empty;
    }

    private async Task SohbetSilAsync()
    {
        if (_yonetici.AcikSohbet is not { GenelMi: false } sohbet)
        {
            return;
        }
        if (MessageBox.Show(FindForm(),
                $"{sohbet.Ad} ile olan sohbet sizden silinsin mi?\n\nSadece sizde silinir; {sohbet.Ad} görmeye devam eder. " +
                $"{sohbet.Ad} size yeniden yazarsa sohbet yeni mesajla geri gelir.",
                GenelHataYakalayici.Baslik, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }
        _uyariYazisi.Text = await _yonetici.SohbetSilAsync() ?? string.Empty;
    }

    private async Task DahaEskiAsync()
    {
        _dahaEskiButonu.Enabled = false;
        _uyariYazisi.Text = await _yonetici.DahaEskiYukleAsync() ?? string.Empty;
        _dahaEskiButonu.Enabled = true;
    }

    private Task SifreDegistirAsync()
    {
        using var form = new SifreDegistirFormu(_yonetici);
        form.ShowDialog(FindForm());
        return Task.CompletedTask;
    }

    // ============================ Yardimcilar ============================

    private static Button Buton(string metin, Func<Task> tiklaninca, int genislik = 0)
    {
        var buton = new Button { Text = metin, AutoSize = genislik == 0 };
        if (genislik > 0)
        {
            buton.Width = genislik;
        }
        buton.Click += async (_, _) => await tiklaninca();
        return buton;
    }
}
