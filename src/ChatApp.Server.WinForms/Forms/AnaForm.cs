using System.Net.Sockets;
using ChatApp.Server.Abstractions;
using ChatApp.Server.Data;
using ChatApp.Server.Postgres;
using ChatApp.Server.Services;
using ChatApp.Server.Sessions;
using ChatApp.Server.Settings;
using ChatApp.Server.WinForms.Platform;

namespace ChatApp.Server.WinForms.Forms;

// ================================================================================
//  SUNUCU PENCERESI
//
//  Ust satir: port, Başlat/Durdur, veritabani ayarlari, sertifika parmak izi.
//  Sekmeler: Bağlı kullanıcılar (At/Yasakla), Yasaklılar, Davet kodları, Mesaj kayıtları, Olay günlüğü.
//  Alt satir: hesap sayisi, mesaj sayisi, mesaj tablosunun boyutu.
//
//  Is mantigi yok: her sey YonetimServisi / SunucuCalistirici'ya iletilir.
//  Beklenen hatalar (veritabanina ulasilamadi, port dolu, dosya) yakalanip sebebiyle gosterilir;
//  digerleri (bug) yakalanmaz, genel hata yakalayiciya duser.
// ================================================================================
public sealed class AnaForm : Form
{
    private static readonly TimeSpan IstatistikAraligi = TimeSpan.FromSeconds(30);

    private readonly SunucuCalistirici _calistirici;
    private readonly PostgresVeritabani _veritabani;
    private readonly YonetimServisi _yonetim;
    private readonly OturumYoneticisi _oturumlar;
    private readonly SerilogGunlukcu _gunlukcu;
    private readonly AyarDeposu _ayarDeposu;
    private SunucuAyarlari _ayarlar;

    // ---- Kontroller ----
    private readonly NumericUpDown _portKutusu;
    private readonly Button _baslatButonu;
    private readonly Button _durdurButonu;
    private readonly Label _durumYazisi;
    private readonly TextBox _parmakIziKutusu;
    private readonly ListView _bagliListesi;
    private readonly ListView _yasakliListesi;
    private readonly ListView _davetListesi;
    private readonly ListBox _gunlukListesi;
    private readonly ToolStripStatusLabel _istatistikYazisi;
    private readonly System.Windows.Forms.Timer _istatistikZamanlayici;

    private bool _kapanabilir;

    public AnaForm(SunucuCalistirici calistirici, PostgresVeritabani veritabani, YonetimServisi yonetim,
        OturumYoneticisi oturumlar, SerilogGunlukcu gunlukcu, AyarDeposu ayarDeposu, SunucuAyarlari ayarlar)
    {
        ArgumentNullException.ThrowIfNull(calistirici);
        ArgumentNullException.ThrowIfNull(veritabani);
        ArgumentNullException.ThrowIfNull(yonetim);
        ArgumentNullException.ThrowIfNull(oturumlar);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        ArgumentNullException.ThrowIfNull(ayarDeposu);
        ArgumentNullException.ThrowIfNull(ayarlar);
        _calistirici = calistirici;
        _veritabani = veritabani;
        _yonetim = yonetim;
        _oturumlar = oturumlar;
        _gunlukcu = gunlukcu;
        _ayarDeposu = ayarDeposu;
        _ayarlar = ayarlar;

        Text = "ChatServer";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(900, 560);
        MinimumSize = new Size(720, 420);
        Font = new Font("Segoe UI", 9.75f);

        // ---- Ust satir ----
        _portKutusu = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = ayarlar.DinlemePortu, Width = 80, Margin = new Padding(3, 5, 12, 3) };
        _baslatButonu = Buton("Başlat", BaslatTiklandi);
        _durdurButonu = Buton("Durdur", DurdurTiklandi);
        Button ayarButonu = Buton("Veritabanı ayarları...", AyarlarTiklandi);
        _durumYazisi = new Label { AutoSize = true, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Margin = new Padding(12, 7, 3, 3) };
        var ustSatir = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 8, 8, 0) };
        ustSatir.Controls.AddRange([Etiket("Port:"), _portKutusu, _baslatButonu, _durdurButonu, ayarButonu, _durumYazisi]);

        _parmakIziKutusu = new TextBox { ReadOnly = true, Width = 560, Margin = new Padding(3, 4, 3, 3) };
        Button kopyalaButonu = Buton("Kopyala", (_, _) => PanoyaKopyala(_parmakIziKutusu.Text));
        var ikinciSatir = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 0, 8, 4) };
        ikinciSatir.Controls.AddRange([Etiket("Sertifika parmak izi:"), _parmakIziKutusu, kopyalaButonu]);

        // ---- Sekme: Bagli kullanicilar ----
        _bagliListesi = Liste(("ID", 80), ("Görünen ad", 220), ("IP", 140), ("Bağlandı", 160));
        var bagliSekmesi = Sekme("Bağlı kullanıcılar", _bagliListesi,
            Buton("At", AtTiklandi), Buton("Yasakla", BagliYasaklaTiklandi), YenileButonu());

        // ---- Sekme: Yasaklilar ----
        _yasakliListesi = Liste(("ID", 80), ("Kullanıcı adı", 140), ("Görünen ad", 160), ("Sebep", 260), ("Tarih", 140));
        var yasakliSekmesi = Sekme("Yasaklılar", _yasakliListesi,
            Buton("Yasağı kaldır", YasagiKaldirTiklandi), Buton("ID / kullanıcı adıyla yasakla...", AdlaYasaklaTiklandi),
            YenileButonu());

        // ---- Sekme: Davet kodlari ----
        _davetListesi = Liste(("Kod", 120), ("Oluşturuldu", 150), ("Kullanan", 220), ("Kullanıldı", 150));
        var davetSekmesi = Sekme("Davet kodları", _davetListesi,
            Buton("Yeni kod üret", DavetUretTiklandi), Buton("Seçili kodu kopyala", DavetKopyalaTiklandi), YenileButonu());

        // ---- Sekme: Olay gunlugu ----
        _gunlukListesi = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
        var gunlukSekmesi = new TabPage("Olay günlüğü");
        gunlukSekmesi.Controls.Add(_gunlukListesi);

        // ---- Sekme: Mesaj kayitlari (salt okunur; silinenler dahil) ----
        var kayitSekmesi = new TabPage("Mesaj kayıtları");
        var kayitPaneli = new MesajKayitlariPaneli(yonetim, gunlukcu);
        kayitPaneli.KullaniciYasaklandi += async () => await ListeleriYenileAsync();   // Yasaklılar sekmesi guncel kalsin
        kayitSekmesi.Controls.Add(kayitPaneli);

        var sekmeler = new TabControl { Dock = DockStyle.Fill };
        sekmeler.TabPages.AddRange([bagliSekmesi, yasakliSekmesi, davetSekmesi, kayitSekmesi, gunlukSekmesi]);

        // ---- Alt satir ----
        _istatistikYazisi = new ToolStripStatusLabel();
        var durumCubugu = new StatusStrip();
        durumCubugu.Items.Add(_istatistikYazisi);

        // Dock sirasi: once eklenen en alta/ice yerlesir.
        Controls.Add(sekmeler);
        Controls.Add(ikinciSatir);
        Controls.Add(ustSatir);
        Controls.Add(durumCubugu);

        _istatistikZamanlayici = new System.Windows.Forms.Timer { Interval = (int)IstatistikAraligi.TotalMilliseconds };
        _istatistikZamanlayici.Tick += async (_, _) => await IstatistigiYenileAsync();

        foreach (string satir in _gunlukcu.SonSatirlar())
        {
            _gunlukListesi.Items.Add(satir);
        }
        _gunlukcu.SatirYazildi += GunlukSatiriGeldi;
        _oturumlar.Degisti += OturumlarDegisti;

        DurumuGoster();
    }

    // Kullanici veritabani ayarlarini degistirdi: Program uygulamayi yeniden baslatir.
    public bool YenidenBaslatilsin { get; private set; }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _ayarDeposu.SilindiyseYenidenOlustur(_ayarlar);

        // Tablolar listeler okunmadan ONCE olusturulur (yeni kurulan veritabani bos gelir).
        // Veritabanina ulasilamazsa alt satirda yazar; Başlat'a basinca tekrar denenir.
        await VeritabaniIslemiAsync(_veritabani.HazirlaAsync, sessiz: true);
        _istatistikZamanlayici.Start();
        await ListeleriYenileAsync();
    }

    // Sunucu calisiyorsa once duzgunce durdurulur (istemcilere "sunucu kapandı" gider), sonra kapanir.
    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (_kapanabilir || !_calistirici.CalisiyorMu)
        {
            AboneliklerdenCik();
            return;
        }

        e.Cancel = true;
        Enabled = false;
        await _calistirici.DurdurAsync();
        _kapanabilir = true;
        Close();
    }

    private void AboneliklerdenCik()
    {
        _ayarDeposu.SilindiyseYenidenOlustur(_ayarlar);
        _istatistikZamanlayici.Stop();
        _istatistikZamanlayici.Dispose();
        _gunlukcu.SatirYazildi -= GunlukSatiriGeldi;
        _oturumlar.Degisti -= OturumlarDegisti;
    }

    // ============================ Baslat / Durdur ============================

    private async void BaslatTiklandi(object? sender, EventArgs e)
    {
        int port = (int)_portKutusu.Value;
        _baslatButonu.Enabled = false;
        try
        {
            await _veritabani.HazirlaAsync();   // PostgreSQL acik mi? Tablolar var mi?
            _calistirici.Baslat(port);
        }
        catch (DepoHatasi hata)
        {
            Uyar(hata, "Sunucu başlatılamadı: veritabanına ulaşılamadı.\n" + DepoHatasiAciklayici.Acikla(hata) +
                "\n\nAyarları \"Veritabanı ayarları...\" düğmesinden değiştirebilirsiniz.");
        }
        catch (SocketException hata)
        {
            Uyar(hata, $"Sunucu başlatılamadı: {port} portu kullanımda ya da açılamadı.\n{hata.Message}");
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            Uyar(hata, "Sunucu başlatılamadı: sertifika dosyası okunamadı/yazılamadı.\n" + hata.Message);
        }

        if (_calistirici.CalisiyorMu && port != _ayarlar.DinlemePortu)
        {
            PortuKaydet(port);
        }
        _ayarDeposu.SilindiyseYenidenOlustur(_ayarlar);
        DurumuGoster();
        await ListeleriYenileAsync();
    }

    private async void DurdurTiklandi(object? sender, EventArgs e)
    {
        _durdurButonu.Enabled = false;
        await _calistirici.DurdurAsync();
        DurumuGoster();
    }

    private void PortuKaydet(int port)
    {
        try
        {
            _ayarlar = _ayarlar with { DinlemePortu = port };
            _ayarDeposu.Kaydet(_ayarlar);
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            // Sunucu calisiyor; sadece port bir dahaki acilista hatirlanmaz.
            _gunlukcu.Hata(hata, "Port ayar dosyasina yazilamadi.");
        }
    }

    private void DurumuGoster()
    {
        bool calisiyor = _calistirici.CalisiyorMu;
        _baslatButonu.Enabled = !calisiyor;
        _durdurButonu.Enabled = calisiyor;
        _portKutusu.Enabled = !calisiyor;
        _durumYazisi.Text = !calisiyor ? "● KAPALI"
            : _calistirici.OtomatikBulmaAcikMi ? "● ÇALIŞIYOR"
            : "● ÇALIŞIYOR (otomatik bulma kapalı, log'a bakın)";
        _durumYazisi.ForeColor = calisiyor ? Color.ForestGreen : Color.Firebrick;
        _parmakIziKutusu.Text = _calistirici.ParmakIzi ?? "(sunucu başlayınca görünür)";
    }

    private void AyarlarTiklandi(object? sender, EventArgs e)
    {
        using var form = new VeritabaniAyarFormu(_ayarlar, _ayarDeposu, _gunlukcu);
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        MessageBox.Show(this, "Ayarlar kaydedildi. ChatServer yeni ayarlarla yeniden başlatılacak.",
            GenelHataYakalayici.Baslik, MessageBoxButtons.OK, MessageBoxIcon.Information);
        YenidenBaslatilsin = true;
        Close();
    }

    // ============================ Yonetim ============================

    private async void AtTiklandi(object? sender, EventArgs e)
    {
        if (SeciliId(_bagliListesi) is not int id)
        {
            return;
        }
        await _yonetim.AtAsync(id);   // cevrimdisi olduysa false; liste zaten yenilenir
    }

    private async void BagliYasaklaTiklandi(object? sender, EventArgs e)
    {
        if (SeciliId(_bagliListesi) is int id)
        {
            await YasaklaAsync(id, _bagliListesi.SelectedItems[0].SubItems[1].Text);
        }
    }

    private async void AdlaYasaklaTiklandi(object? sender, EventArgs e)
    {
        string? aranan = MetinSorFormu.Sor(this, "Yasakla", "Kullanıcının 6 haneli ID'si ya da kullanıcı adı:", 40);
        if (string.IsNullOrWhiteSpace(aranan))
        {
            return;
        }
        string arananMetin = aranan.Trim();

        await VeritabaniIslemiAsync(async () =>
        {
            KullaniciKaydi? kullanici = await _yonetim.KullaniciBulAsync(arananMetin);
            if (kullanici is null)
            {
                MessageBox.Show(this, "Bu ID ya da kullanıcı adıyla hesap yok.", GenelHataYakalayici.Baslik,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            await YasaklaAsync(kullanici.Id, kullanici.GorunenAd);
        });
    }

    private async Task YasaklaAsync(int id, string ad)
    {
        string? sebep = MetinSorFormu.Sor(this, "Yasakla", $"{ad} ({id}) yasaklanacak. Sebep (boş bırakılabilir):", 200);
        if (sebep is null)
        {
            return;   // iptal
        }
        await VeritabaniIslemiAsync(async () =>
        {
            await _yonetim.YasaklaAsync(id, sebep);
            await ListeleriYenileAsync();
        });
    }

    private async void YasagiKaldirTiklandi(object? sender, EventArgs e)
    {
        if (SeciliId(_yasakliListesi) is not int id)
        {
            return;
        }
        await VeritabaniIslemiAsync(async () =>
        {
            await _yonetim.YasagiKaldirAsync(id);
            await ListeleriYenileAsync();
        });
    }

    private async void DavetUretTiklandi(object? sender, EventArgs e)
    {
        await VeritabaniIslemiAsync(async () =>
        {
            string kod = await _yonetim.DavetKoduUretAsync();
            PanoyaKopyala(kod);
            await ListeleriYenileAsync();
            MessageBox.Show(this, $"Yeni davet kodu: {kod}\n(Panoya kopyalandı. Tek kullanımlıktır.)",
                GenelHataYakalayici.Baslik, MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
    }

    private void DavetKopyalaTiklandi(object? sender, EventArgs e)
    {
        if (_davetListesi.SelectedItems.Count > 0)
        {
            PanoyaKopyala(_davetListesi.SelectedItems[0].Text);
        }
    }

    // ============================ Listeler ============================

    private async Task ListeleriYenileAsync()
    {
        BagliListesiniYenile();
        await VeritabaniIslemiAsync(async () =>
        {
            IReadOnlyList<KullaniciKaydi> yasaklilar = await _yonetim.YasaklilarAsync();
            IReadOnlyList<DavetKoduKaydi> davetler = await _yonetim.DavetKodlariAsync();

            ListeyiDoldur(_yasakliListesi, yasaklilar.Select(k => new[]
            {
                k.Id.ToString(), k.KullaniciAdi, k.GorunenAd, k.YasakSebebi ?? "-", Saat(k.YasakZamani)
            }));
            ListeyiDoldur(_davetListesi, davetler.Select(d => new[]
            {
                d.Kod, Saat(d.OlusturmaZamani),
                d.KullananId is int kullanan ? $"{d.KullananAdi} ({kullanan})" : "— kullanılmadı —",
                Saat(d.KullanilmaZamani)
            }));
        }, sessiz: true);
        await IstatistigiYenileAsync();
    }

    private void BagliListesiniYenile()
    {
        IReadOnlyList<IIstemciOturumu> girisYapanlar = _oturumlar.GirisYapanlar();
        ListeyiDoldur(_bagliListesi, girisYapanlar
            .OrderBy(o => o.GorunenAd)
            .Select(o => new[] { o.KullaniciId?.ToString() ?? "", o.GorunenAd ?? "", o.IpAdresi, Saat(o.BaglanmaZamani) }));

        // Sekme basliginda: giris yapan / toplam baglanti (giris ekraninda bekleyenler dahil).
        if (_bagliListesi.Parent is TabPage sekme)
        {
            sekme.Text = $"Bağlı kullanıcılar ({girisYapanlar.Count} / {_oturumlar.TumBaglantilar().Count} bağlantı)";
        }
    }

    private async Task IstatistigiYenileAsync()
    {
        await VeritabaniIslemiAsync(async () =>
        {
            SunucuIstatistigi s = await _yonetim.IstatistikAsync();
            _istatistikYazisi.Text = $"Hesap: {s.HesapSayisi}    Mesaj: {s.MesajSayisi}    " +
                $"Mesaj tablosu: {s.MesajTablosuBayt / 1024.0 / 1024.0:0.0} MB (500 MB'ı geçince en eskiler silinir)";
        }, sessiz: true);
    }

    // Veritabanina ulasilamazsa: sessiz=false ise kullaniciya sebebiyle soylenir; her durumda loga yazilir.
    private async Task VeritabaniIslemiAsync(Func<Task> islem, bool sessiz = false)
    {
        try
        {
            await islem();
        }
        catch (DepoHatasi hata)
        {
            if (sessiz)
            {
                _gunlukcu.Hata(hata, "Pencere verileri okunamadi (veritabani).");
                _istatistikYazisi.Text = "Veritabanına ulaşılamıyor: " + DepoHatasiAciklayici.Acikla(hata).Split('\n')[0];
            }
            else
            {
                Uyar(hata, "Veritabanına ulaşılamadı.\n" + DepoHatasiAciklayici.Acikla(hata));
            }
        }
    }

    // ============================ Olaylar (baska is parcaciklarindan gelir) ============================

    private void GunlukSatiriGeldi(string satir) => ArayuzdeCalistir(() =>
    {
        _gunlukListesi.Items.Add(satir);
        while (_gunlukListesi.Items.Count > 1000)
        {
            _gunlukListesi.Items.RemoveAt(0);
        }
        _gunlukListesi.TopIndex = _gunlukListesi.Items.Count - 1;
    });

    private void OturumlarDegisti(object? sender, EventArgs e) => ArayuzdeCalistir(BagliListesiniYenile);

    private void ArayuzdeCalistir(Action islem)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }
        try
        {
            BeginInvoke(islem);
        }
        catch (InvalidOperationException)
        {
            // pencere tam o anda kapandi; gosterilecek yer kalmadi.
        }
    }

    // ============================ Yardimcilar ============================

    private void Uyar(Exception hata, string metin)
    {
        _gunlukcu.Hata(hata, metin.Replace("\n", " "));
        MessageBox.Show(this, metin, GenelHataYakalayici.Baslik, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private void PanoyaKopyala(string metin)
    {
        if (string.IsNullOrEmpty(metin))
        {
            return;
        }
        try
        {
            Clipboard.SetText(metin);
        }
        catch (System.Runtime.InteropServices.ExternalException hata)
        {
            // Pano o an baska programda acik.
            Uyar(hata, "Panoya kopyalanamadı, tekrar deneyin.");
        }
    }

    private static int? SeciliId(ListView liste) =>
        liste.SelectedItems.Count > 0 && int.TryParse(liste.SelectedItems[0].Text, out int id) ? id : null;

    private static string Saat(DateTimeOffset? zaman) =>
        zaman is { } z ? z.ToLocalTime().ToString("dd.MM.yyyy HH:mm") : "-";

    private static void ListeyiDoldur(ListView liste, IEnumerable<string[]> satirlar)
    {
        liste.BeginUpdate();
        liste.Items.Clear();
        foreach (string[] satir in satirlar)
        {
            liste.Items.Add(new ListViewItem(satir));
        }
        liste.EndUpdate();
    }

    private static ListView Liste(params (string Baslik, int Genislik)[] sutunlar)
    {
        var liste = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false };
        foreach ((string baslik, int genislik) in sutunlar)
        {
            liste.Columns.Add(baslik, genislik);
        }
        return liste;
    }

    private static TabPage Sekme(string baslik, ListView liste, params Button[] butonlar)
    {
        var butonSatiri = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
        butonSatiri.Controls.AddRange(butonlar);
        var sekme = new TabPage(baslik);
        sekme.Controls.Add(liste);
        sekme.Controls.Add(butonSatiri);
        return sekme;
    }

    private static Button Buton(string metin, EventHandler tiklaninca)
    {
        var buton = new Button { Text = metin, AutoSize = true };
        buton.Click += tiklaninca;
        return buton;
    }

    // Ali istegi: her listede elle yenileme (veritabanindan yeniden okur).
    private Button YenileButonu() => Buton("Yenile", async (_, _) => await ListeleriYenileAsync());

    private static Label Etiket(string metin) => new() { Text = metin, AutoSize = true, Margin = new Padding(3, 7, 3, 3) };
}
