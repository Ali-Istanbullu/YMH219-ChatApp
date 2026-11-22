; ================================================================================
;  ChatServer kurulum betigi (Inno Setup) - sunucu bilgisayarina kurulur.
;
;  En kolayi: repo kokunden  powershell -ExecutionPolicy Bypass -File installer\kurulumlari-olustur.ps1
;  Elle:
;    dotnet publish src/ChatApp.Server.WinForms -c Release -r win-x64 --self-contained false -o publish/ChatServer
;    powershell -File installer\postgresql-hazirla.ps1      (publish\pgsql klasorunu hazirlar)
;    ISCC.exe installer\ChatServer.iss
;  Cikti: installer\Output\ChatServer-Kurulum-1.0.0.exe
;
;  Icinde PostgreSQL'in sunucu dosyalari var (pgAdmin haric). ChatServer ilk acilista "Hazir veritabanini kur"
;  dendiginde bunlarla kendi veritabanini kurar (Windows servisi YOK; ChatServer acikken calisir).
;  Gereksinimler (yoksa indirilip sessizce kurulur, olmazsa kurulum durur):
;    - .NET 10 Desktop Runtime (x64)
;    - Microsoft Visual C++ 2015-2022 calisma zamani (x64) - PostgreSQL icin
;  Kurulum sonunda PostgreSQL programlarinin bu bilgisayarda gercekten calistigi denenir.
;  Kaldirinca (Ali karari): %LocalAppData%\ChatServer (veritabani, ayarlar, sertifika, loglar) KALIR,
;  program ve PostgreSQL dosyalari tamamen silinir. Yeniden kurulunca kaldigi yerden devam eder.
; ================================================================================

#define UygulamaAdi "ChatServer"
#define GorunenAd "ChatServer"
#define UygulamaSurumu "1.0.0"
#define Yayinci "Ali İstanbullu"
#define ExeAdi "ChatServer.exe"
#define YayinKlasoru "..\publish\ChatServer"
#define PostgresKlasoru "..\publish\pgsql"

; Microsoft'un "her zaman son .NET 10 Desktop Runtime x64" kalici baglantisi (aka.ms/dotnet/<kanal>/...)
#define RuntimeUrl "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe"
#define RuntimeDosyasi "windowsdesktop-runtime-win-x64.exe"

; PostgreSQL programlari Microsoft Visual C++ 2015-2022 calisma zamanini (vcruntime140.dll, msvcp140.dll) ister;
; bu dosyalar PostgreSQL paketinde YOK. Gelistiricinin bilgisayarinda genelde vardir, temiz Windows'ta olmayabilir.
; Microsoft'un kalici baglantisi:
#define VcUrl "https://aka.ms/vs/17/release/vc_redist.x64.exe"
#define VcDosyasi "vc_redist.x64.exe"

[Setup]
AppId={{BB79DB0F-A57D-4223-AE53-2CE84FE6BCFC}
AppName={#GorunenAd}
AppVersion={#UygulamaSurumu}
AppPublisher={#Yayinci}
DefaultDirName={autopf}\{#UygulamaAdi}
DefaultGroupName={#GorunenAd}
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename={#UygulamaAdi}-Kurulum-{#UygulamaSurumu}
SetupIconFile=..\assets\chatserver.ico
UninstallDisplayIcon={app}\{#ExeAdi}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
; Veritabani %LocalAppData%\ChatServer altinda (kurulumu yapan kullanicinin): bilerek kullanici klasoru kullaniliyor
UsedUserAreasWarning=no

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "masaustu"; Description: "Masaüstüne kısayol oluştur"; GroupDescription: "Kısayollar:"
Name: "baslatmenusu"; Description: "Başlat menüsüne kısayol oluştur"; GroupDescription: "Kısayollar:"

[Files]
Source: "{#YayinKlasoru}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
; PostgreSQL sunucu dosyalari: ChatServer bunlari {app}\pgsql\bin altinda arar.
Source: "{#PostgresKlasoru}\*"; DestDir: "{app}\pgsql"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "ucuncu-taraf\PostgreSQL-LISANS.txt"; DestDir: "{app}\pgsql"; Flags: ignoreversion

[Icons]
Name: "{autodesktop}\{#GorunenAd}"; Filename: "{app}\{#ExeAdi}"; Tasks: masaustu
Name: "{group}\{#GorunenAd}"; Filename: "{app}\{#ExeAdi}"; Tasks: baslatmenusu
Name: "{group}\{#GorunenAd} Kaldır"; Filename: "{uninstallexe}"; Tasks: baslatmenusu

[UninstallRun]
; Hazir veritabani calisiyorsa once durdurulur (yoksa dosyalari silinemez). Calismiyorsa pg_ctl hata verir; onemsiz.
Filename: "{app}\pgsql\bin\pg_ctl.exe"; Parameters: "stop -D ""{localappdata}\ChatServer\veritabani"" -m fast -w -t 30"; Flags: runhidden waituntilterminated; RunOnceId: "VeritabaniniDurdur"

[UninstallDelete]
; Program ve PostgreSQL dosyalari TAMAMEN silinir. %LocalAppData%\ChatServer bilerek SILINMEZ (veritabani orada).
Type: filesandordirs; Name: "{app}"

[Run]
Filename: "{app}\{#ExeAdi}"; Description: "ChatServer'ı başlat"; Flags: nowait postinstall skipifsilent

[Code]
var
  IndirmeSayfasi: TDownloadWizardPage;   // indirilen dosyalar {tmp}'ye gelir, kurulum bitince silinir

procedure InitializeWizard;
begin
  IndirmeSayfasi := CreateDownloadPage('Gerekli bileşenler indiriliyor',
    'ChatServer için gerekli Microsoft bileşenleri indiriliyor, lütfen bekleyin.', nil);
end;

// .NET 10 Desktop Runtime (x64) kurulu mu? Microsoft'un kurdugu klasorde 10.x surumu aranir.
function RuntimeKuruluMu: Boolean;
var
  Bulunan: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), Bulunan);
  if Result then
    FindClose(Bulunan);
end;

// Visual C++ 2015-2022 (x64) calisma zamani kurulu mu? Microsoft'un yazdigi kayit defteri degeri.
function VcKuruluMu: Boolean;
var
  Kurulu: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Kurulu)
            and (Kurulu = 1);
end;

// Indirilen kurulum programini sessizce calistirir. Basarisizsa False ve hata mesaji.
// 0 = basarili, 3010/1641 = basarili ama yeniden baslatma gerekli, 1638 = daha yeni surumu zaten kurulu.
function SessizKur(Dosya, Ad: String; var Hata: String): Boolean;
var
  Sayfa: TOutputMarqueeProgressWizardPage;
  CikisKodu: Integer;
begin
  Result := False;
  Sayfa := CreateOutputMarqueeProgressPage(Ad + ' kuruluyor', 'Bu birkaç dakika sürebilir, lütfen bekleyin.');
  Sayfa.Show;
  try
    Sayfa.Animate;
    if not Exec(ExpandConstant('{tmp}\') + Dosya, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, CikisKodu) then
    begin
      Hata := Ad + ' kurulum programı çalıştırılamadı: ' + SysErrorMessage(CikisKodu);
      Exit;
    end;
    if (CikisKodu <> 0) and (CikisKodu <> 3010) and (CikisKodu <> 1641) and (CikisKodu <> 1638) then
    begin
      Hata := Ad + ' kurulumu başarısız oldu (çıkış kodu ' + IntToStr(CikisKodu) + ').';
      Exit;
    end;
    Result := True;
  finally
    Sayfa.Hide;
  end;
end;

// "Kur" dugmesine basilinca: eksik bilesenler indirilir ve kurulur. Hata olursa kurulum ilerlemez.
function NextButtonClick(CurPageID: Integer): Boolean;
var
  Hata: String;
  RuntimeGerekli, VcGerekli: Boolean;
begin
  Result := True;
  if CurPageID <> wpReady then
    Exit;
  RuntimeGerekli := not RuntimeKuruluMu;
  VcGerekli := not VcKuruluMu;
  if not (RuntimeGerekli or VcGerekli) then
    Exit;

  IndirmeSayfasi.Clear;
  if RuntimeGerekli then
    IndirmeSayfasi.Add('{#RuntimeUrl}', '{#RuntimeDosyasi}', '');
  if VcGerekli then
    IndirmeSayfasi.Add('{#VcUrl}', '{#VcDosyasi}', '');
  IndirmeSayfasi.Show;
  try
    try
      IndirmeSayfasi.Download;
    except
      if not IndirmeSayfasi.AbortedByUser then
        SuppressibleMsgBox('Gerekli Microsoft bileşenleri indirilemedi. İnternet bağlantınızı kontrol edip tekrar deneyin.' + #13#10#13#10 +
          AddPeriod(GetExceptionMessage), mbCriticalError, MB_OK, IDOK);
      Result := False;
      Exit;
    end;
  finally
    IndirmeSayfasi.Hide;
  end;

  if RuntimeGerekli then
  begin
    if not SessizKur('{#RuntimeDosyasi}', '.NET 10 Desktop Runtime', Hata) or not RuntimeKuruluMu then
    begin
      if Hata = '' then Hata := '.NET 10 Desktop Runtime kuruldu görünüyor ama bulunamadı.';
      SuppressibleMsgBox(Hata + #13#10#13#10 + 'ChatServer kurulamadı.', mbCriticalError, MB_OK, IDOK);
      Result := False;
      Exit;
    end;
  end;

  if VcGerekli then
  begin
    if not SessizKur('{#VcDosyasi}', 'Microsoft Visual C++ çalışma zamanı', Hata) or not VcKuruluMu then
    begin
      if Hata = '' then Hata := 'Microsoft Visual C++ çalışma zamanı kuruldu görünüyor ama bulunamadı.';
      SuppressibleMsgBox(Hata + #13#10#13#10 + 'ChatServer kurulamadı.', mbCriticalError, MB_OK, IDOK);
      Result := False;
    end;
  end;
end;

// Guncelleme (eskisinin ustune kurulum): hazir veritabani calisiyorsa PostgreSQL dosyalari kilitli olur;
// once durdurulur. Ilk kurulumda pg_ctl yoktur, bir sey yapilmaz.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  PgCtl: String;
  CikisKodu: Integer;
begin
  Result := '';
  PgCtl := ExpandConstant('{app}\pgsql\bin\pg_ctl.exe');
  if FileExists(PgCtl) then
    Exec(PgCtl, 'stop -D "' + ExpandConstant('{localappdata}\ChatServer\veritabani') + '" -m fast -w -t 30', '',
         SW_HIDE, ewWaitUntilTerminated, CikisKodu);
end;

// Kurulum bitince: PostgreSQL programlari BU bilgisayarda gercekten calisiyor mu? (eksik DLL vb. burada yakalanir)
procedure CurStepChanged(CurStep: TSetupStep);
var
  CikisKodu: Integer;
begin
  if CurStep <> ssPostInstall then
    Exit;
  if not Exec(ExpandConstant('{app}\pgsql\bin\initdb.exe'), '--version', '', SW_HIDE, ewWaitUntilTerminated, CikisKodu)
     or (CikisKodu <> 0) then
    SuppressibleMsgBox('Uyarı: PostgreSQL programları bu bilgisayarda çalıştırılamadı (çıkış kodu ' + IntToStr(CikisKodu) + ').' + #13#10 +
      'ChatServer kuruldu ama "Hazır veritabanı" çalışmayabilir; "Kendi PostgreSQL''imi kullanacağım" seçeneği yine kullanılabilir.',
      mbError, MB_OK, IDOK);
end;
