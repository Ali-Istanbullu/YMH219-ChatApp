# ================================================================================
#  Iki kurulum dosyasini birden olusturur (repo kokunden calistirilir):
#    installer\Output\ChatApp-Kurulum-<surum>.exe
#    installer\Output\ChatServer-Kurulum-<surum>.exe
#
#  Kullanim:
#    powershell -ExecutionPolicy Bypass -File installer\kurulumlari-olustur.ps1
#    powershell -ExecutionPolicy Bypass -File installer\kurulumlari-olustur.ps1 -YerelPostgres "C:\Program Files\PostgreSQL\18"
#  -YerelPostgres verilirse PostgreSQL indirilmez, bilgisayardaki kurulumdan kopyalanir.
# ================================================================================
param(
    [string]$YerelPostgres = "",
    [string]$Iscc = ""
)
$ErrorActionPreference = "Stop"

function Calistir([string]$aciklama, [scriptblock]$komut) {
    Write-Host "== $aciklama" -ForegroundColor Cyan
    & $komut
    if ($LASTEXITCODE -ne 0) { throw "$aciklama basarisiz (cikis kodu $LASTEXITCODE)" }
}

if (-not $Iscc) {
    $aday = @("$env:USERPROFILE\Desktop\Inno Setup 7\ISCC.exe") +
        (Get-ChildItem "C:\Program Files*\Inno Setup*\ISCC.exe" -ErrorAction SilentlyContinue | ForEach-Object FullName)
    $Iscc = $aday | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $Iscc) { throw "ISCC.exe bulunamadi. -Iscc ile yolunu verin." }
}

Calistir "ChatApp yayinlaniyor" { dotnet publish src/ChatApp.WinForms -c Release -r win-x64 --self-contained false -o publish/ChatApp }
Calistir "ChatServer yayinlaniyor" { dotnet publish src/ChatApp.Server.WinForms -c Release -r win-x64 --self-contained false -o publish/ChatServer }

Write-Host "== PostgreSQL dosyalari hazirlaniyor" -ForegroundColor Cyan
if ($YerelPostgres) {
    & "$PSScriptRoot\postgresql-hazirla.ps1" -Yerel $YerelPostgres
} else {
    & "$PSScriptRoot\postgresql-hazirla.ps1"
}

Calistir "ChatApp kurulumu derleniyor" { & $Iscc "installer\ChatApp.iss" }
Calistir "ChatServer kurulumu derleniyor" { & $Iscc "installer\ChatServer.iss" }

Get-ChildItem installer\Output\*.exe | ForEach-Object { "{0}  {1:N1} MB" -f $_.Name, ($_.Length / 1MB) }
