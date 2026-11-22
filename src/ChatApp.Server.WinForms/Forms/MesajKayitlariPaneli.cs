using ChatApp.Server.Abstractions;
using ChatApp.Server.Data;
using ChatApp.Server.Services;
using ChatApp.Server.WinForms.Platform;

namespace ChatApp.Server.WinForms.Forms;

// ================================================================================
//  "MESAJ KAYITLARI" SEKMESI (Ali karari: salt okunur)
//
//  Kullanici (ID ya da kullanici adi) -> konustugu kisiler -> secilen sohbetin TUM mesajlari.
//  Kullanicilarin sildigi/gizledigi mesajlar da gorunur ve isaretlidir: yasak sebebine kanit.
//  Genel oda da ayni sekilde. Buradan hicbir sey degistirilemez (sadece "Bu kullaniciyi yasakla").
// ================================================================================
public sealed class MesajKayitlariPaneli : UserControl
{
    private readonly YonetimServisi _yonetim;
    private readonly IGunlukcu _gunlukcu;

    private readonly TextBox _aramaKutusu;
    private readonly Label _kullaniciYazisi;
    private readonly ListView _sohbetListesi;
    private readonly ListView _mesajListesi;
    private readonly TextBox _mesajMetni;
    private readonly Button _dahaEskiButonu;
    private readonly Button _yasaklaButonu;

    private KullaniciKaydi? _kullanici;
    private int? _karsiId;        // null: Genel oda
    private bool _genelGosteriliyor;
    private string _karsiAd = string.Empty;
    private readonly List<YonetimMesajKaydi> _kayitlar = [];

    public MesajKayitlariPaneli(YonetimServisi yonetim, IGunlukcu gunlukcu)
    {
        ArgumentNullException.ThrowIfNull(yonetim);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        _yonetim = yonetim;
        _gunlukcu = gunlukcu;
        Dock = DockStyle.Fill;

        // ---- Ust: arama ----
        _aramaKutusu = new TextBox { Width = 180, Margin = new Padding(3, 5, 3, 3) };
        Button gosterButonu = Buton("Göster", async () => await KullaniciGosterAsync());
        Button genelButonu = Buton("Genel odayı göster", async () => await GenelGosterAsync());
        _yasaklaButonu = Buton("Bu kullanıcıyı yasakla...", async () => await YasaklaAsync());
        _yasaklaButonu.Enabled = false;
        _kullaniciYazisi = new Label { AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(12, 8, 3, 3) };
        var ust = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
        ust.Controls.AddRange([
            new Label { Text = "Kullanıcı (ID ya da kullanıcı adı):", AutoSize = true, Margin = new Padding(3, 8, 3, 3) },
            _aramaKutusu, gosterButonu, genelButonu, _yasaklaButonu, _kullaniciYazisi
        ]);
        _aramaKutusu.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await KullaniciGosterAsync();
            }
        };

        // ---- Sol: konustugu kisiler ----
        _sohbetListesi = Liste(("Kişi", 130), ("ID", 70), ("Mesaj", 55), ("Son mesaj", 115));
        _sohbetListesi.Dock = DockStyle.Left;
        _sohbetListesi.Width = 380;
        _sohbetListesi.SelectedIndexChanged += async (_, _) => await SecilenSohbetiGosterAsync();

        // ---- Sag: mesajlar ----
        _mesajListesi = Liste(("Zaman", 120), ("Gönderen", 110), ("Mesaj", 330), ("Durum", 80), ("Silme", 200));
        _mesajListesi.Dock = DockStyle.Fill;
        _mesajListesi.SelectedIndexChanged += (_, _) => SecilenMesajiGoster();
        _mesajMetni = new TextBox { Dock = DockStyle.Bottom, Height = 70, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
        _dahaEskiButonu = Buton("Daha eski mesajlar", async () => await DahaEskiAsync());
        _dahaEskiButonu.Dock = DockStyle.Top;
        _dahaEskiButonu.Visible = false;

        var sag = new Panel { Dock = DockStyle.Fill };
        sag.Controls.Add(_mesajListesi);
        sag.Controls.Add(_dahaEskiButonu);
        sag.Controls.Add(_mesajMetni);

        Controls.Add(sag);
        Controls.Add(_sohbetListesi);
        Controls.Add(ust);
    }

    // ============================ Islemler ============================

    private async Task KullaniciGosterAsync()
    {
        string aranan = _aramaKutusu.Text.Trim();
        if (aranan.Length == 0)
        {
            return;
        }
        await VeritabaniIslemiAsync(async () =>
        {
            _kullanici = await _yonetim.KullaniciBulAsync(aranan);
            _sohbetListesi.Items.Clear();
            MesajlariTemizle();
            _yasaklaButonu.Enabled = _kullanici is not null;
            if (_kullanici is null)
            {
                _kullaniciYazisi.Text = "Bu ID ya da kullanıcı adıyla hesap yok.";
                return;
            }

            IReadOnlyList<YonetimSohbetOzeti> sohbetler = await _yonetim.KullaniciSohbetleriAsync(_kullanici.Id);
            _kullaniciYazisi.Text = $"{_kullanici.GorunenAd} (@{_kullanici.KullaniciAdi}, {_kullanici.Id})" +
                (_kullanici.Yasakli ? "  ·  YASAKLI" : "") + $"  ·  {sohbetler.Count} özel sohbet";
            foreach (YonetimSohbetOzeti sohbet in sohbetler)
            {
                _sohbetListesi.Items.Add(new ListViewItem(new[] { sohbet.KarsiAd, sohbet.KarsiId.ToString(),
                    sohbet.MesajSayisi.ToString(), Saat(sohbet.SonMesajZamani) }) { Tag = sohbet });
            }
        });
    }

    private async Task GenelGosterAsync()
    {
        _sohbetListesi.SelectedItems.Clear();
        _genelGosteriliyor = true;
        _karsiId = null;
        _karsiAd = "Genel";
        await SayfaYukleAsync(oncekiMesajId: null);
    }

    private async Task SecilenSohbetiGosterAsync()
    {
        if (_kullanici is null || _sohbetListesi.SelectedItems.Count == 0
            || _sohbetListesi.SelectedItems[0].Tag is not YonetimSohbetOzeti sohbet)
        {
            return;
        }
        _genelGosteriliyor = false;
        _karsiId = sohbet.KarsiId;
        _karsiAd = sohbet.KarsiAd;
        await SayfaYukleAsync(oncekiMesajId: null);
    }

    private async Task DahaEskiAsync()
    {
        if (_kayitlar.Count > 0)
        {
            await SayfaYukleAsync(_kayitlar[0].Mesaj.Id);
        }
    }

    // oncekiMesajId null: en yeni sayfa (liste bastan doldurulur); degilse daha eskiler basa eklenir.
    private async Task SayfaYukleAsync(long? oncekiMesajId)
    {
        await VeritabaniIslemiAsync(async () =>
        {
            IReadOnlyList<YonetimMesajKaydi> sayfa = _genelGosteriliyor
                ? await _yonetim.GenelKayitlarAsync(oncekiMesajId)
                : await _yonetim.OzelKayitlarAsync(_kullanici!.Id, _karsiId!.Value, oncekiMesajId);

            if (oncekiMesajId is null)
            {
                MesajlariTemizle();
            }
            _kayitlar.InsertRange(0, sayfa);
            _dahaEskiButonu.Visible = sayfa.Count == YonetimServisi.KayitSayfaBoyutu;

            _mesajListesi.BeginUpdate();
            _mesajListesi.Items.Clear();
            foreach (YonetimMesajKaydi kayit in _kayitlar)
            {
                _mesajListesi.Items.Add(Satir(kayit));
            }
            _mesajListesi.EndUpdate();
            if (oncekiMesajId is null && _mesajListesi.Items.Count > 0)
            {
                _mesajListesi.EnsureVisible(_mesajListesi.Items.Count - 1);   // en yeni en altta
            }
        });
    }

    private async Task YasaklaAsync()
    {
        if (_kullanici is not { } kullanici)
        {
            return;
        }
        string? sebep = MetinSorFormu.Sor(this, "Yasakla", $"{kullanici.GorunenAd} ({kullanici.Id}) yasaklanacak. Sebep:", 200);
        if (sebep is null)
        {
            return;
        }
        await VeritabaniIslemiAsync(async () =>
        {
            await _yonetim.YasaklaAsync(kullanici.Id, sebep);
            await KullaniciGosterAsync();
        });
    }

    private void SecilenMesajiGoster()
    {
        _mesajMetni.Text = _mesajListesi.SelectedItems.Count > 0 && _mesajListesi.SelectedItems[0].Tag is YonetimMesajKaydi kayit
            ? kayit.Mesaj.Metin
            : string.Empty;
    }

    // ============================ Yardimcilar ============================

    private ListViewItem Satir(YonetimMesajKaydi kayit)
    {
        KayitliMesaj m = kayit.Mesaj;
        string durum = m.AliciId is null ? "-" : m.Durum.ToString();
        string metin = m.Metin.ReplaceLineEndings(" ⏎ ");

        var silme = new List<string>();
        if (m.HerkestenSilindi)
        {
            silme.Add("gönderen herkesten sildi");
        }
        // Genel odada kimin kendinden gizledigi onemsiz (Ali karari): sadece "herkesten sildi" gosterilir.
        // Ozel sohbette kimin sildigi gosterilir. Mesajin kendisi her durumda listede.
        if (m.AliciId is not null)
        {
            foreach (int gizleyen in kayit.Gizleyenler)
            {
                silme.Add($"{Ad(gizleyen)} kendinden sildi");
            }
        }

        var satir = new ListViewItem(new[] { Saat(m.GonderimZamani), m.GonderenAd, metin, durum, string.Join(", ", silme) }) { Tag = kayit };
        if (silme.Count > 0)
        {
            satir.ForeColor = Color.Firebrick;   // silinmis/gizlenmis: dikkat ceksin
        }
        return satir;
    }

    private string Ad(int kullaniciId) =>
        kullaniciId == _kullanici?.Id ? _kullanici.GorunenAd
        : kullaniciId == _karsiId ? _karsiAd
        : kullaniciId.ToString();

    private void MesajlariTemizle()
    {
        _kayitlar.Clear();
        _mesajListesi.Items.Clear();
        _mesajMetni.Clear();
        _dahaEskiButonu.Visible = false;
    }

    private async Task VeritabaniIslemiAsync(Func<Task> islem)
    {
        try
        {
            await islem();
        }
        catch (DepoHatasi hata)
        {
            _gunlukcu.Hata(hata, "Mesaj kayitlari okunamadi (veritabani).");
            MessageBox.Show(this, "Veritabanına ulaşılamadı.\n" + DepoHatasiAciklayici.Acikla(hata),
                GenelHataYakalayici.Baslik, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string Saat(DateTimeOffset zaman) => zaman.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss");

    private static ListView Liste(params (string Baslik, int Genislik)[] sutunlar)
    {
        var liste = new ListView { View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false };
        foreach ((string baslik, int genislik) in sutunlar)
        {
            liste.Columns.Add(baslik, genislik);
        }
        return liste;
    }

    private static Button Buton(string metin, Func<Task> tiklaninca)
    {
        var buton = new Button { Text = metin, AutoSize = true };
        buton.Click += async (_, _) => await tiklaninca();
        return buton;
    }
}
