; ================================================================================
;  ChatApp kurulum betigi (Inno Setup) - sohbet edecek herkesin bilgisayarina kurulur.
;
;  En kolayi: repo kokunden  powershell -ExecutionPolicy Bypass -File installer\kurulumlari-olustur.ps1
;  Elle:
;    dotnet publish src/ChatApp.WinForms -c Release -r win-x64 --self-contained false -o publish/ChatApp
;    ISCC.exe installer\ChatApp.iss
;  Cikti: installer\Output\ChatApp-Kurulum-1.0.0.exe  (repoya girmez, GitHub Releases'e yuklenir)
;
;  .NET 10 Desktop Runtime pakete GOMULMEZ: yoksa Microsoft'tan indirilip sessizce kurulur;
;  indirme/kurulum basarisizsa hata gosterilir ve kurulum ilerlemez.
;  Kaldirinca %LocalAppData%\ChatApp (ayarlar, sunucu parmak izleri, loglar) SILINMEZ.
; ================================================================================

#define UygulamaAdi "ChatApp"
#define GorunenAd "ChatApp"
#define UygulamaSurumu "1.0.0"
#define Yayinci "Ali İstanbullu"
#define ExeAdi "ChatApp.exe"
#define YayinKlasoru "..\publish\ChatApp"

; Microsoft'un "her zaman son .NET 10 Desktop Runtime x64" kalici baglantisi (aka.ms/dotnet/<kanal>/...)
#define RuntimeUrl "https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe"
#define RuntimeDosyasi "windowsdesktop-runtime-win-x64.exe"

[Setup]
AppId={{7D9E6E1D-F737-4C12-8792-36B6D977B554}
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
SetupIconFile=..\assets\chatapp.ico
UninstallDisplayIcon={app}\{#ExeAdi}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Guncelleme/kaldirma sirasinda acik ChatApp kapatilsin
CloseApplications=yes

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "masaustu"; Description: "Masaüstüne kısayol oluştur"; GroupDescription: "Kısayollar:"
Name: "baslatmenusu"; Description: "Başlat menüsüne kısayol oluştur"; GroupDescription: "Kısayollar:"

[Files]
Source: "{#YayinKlasoru}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autodesktop}\{#GorunenAd}"; Filename: "{app}\{#ExeAdi}"; Tasks: masaustu
Name: "{group}\{#GorunenAd}"; Filename: "{app}\{#ExeAdi}"; Tasks: baslatmenusu
Name: "{group}\{#GorunenAd} Kaldır"; Filename: "{uninstallexe}"; Tasks: baslatmenusu

[UninstallDelete]
; Program klasoru TAMAMEN silinir. %LocalAppData%\ChatApp bilerek SILINMEZ.
Type: filesandordirs; Name: "{app}"

[Run]
Filename: "{app}\{#ExeAdi}"; Description: "ChatApp'i başlat"; Flags: nowait postinstall skipifsilent

[Code]
var
  IndirmeSayfasi: TDownloadWizardPage;   // indirilen dosya {tmp}'ye gelir, kurulum bitince silinir

procedure InitializeWizard;
begin
  IndirmeSayfasi := CreateDownloadPage('.NET 10 Desktop Runtime indiriliyor',
    'ChatApp için gerekli .NET 10 Desktop Runtime indiriliyor, lütfen bekleyin.', nil);
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

// Runtime'i sessizce kurar. Basarisizsa False ve hata mesaji.
function RuntimeKur(var Hata: String): Boolean;
var
  Sayfa: TOutputMarqueeProgressWizardPage;
  CikisKodu: Integer;
begin
  Result := False;
  Sayfa := CreateOutputMarqueeProgressPage('.NET 10 Desktop Runtime kuruluyor',
    'Bu birkaç dakika sürebilir, lütfen bekleyin.');
  Sayfa.Show;
  try
    Sayfa.Animate;
    if not Exec(ExpandConstant('{tmp}\{#RuntimeDosyasi}'), '/install /quiet /norestart', '',
                SW_HIDE, ewWaitUntilTerminated, CikisKodu) then
    begin
      Hata := 'Runtime kurulum programı çalıştırılamadı: ' + SysErrorMessage(CikisKodu);
      Exit;
    end;
    // 0 = basarili, 3010/1641 = basarili ama yeniden baslatma gerekli
    if (CikisKodu <> 0) and (CikisKodu <> 3010) and (CikisKodu <> 1641) then
    begin
      Hata := 'Runtime kurulumu başarısız oldu (çıkış kodu ' + IntToStr(CikisKodu) + ').';
      Exit;
    end;
    if not RuntimeKuruluMu then
    begin
      Hata := 'Runtime kuruldu görünüyor ama bulunamadı.';
      Exit;
    end;
    Result := True;
  finally
    Sayfa.Hide;
  end;
end;

// "Kur" dugmesine basilinca: runtime yoksa indir + kur. Hata olursa kurulum ilerlemez.
function NextButtonClick(CurPageID: Integer): Boolean;
var
  Hata: String;
begin
  Result := True;
  if (CurPageID <> wpReady) or RuntimeKuruluMu then
    Exit;

  IndirmeSayfasi.Clear;
  IndirmeSayfasi.Add('{#RuntimeUrl}', '{#RuntimeDosyasi}', '');
  IndirmeSayfasi.Show;
  try
    try
      IndirmeSayfasi.Download;
    except
      if not IndirmeSayfasi.AbortedByUser then
        SuppressibleMsgBox('.NET 10 Desktop Runtime indirilemedi. İnternet bağlantınızı kontrol edip tekrar deneyin.' + #13#10#13#10 +
          AddPeriod(GetExceptionMessage), mbCriticalError, MB_OK, IDOK);
      Result := False;
      Exit;
    end;
  finally
    IndirmeSayfasi.Hide;
  end;

  if not RuntimeKur(Hata) then
  begin
    SuppressibleMsgBox(Hata + #13#10#13#10 + '{#GorunenAd} kurulamadı.', mbCriticalError, MB_OK, IDOK);
    Result := False;
  end;
end;
