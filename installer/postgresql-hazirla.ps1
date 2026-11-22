# ================================================================================
#  ChatServer kurulumuna konacak PostgreSQL dosyalarini hazirlar: publish\pgsql
#
#  Iki kaynak:
#   -Yerel "C:\Program Files\PostgreSQL\18"  : bilgisayarda kurulu PostgreSQL 18'den kopyalar (internet gerekmez)
#   (verilmezse)                              : EnterpriseDB'nin resmi zip'ini indirir, yanindaki .sha256 ile dogrular
#
#  Sadece sunucunun calismasi icin gerekenler alinir (bin, lib, share + lisans dosyalari);
#  pgAdmin, StackBuilder, belgeler, gelistirici dosyalari ve hata ayiklama sembolleri alinmaz.
#  Ana surum 18 olmali: hazir veritabani 18 ile kuruldu; baska ana surum o veriyi acamaz.
#
#  Kullanim (repo kokunden):
#    powershell -ExecutionPolicy Bypass -File installer\postgresql-hazirla.ps1
#    powershell -ExecutionPolicy Bypass -File installer\postgresql-hazirla.ps1 -Yerel "C:\Program Files\PostgreSQL\18"
# ================================================================================
param(
    [string]$Surum = "18.7-1",
    [string]$Yerel = "",
    [string]$Hedef = "publish\pgsql"
)
$ErrorActionPreference = "Stop"

$alinacaklar = @("bin", "lib", "share")
# wx*.dll: StackBuilder'in arayuz kutuphaneleri (sunucu kullanmaz). Visual C++ calisma zamani pakette yok;
# ChatServer kurulumu onu Microsoft'tan indirip kurar.
$haricDosyalar = @("*.pdb", "pgAdmin*", "stackbuilder*", "wx*.dll")

if (Test-Path $Hedef) { Remove-Item $Hedef -Recurse -Force }
New-Item -ItemType Directory -Path $Hedef | Out-Null

if ($Yerel) {
    $kaynak = $Yerel
    Write-Host "Yerel PostgreSQL kullaniliyor: $kaynak"
} else {
    $zipAdi = "postgresql-$Surum-windows-x64-binaries.zip"
    $url = "https://get.enterprisedb.com/postgresql/$zipAdi"
    $indirme = "publish\indirilen"
    New-Item -ItemType Directory -Path $indirme -Force | Out-Null
    $zip = Join-Path $indirme $zipAdi

    if (-not (Test-Path $zip)) {
        Write-Host "Indiriliyor: $url"
        Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
    }

    # EDB, zip'in yanina SHA-256 dosyasi koyar: indirilen dosya bozuk/eksikse burada yakalanir.
    $beklenen = ((Invoke-WebRequest -Uri "$url.sha256" -UseBasicParsing).Content -split '\s+')[0].Trim().ToLower()
    $gercek = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
    if ($beklenen -ne $gercek) {
        Remove-Item $zip -Force
        throw "SHA-256 tutmadi ($zipAdi). Beklenen $beklenen, gelen $gercek"
    }
    Write-Host "SHA-256 dogrulandi: $gercek"

    $acilan = Join-Path $indirme "acilan"
    if (Test-Path $acilan) { Remove-Item $acilan -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $acilan
    $kaynak = Join-Path $acilan "pgsql"
}

foreach ($klasor in $alinacaklar) {
    $yol = Join-Path $kaynak $klasor
    if (-not (Test-Path $yol)) { throw "PostgreSQL klasoru eksik: $yol" }
    Copy-Item $yol -Destination $Hedef -Recurse
}
# Lisans dosyalari (kok klasorde ya da 'licenses' gibi klasorlerde olabilir)
Get-ChildItem $kaynak -File | Where-Object { $_.Name -match 'licen|copying|copyright' } |
    ForEach-Object { Copy-Item $_.FullName -Destination $Hedef }
foreach ($haric in $haricDosyalar) {
    Get-ChildItem $Hedef -Recurse -Filter $haric | Remove-Item -Recurse -Force
}

# Sunucunun calistigini dogrula: initdb ve pg_ctl surumunu yazdir.
& (Join-Path $Hedef "bin\pg_ctl.exe") --version
if ($LASTEXITCODE -ne 0) { throw "pg_ctl calistirilamadi" }
$surumYazisi = & (Join-Path $Hedef "bin\postgres.exe") --version
if ($surumYazisi -notmatch ' 18\.') { throw "PostgreSQL ana surumu 18 olmali: $surumYazisi" }

$boyut = (Get-ChildItem $Hedef -Recurse -File | Measure-Object Length -Sum).Sum / 1MB
Write-Host ("Hazir: {0} ({1:N0} MB) - {2}" -f $Hedef, $boyut, $surumYazisi)
