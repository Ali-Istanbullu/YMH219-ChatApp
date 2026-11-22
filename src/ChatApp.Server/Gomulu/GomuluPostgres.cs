using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using ChatApp.Server.Abstractions;
using ChatApp.Server.Settings;

namespace ChatApp.Server.Gomulu;

// ================================================================================
//  CHATSERVER'IN KENDI POSTGRESQL'I ("hazir veritabani", Ali karari 22.11)
//
//  Kullanici PostgreSQL kurmakla ugrasmasin diye ChatServer kendi veritabanini kurar ve yonetir:
//   - Kurulum (bir kez): initdb -> ayarlar (port, sadece localhost) -> baslat -> chatapp kullanicisi + veritabani.
//   - Her acilista baslatilir, kapanista durdurulur (Windows servisi YOK; sadece ChatServer kullaniyor).
//  Veriler %LocalAppData%\ChatServer\veritabani altinda; ChatServer kaldirilsa da kalir.
//  Sifreler rastgele uretilir; chatapp sifresi ayarlar.json'a DPAPI ile sifrelenmis yazilir,
//  yonetici (postgres) sifresi hicbir yere yazilmaz (ChatServer'in ihtiyaci yok).
//
//  PostgreSQL'in kendi programlari (initdb, pg_ctl, psql) calistirilir; ek NuGet paketi gerekmez.
// ================================================================================
public sealed class GomuluPostgres
{
    public const int IlkPort = 5434;
    private const int SonPort = 5454;
    private const int SifreUzunlugu = 32;
    private const string SifreKarakterleri = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
    private static readonly TimeSpan IslemZamanAsimi = TimeSpan.FromSeconds(90);

    private readonly string _binKlasoru;
    private readonly string _veriKlasoru;
    private readonly string _logDosyasi;
    private readonly IGunlukcu _gunlukcu;

    public GomuluPostgres(string binKlasoru, string veriKlasoru, string logDosyasi, IGunlukcu gunlukcu)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(binKlasoru);
        ArgumentException.ThrowIfNullOrWhiteSpace(veriKlasoru);
        ArgumentException.ThrowIfNullOrWhiteSpace(logDosyasi);
        ArgumentNullException.ThrowIfNull(gunlukcu);
        _binKlasoru = binKlasoru;
        _veriKlasoru = veriKlasoru;
        _logDosyasi = logDosyasi;
        _gunlukcu = gunlukcu;
    }

    public string VeriKlasoru => _veriKlasoru;

    // Daha once kurulmus mu? (PostgreSQL veri klasorunde PG_VERSION dosyasi olur.)
    public bool KuruluMu => File.Exists(Path.Combine(_veriKlasoru, "PG_VERSION"));

    // PostgreSQL programlarinin klasoru. Sira:
    //  1) ChatServer'in yanindaki pgsql\bin (kurulum dosyasi buraya koyar)
    //  2) CHATAPP_PG_BIN ortam degiskeni (testler)
    //  3) Program Files\PostgreSQL\<en yeni surum>\bin (kaynak koddan calistiran gelistirici)
    // Bulunamazsa null: "hazir veritabani" secenegi kapali olur.
    public static string? BinKlasoruBul(string uygulamaKlasoru)
    {
        string yanindaki = Path.Combine(uygulamaKlasoru, "pgsql", "bin");
        if (PgCtlVarMi(yanindaki))
        {
            return yanindaki;
        }

        string? ortam = Environment.GetEnvironmentVariable("CHATAPP_PG_BIN");
        if (!string.IsNullOrWhiteSpace(ortam) && PgCtlVarMi(ortam))
        {
            return ortam;
        }

        string programFiles = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PostgreSQL");
        if (Directory.Exists(programFiles))
        {
            foreach (string surum in Directory.GetDirectories(programFiles).OrderByDescending(SurumNumarasi))
            {
                string bin = Path.Combine(surum, "bin");
                if (PgCtlVarMi(bin))
                {
                    return bin;
                }
            }
        }
        return null;
    }

    // Ilk kurulum. Basariliysa veritabani calisir durumda kalir ve kaydedilecek ayarlar doner.
    public async Task<SunucuAyarlari> KurAsync()
    {
        if (Directory.Exists(_veriKlasoru) && Directory.EnumerateFileSystemEntries(_veriKlasoru).Any())
        {
            throw new GomuluVeritabaniHatasi(
                $"Veritabanı klasörü boş değil; daha önce kurulmuş olabilir:\n{_veriKlasoru}");
        }

        int port = BosPortBul();
        string yoneticiSifresi = RandomNumberGenerator.GetString(SifreKarakterleri, SifreUzunlugu);
        string chatappSifresi = RandomNumberGenerator.GetString(SifreKarakterleri, SifreUzunlugu);
        _gunlukcu.Bilgi($"Hazir veritabani kuruluyor: {_veriKlasoru}, port {port}.");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_veriKlasoru))!);
        string sifreDosyasi = Path.GetTempFileName();   // initdb yonetici sifresini dosyadan okur (komut satirinda gorunmesin)
        try
        {
            await File.WriteAllTextAsync(sifreDosyasi, yoneticiSifresi);
            // --locale=C: Windows dil ayari Turkce/ozel karakterliyken initdb'nin hata vermesini onler.
            await CalistirAsync("initdb", ["-D", _veriKlasoru, "-U", "postgres", "-A", "scram-sha-256",
                "--pwfile", sifreDosyasi, "-E", "UTF8", "--locale=C"]);
        }
        finally
        {
            File.Delete(sifreDosyasi);
        }

        // Sadece bu bilgisayardan baglanilabilir; port bizim sectigimiz.
        await File.AppendAllTextAsync(Path.Combine(_veriKlasoru, "postgresql.conf"),
            $"\n# ChatServer ayarlari\nport = {port}\nlisten_addresses = 'localhost'\n");

        await BaslatAsync();

        // chatapp kullanicisi ve veritabani (sifre sadece harf/rakam: SQL icinde guvenle yazilir).
        await CalistirAsync("psql", ["-h", "localhost", "-p", port.ToString(), "-U", "postgres", "-d", "postgres",
                "-v", "ON_ERROR_STOP=1", "-q",
                "-c", $"CREATE ROLE chatapp LOGIN PASSWORD '{chatappSifresi}'",
                "-c", "CREATE DATABASE chatapp OWNER chatapp ENCODING 'UTF8'"],
            sifre: yoneticiSifresi);

        _gunlukcu.Bilgi("Hazir veritabani kuruldu.");
        return new SunucuAyarlari("localhost", port, "chatapp", "chatapp", chatappSifresi,
            SunucuAyarlari.VarsayilanDinlemePortu, GomuluVeritabani: true);
    }

    // Calismiyorsa baslatir (ChatServer bir onceki sefer coktuyse hala calisiyor olabilir).
    public async Task BaslatAsync()
    {
        if (!KuruluMu)
        {
            throw new GomuluVeritabaniHatasi($"Veritabanı bulunamadı:\n{_veriKlasoru}");
        }
        if (await CalisiyorMuAsync())
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_logDosyasi))!);
        // Ciktisi yakalanmaz: pg_ctl'in baslattigi sunucu, yakalanan ciktiyi acik tutup bekletebiliyor.
        await CalistirAsync("pg_ctl", ["start", "-D", _veriKlasoru, "-l", _logDosyasi, "-w", "-t", "60"], ciktiYakala: false);
        _gunlukcu.Bilgi("Hazir veritabani baslatildi.");
    }

    public async Task DurdurAsync()
    {
        if (!KuruluMu || !await CalisiyorMuAsync())
        {
            return;
        }
        await CalistirAsync("pg_ctl", ["stop", "-D", _veriKlasoru, "-m", "fast", "-w", "-t", "60"]);
        _gunlukcu.Bilgi("Hazir veritabani durduruldu.");
    }

    public async Task<bool> CalisiyorMuAsync() =>
        (await CalistirAsync("pg_ctl", ["status", "-D", _veriKlasoru], hataVer: false)).CikisKodu == 0;

    // Eski (sifresi bilinmeyen) veritabanini kenara koyar: veritabani-yedek-<tarih> klasorune tasinir, silinmez.
    public string EskisiniYedekle()
    {
        string yedek = $"{_veriKlasoru}-yedek-{DateTime.Now:yyyyMMdd-HHmmss}";
        Directory.Move(_veriKlasoru, yedek);
        _gunlukcu.Uyari($"Eski veritabani yedeklendi: {yedek}");
        return yedek;
    }

    // ============================ Yardimcilar ============================

    private async Task<(int CikisKodu, string Cikti)> CalistirAsync(string program, IEnumerable<string> argumanlar,
        string? sifre = null, bool ciktiYakala = true, bool hataVer = true)
    {
        var bilgi = new ProcessStartInfo(Path.Combine(_binKlasoru, ProgramAdi(program)))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = ciktiYakala,
            RedirectStandardError = ciktiYakala
        };
        foreach (string arguman in argumanlar)
        {
            bilgi.ArgumentList.Add(arguman);
        }
        if (sifre is not null)
        {
            bilgi.Environment["PGPASSWORD"] = sifre;   // sadece bu programa; komut satirinda gorunmez
        }

        Process surec;
        try
        {
            surec = Process.Start(bilgi) ?? throw new GomuluVeritabaniHatasi($"{program} başlatılamadı.");
        }
        catch (Exception hata) when (hata is Win32Exception or IOException)
        {
            throw new GomuluVeritabaniHatasi($"PostgreSQL programı ({program}) çalıştırılamadı: {hata.Message}", null, hata);
        }

        using (surec)
        {
            Task<string> cikti = ciktiYakala ? surec.StandardOutput.ReadToEndAsync() : Task.FromResult(string.Empty);
            Task<string> hataCiktisi = ciktiYakala ? surec.StandardError.ReadToEndAsync() : Task.FromResult(string.Empty);
            using var zamanAsimi = new CancellationTokenSource(IslemZamanAsimi);
            try
            {
                await surec.WaitForExitAsync(zamanAsimi.Token);
            }
            catch (OperationCanceledException)
            {
                surec.Kill(entireProcessTree: true);
                throw new GomuluVeritabaniHatasi($"PostgreSQL programı ({program}) zamanında bitmedi.", LogSonu());
            }

            string tumCikti = $"{await cikti}\n{await hataCiktisi}".Trim();
            if (hataVer && surec.ExitCode != 0)
            {
                throw new GomuluVeritabaniHatasi($"PostgreSQL programı ({program}) başarısız oldu (çıkış kodu {surec.ExitCode}).",
                    $"{tumCikti}\n{LogSonu()}".Trim());
            }
            return (surec.ExitCode, tumCikti);
        }
    }

    // PostgreSQL log dosyasinin son satirlari (hata sebebi genelde orada yazar).
    private string LogSonu()
    {
        try
        {
            return File.Exists(_logDosyasi)
                ? string.Join('\n', File.ReadLines(_logDosyasi).TakeLast(15))
                : string.Empty;
        }
        catch (Exception hata) when (hata is IOException or UnauthorizedAccessException)
        {
            return string.Empty;   // log okunamadi; asil hata mesaji yine gosterilir
        }
    }

    // 5434'ten baslayarak bu bilgisayarda bos olan ilk port.
    private static int BosPortBul()
    {
        for (int port = IlkPort; port <= SonPort; port++)
        {
            if (PortBosMu(port))
            {
                return port;
            }
        }
        throw new GomuluVeritabaniHatasi($"{IlkPort}-{SonPort} arasında boş port bulunamadı.");
    }

    private static bool PortBosMu(int port)
    {
        foreach (IPAddress adres in new[] { IPAddress.Any, IPAddress.Loopback })
        {
            try
            {
                var dinleyici = new TcpListener(adres, port);
                dinleyici.Start();
                dinleyici.Stop();
            }
            catch (SocketException)
            {
                return false;
            }
        }
        return true;
    }

    private static bool PgCtlVarMi(string bin) => File.Exists(Path.Combine(bin, ProgramAdi("pg_ctl")));

    private static string ProgramAdi(string ad) => OperatingSystem.IsWindows() ? ad + ".exe" : ad;

    private static int SurumNumarasi(string klasor) => int.TryParse(Path.GetFileName(klasor), out int surum) ? surum : 0;
}
