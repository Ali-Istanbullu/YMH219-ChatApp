using ChatApp.Client.Abstractions;
using ChatApp.Client.Metinler;
using ChatApp.Client.Network;
using ChatApp.Client.Session;
using ChatApp.Client.Settings;
using ChatApp.Protocol.Kurallar;
using ChatApp.Protocol.Modeller;
using ChatApp.Protocol.Paketler;
using ChatApp.WinForms.Platform;

namespace ChatApp.WinForms.Forms;

// Giris / kayit ekrani. Sunucu agda otomatik bulunur. "Gelismis" altinda (Ali karari):
//   - Sunucu adresi: otomatik bulunamiyorsa (baska ag / ngrok) elle yazilir.
//   - Sunucu parmak izi: ChatServer penceresindekiyle ayni olmali; yazilirsa sahte sunucuya baglanilmaz.
// Kullanici adi (ve elle yazildiysa adres) hatirlanir, sifre hatirlanmaz.
// Kayit: kullanici adi + gorunen ad + sifre (iki kez) + davet kodu; basariliysa ayni bilgilerle giris yapilir.
public sealed class GirisPaneli : UserControl
{
    private readonly SohbetIstemcisi _istemci;
    private readonly SunucuBulucu _bulucu;
    private readonly ParmakIziDeposu _parmakIzleri;
    private readonly IstemciAyarDeposu _ayarDeposu;
    private readonly IGunlukcu _gunlukcu;

    private readonly TextBox _sunucuKutusu;
    private readonly TextBox _parmakIziKutusu;
    private readonly TextBox _kullaniciAdiKutusu;
    private readonly TextBox _sifreKutusu;
    private readonly TextBox _sifreTekrarKutusu;
    private readonly TextBox _gorunenAdKutusu;
    private readonly TextBox _davetKoduKutusu;
    private readonly Control[] _kayitSatirlari;
    private readonly Control[] _adresSatiri;
    private readonly LinkLabel _gelismisBaglantisi;
    private readonly Button _tamamButonu;
    private readonly LinkLabel _modBaglantisi;
    private readonly Label _durumYazisi;
    private readonly Label _baslik;
    private readonly Label _gizlilikYazisi;

    private bool _kayitModu;

    public GirisPaneli(SohbetIstemcisi istemci, SunucuBulucu bulucu, ParmakIziDeposu parmakIzleri,
        IstemciAyarDeposu ayarDeposu, IGunlukcu gunlukcu)
    {
        ArgumentNullException.ThrowIfNull(istemci);
        ArgumentNullException.ThrowIfNull(bulucu);
        ArgumentNullException.ThrowIfNull(parmakIzleri);
        ArgumentNullException.ThrowIfNull(ayarDeposu);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        _istemci = istemci;
        _bulucu = bulucu;
        _parmakIzleri = parmakIzleri;
        _ayarDeposu = ayarDeposu;
        _gunlukcu = gunlukcu;

        Dock = DockStyle.Fill;
        _baslik = new Label { Font = new Font("Segoe UI", 16f, FontStyle.Bold), AutoSize = true, Margin = new Padding(3, 3, 3, 12) };

        _sunucuKutusu = Kutu();
        _parmakIziKutusu = Kutu();
        _kullaniciAdiKutusu = Kutu();
        _sifreKutusu = Kutu(sifre: true);
        _sifreTekrarKutusu = Kutu(sifre: true);
        _gorunenAdKutusu = Kutu();
        _davetKoduKutusu = Kutu();
        _davetKoduKutusu.CharacterCasing = CharacterCasing.Upper;

        var tablo = new TableLayoutPanel { ColumnCount = 2, AutoSize = true };
        SatirEkle(tablo, "Kullanıcı adı:", _kullaniciAdiKutusu);
        Control[] gorunen = SatirEkle(tablo, "Görünen ad:", _gorunenAdKutusu);
        SatirEkle(tablo, "Şifre:", _sifreKutusu);
        Control[] tekrar = SatirEkle(tablo, "Şifre (tekrar):", _sifreTekrarKutusu);
        Control[] davet = SatirEkle(tablo, "Davet kodu:", _davetKoduKutusu);
        _kayitSatirlari = [.. gorunen, .. tekrar, .. davet];
        _adresSatiri =
        [
            .. SatirEkle(tablo, "Sunucu adresi:", _sunucuKutusu),
            .. AciklamaEkle(tablo, "IP:port, örnek 192.168.1.20:5050. Sadece sunucu otomatik bulunamıyorsa " +
                "(başka ağ / internet, ngrok). Boş bırakılırsa otomatik bulunur."),
            .. SatirEkle(tablo, "Sunucu parmak izi:", _parmakIziKutusu),
            .. AciklamaEkle(tablo, "ChatServer penceresindeki \"Sertifika parmak izi\" (64 karakter). Yazılırsa " +
                "bağlanılan sunucunun gerçekten o olduğu kontrol edilir; aynı ağdaki sahte bir sunucuya şifre gitmez. " +
                "İsteğe bağlı."),
        ];

        _tamamButonu = new Button { AutoSize = true, Margin = new Padding(3, 12, 3, 3) };
        _tamamButonu.Click += async (_, _) => await TamamAsync();
        _modBaglantisi = new LinkLabel { AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
        _modBaglantisi.LinkClicked += (_, _) => ModuDegistir(!_kayitModu);
        // Ali karari: kullanici, mesajlarinin (silinenler dahil) yonetici tarafindan incelenebilecegini bilsin.
        _gizlilikYazisi = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            ForeColor = Color.DarkSlateGray,
            Margin = new Padding(3, 8, 3, 3),
            Text = "Bilgilendirme: Mesajlar sunucuda saklanır. Silinen mesajlar dahil, kötüye kullanım durumunda " +
                   "sunucu yöneticisi tarafından incelenebilir. Kayıt olarak bunu kabul etmiş olursunuz."
        };
        _gelismisBaglantisi = new LinkLabel { AutoSize = true, Margin = new Padding(3, 4, 3, 3) };
        _gelismisBaglantisi.LinkClicked += (_, _) => AdresSatiriniGoster(!_sunucuKutusu.Visible);
        _durumYazisi = new Label { AutoSize = true, MaximumSize = new Size(420, 0), ForeColor = Color.Firebrick, Margin = new Padding(3, 8, 3, 3) };
        var ipucu = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(420, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(3, 16, 3, 3),
            Text = "Sunucu, bağlı olduğunuz ağda otomatik bulunur. Bunun için sunucu bilgisayarıyla AYNI AĞDA " +
                   "(aynı Wi-Fi / aynı modem) olmalısınız.\n" +
                   "Davet kodunu sunucuyu çalıştıran kişiden alın."
        };

        var dikey = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Anchor = AnchorStyles.None };
        dikey.Controls.AddRange([_baslik, tablo, _gizlilikYazisi, _tamamButonu, _modBaglantisi, _gelismisBaglantisi, _durumYazisi, ipucu]);

        // Ortalamak icin tek hucreli tablo.
        var ortala = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1 };
        ortala.Controls.Add(dikey);
        Controls.Add(ortala);

        IstemciAyarlari ayarlar = _ayarDeposu.Yukle();
        _sunucuKutusu.Text = ayarlar.ElleAdres;
        _kullaniciAdiKutusu.Text = ayarlar.SonKullaniciAdi;
        ModuDegistir(kayit: false);
        AdresSatiriniGoster(ayarlar.ElleAdres.Length > 0);   // daha once elle yazildiysa gorunsun
    }

    // Giris basarili (SohbetIstemcisi artik bagli).
    public event Action? GirisYapildi;

    // Panel gosterilince: imlec bos olan ilk kutuya.
    public void Hazirla(string? mesaj = null)
    {
        _sifreKutusu.Clear();
        _sifreTekrarKutusu.Clear();
        DurumGoster(mesaj);
        TextBox ilkBos = new[] { _kullaniciAdiKutusu, _sifreKutusu }.FirstOrDefault(k => k.TextLength == 0) ?? _sifreKutusu;
        ilkBos.Focus();
        if (FindForm() is { } form)
        {
            form.AcceptButton = _tamamButonu;
        }
    }

    // Gelismis kapatilirsa iki kutu da bosaltilir: otomatik bulma, ilk baglantida parmak izi kendiliginden ogrenilir.
    private void AdresSatiriniGoster(bool goster)
    {
        foreach (Control kontrol in _adresSatiri)
        {
            kontrol.Visible = goster;
        }
        if (!goster)
        {
            _sunucuKutusu.Clear();
            _parmakIziKutusu.Clear();
        }
        _gelismisBaglantisi.Text = goster
            ? "Gelişmiş ayarları kapat (otomatik bul)"
            : "Gelişmiş: sunucu adresi ve sertifika parmak izi";
    }

    private void ModuDegistir(bool kayit)
    {
        _kayitModu = kayit;
        foreach (Control kontrol in _kayitSatirlari)
        {
            kontrol.Visible = kayit;
        }
        _gizlilikYazisi.Visible = kayit;
        _baslik.Text = kayit ? "Kayıt ol" : "Giriş yap";
        _tamamButonu.Text = kayit ? "Kayıt ol ve giriş yap" : "Giriş yap";
        _modBaglantisi.Text = kayit ? "Hesabınız var mı? Giriş yapın" : "Hesabınız yok mu? Davet koduyla kayıt olun";
        DurumGoster(null);
    }

    private async Task TamamAsync()
    {
        string elleAdres = _sunucuKutusu.Text.Trim();
        if (ParmakIziBicimi.Duzenle(elleAdres) is not null)
        {
            DurumGoster("\"Sunucu adresi\" kutusuna sertifika parmak izi yazılmış. Parmak izini \"Sunucu parmak izi\" " +
                "kutusuna yazın; adres kutusunu boş bırakırsanız sunucu otomatik bulunur.");
            return;
        }
        string? beklenenParmakIzi = null;
        if (_parmakIziKutusu.TextLength > 0)
        {
            beklenenParmakIzi = ParmakIziBicimi.Duzenle(_parmakIziKutusu.Text);
            if (beklenenParmakIzi is null)
            {
                DurumGoster($"Parmak izi {ParmakIziBicimi.Uzunluk} karakter olmalı (0-9, A-F). ChatServer penceresindeki " +
                    "\"Kopyala\" ile alıp yapıştırın.");
                return;
            }
        }
        SunucuAdresi? adres = null;
        if (elleAdres.Length > 0 && !SunucuAdresi.TryParse(elleAdres, out adres))
        {
            DurumGoster("Sunucu adresi geçersiz. Örnek: 192.168.1.20:5050 ya da 0.tcp.eu.ngrok.io:12345");
            return;
        }
        string kullaniciAdi = _kullaniciAdiKutusu.Text.Trim().ToLowerInvariant();
        string sifre = _sifreKutusu.Text;
        if (!GirdiKurallari.KullaniciAdiGecerliMi(kullaniciAdi))
        {
            DurumGoster(HataMetinleri.Acikla(HataKodu.KullaniciAdiGecersiz));
            return;
        }

        KayitIstegi? kayit = null;
        if (_kayitModu)
        {
            kayit = KayitIstegiOlustur(kullaniciAdi, sifre);
            if (kayit is null)
            {
                return;   // hata gosterildi
            }
        }
        else if (sifre.Length == 0)
        {
            DurumGoster("Şifrenizi yazın.");
            return;
        }

        Kilitle(true);
        try
        {
            if (adres is null)
            {
                DurumGoster("Sunucu aranıyor...", Color.DimGray);
                IReadOnlyList<SunucuAdresi> bulunanlar = await _bulucu.BulAsync();
                if (bulunanlar.Count == 0)
                {
                    DurumGoster("Sunucu bulunamadı. Sunucu bilgisayarıyla aynı ağa (aynı Wi-Fi / modem) bağlı olduğunuzu " +
                        "ve ChatServer'da \"Başlat\"a basıldığını kontrol edin.");
                    return;
                }
                adres = bulunanlar[0];
            }

            DurumGoster("Bağlanılıyor...", Color.DimGray);
            await ParmakIziyleBaglanAsync(adres, beklenenParmakIzi, elleAdres, kullaniciAdi, sifre, kayit);
        }
        finally
        {
            if (!IsDisposed)
            {
                Kilitle(false);
            }
        }
    }

    private KayitIstegi? KayitIstegiOlustur(string kullaniciAdi, string sifre)
    {
        string? gorunenAd = GirdiKurallari.GorunenAdiDuzenle(_gorunenAdKutusu.Text);
        if (gorunenAd is null)
        {
            DurumGoster(HataMetinleri.Acikla(HataKodu.GorunenAdGecersiz));
            return null;
        }
        if (!GirdiKurallari.SifreGecerliMi(sifre))
        {
            DurumGoster(HataMetinleri.Acikla(HataKodu.SifreGecersiz));
            return null;
        }
        if (sifre != _sifreTekrarKutusu.Text)
        {
            DurumGoster("Şifreler aynı değil.");
            return null;
        }
        string? davetKodu = GirdiKurallari.DavetKodunuDuzenle(_davetKoduKutusu.Text);
        if (davetKodu is null)
        {
            DurumGoster("Davet kodu XXXX-XXXX biçiminde olmalı.");
            return null;
        }
        return new KayitIstegi(kullaniciAdi, gorunenAd, sifre, davetKodu);
    }

    // Parmak izi elle yazildiysa bu adres icin "bilinen" o olur: TLS kontrolu sunucuyu onunla karsilastirir.
    // Sunucuya ulasilamaz ya da uymazsa eski kayda donulur (yanlis yazilmis parmak izi kalici olmasin).
    private async Task ParmakIziyleBaglanAsync(SunucuAdresi adres, string? beklenen, string elleAdres,
        string kullaniciAdi, string sifre, KayitIstegi? kayit)
    {
        string? onceki = _parmakIzleri.Bul(adres);
        bool degisti = beklenen is not null && !string.Equals(onceki, beklenen, StringComparison.OrdinalIgnoreCase);
        if (degisti)
        {
            _parmakIzleri.Kaydet(adres, beklenen!);
        }

        bool sunucuDogrulandi = await BaglanAsync(adres, elleAdres, kullaniciAdi, sifre, kayit, beklenen);

        if (degisti && !sunucuDogrulandi)
        {
            if (onceki is null)
            {
                _parmakIzleri.Sil(adres);
            }
            else
            {
                _parmakIzleri.Kaydet(adres, onceki);
            }
        }
    }

    // true: sunucuya TLS ile ulasildi ve parmak izi tuttu (giris/kayit reddedilmis olsa bile).
    private async Task<bool> BaglanAsync(SunucuAdresi adres, string elleAdres, string kullaniciAdi, string sifre,
        KayitIstegi? kayit, string? beklenenParmakIzi)
    {
        try
        {
            if (kayit is not null)
            {
                KayitSonucu kayitSonucu = await _istemci.KayitOlAsync(adres, kayit);
                if (!kayitSonucu.Basarili)
                {
                    DurumGoster("Kayıt olunamadı: " + HataMetinleri.Acikla(kayitSonucu.Hata));
                    return true;
                }
                _gunlukcu.Bilgi($"Kayit olundu, numara {kayitSonucu.KullaniciId}.");
            }

            GirisSonucu sonuc = await _istemci.GirisYapAsync(adres, kullaniciAdi, sifre);
            if (!sonuc.Basarili)
            {
                DurumGoster(HataMetinleri.GirisHatasi(sonuc.Hata, sonuc.Aciklama));
                return true;
            }

            _ayarDeposu.Kaydet(new IstemciAyarlari(elleAdres, kullaniciAdi));
            _sifreKutusu.Clear();
            _sifreTekrarKutusu.Clear();
            _davetKoduKutusu.Clear();
            ModuDegistir(kayit: false);
            GirisYapildi?.Invoke();
            return true;
        }
        catch (BaglantiHatasi hata)
        {
            _gunlukcu.Uyari($"Baglanilamadi ({adres}): {hata.Message}");
            DurumGoster(hata.Message);
            return false;
        }
        catch (SertifikaDegistiHatasi hata) when (beklenenParmakIzi is not null)
        {
            // Elle yazilan parmak izi tutmadi: soru sorulmaz, baglanilmaz.
            _gunlukcu.Uyari($"Sunucu parmak izi elle yazilanla ayni degil: {adres}");
            DurumGoster("Bağlanılmadı: bulunan sunucunun parmak izi yazdığınızla aynı değil.\n" +
                $"Yazdığınız: {Grupla(hata.EskiParmakIzi)}\nSunucunun: {Grupla(hata.YeniParmakIzi)}\n" +
                "ChatServer penceresindeki \"Sertifika parmak izi\" ile karşılaştırın. Aynıysa yanlış yazılmıştır; " +
                "farklıysa aynı ağda başka (sahte olabilecek) bir sunucu var.");
            return false;
        }
        catch (SertifikaDegistiHatasi hata)
        {
            _gunlukcu.Uyari($"Sunucu sertifikasi degisti: {adres}");
            if (YeniSertifikayaGuvenilsinMi(hata))
            {
                _parmakIzleri.Kaydet(hata.Adres, hata.YeniParmakIzi);
                return await BaglanAsync(adres, elleAdres, kullaniciAdi, sifre, kayit, null);   // yeni parmak iziyle tekrar dene
            }
            DurumGoster("Bağlanılmadı: sunucunun kimliği değişmiş.");
            return false;
        }
    }

    // Ali karari: uyar, kullanici secsin. Varsayilan secim "Hayir" (baglanma).
    private bool YeniSertifikayaGuvenilsinMi(SertifikaDegistiHatasi hata)
    {
        string metin =
            "Sunucunun kimliği (sertifikası) değişti!\n\n" +
            $"Sunucu: {hata.Adres}\n" +
            $"Eski parmak izi: {Grupla(hata.EskiParmakIzi)}\n" +
            $"Yeni parmak izi: {Grupla(hata.YeniParmakIzi)}\n\n" +
            "Sunucuyu çalıştıran kişi ChatServer'ı yeniden kurduysa bu normaldir: yeni parmak izini " +
            "onun ChatServer penceresinde gördüğüyle karşılaştırın.\n" +
            "Emin değilseniz BAĞLANMAYIN; araya başka biri girmiş olabilir.\n\n" +
            "Yeni sertifikaya güvenip bağlanılsın mı?";
        return MessageBox.Show(FindForm(), metin, GenelHataYakalayici.Baslik, MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    // "AB12CD34..." -> "AB12 CD34 ..." (okumasi kolay olsun)
    private static string Grupla(string parmakIzi) =>
        string.Join(' ', parmakIzi.Chunk(4).Select(p => new string(p)));

    private void Kilitle(bool kilitli)
    {
        _tamamButonu.Enabled = !kilitli;
        _modBaglantisi.Enabled = !kilitli;
        _gelismisBaglantisi.Enabled = !kilitli;
        UseWaitCursor = kilitli;
    }

    private void DurumGoster(string? metin, Color? renk = null)
    {
        _durumYazisi.ForeColor = renk ?? Color.Firebrick;
        _durumYazisi.Text = metin ?? string.Empty;
    }

    private static TextBox Kutu(bool sifre = false) => new() { Width = 260, UseSystemPasswordChar = sifre };

    // Kutunun altina gri, kucuk aciklama (ikinci sutunda).
    private static Control[] AciklamaEkle(TableLayoutPanel tablo, string metin)
    {
        var bos = new Label { AutoSize = true, Margin = Padding.Empty };
        var aciklama = new Label
        {
            Text = metin,
            AutoSize = true,
            MaximumSize = new Size(300, 0),
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 8.25f),
            Margin = new Padding(3, 0, 3, 6)
        };
        tablo.Controls.Add(bos);
        tablo.Controls.Add(aciklama);
        return [bos, aciklama];
    }

    private static Control[] SatirEkle(TableLayoutPanel tablo, string etiket, Control kontrol)
    {
        var yazi = new Label { Text = etiket, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 3, 3) };
        tablo.Controls.Add(yazi);
        tablo.Controls.Add(kontrol);
        return [yazi, kontrol];
    }
}
