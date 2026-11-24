# Geliştirici amaçlı yerel kaynak kurulum ve açık güncelleme. Son kullanıcılar NuGet.org üzerinden kurar.
# Does not publish to GitHub or NuGet.org. Does not replace an installed tool without -Update.
[CmdletBinding()]
param([switch]$Update)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$solution = Join-Path $repo 'Softbreak.NuGetLicenseRadar.slnx'
$project = Join-Path $repo 'Softbreak.LicenseRadar.Cli\Softbreak.LicenseRadar.Cli.csproj'
$localSource = Join-Path $repo '.local-test\packages'
$toolId = 'Softbreak.NuGetLicenseRadar'
$toolVersion = '0.3.0'

function Invoke-DotNet {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw ('dotnet başarısız (çıkış {0}): {1}' -f $LASTEXITCODE, ($Arguments -join ' '))
    }
}

Write-Host 'Softbreak NuGet License Radar — GELİŞTİRİCİ yerel kurulum' -ForegroundColor Cyan
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK bulunamadı. .NET 10 SDK kurup tekrar deneyin.'
}

$sdkList = & dotnet --list-sdks
if ($LASTEXITCODE -ne 0 -or -not (@($sdkList | Select-String '^10\.') ).Count) {
    throw '.NET 10 SDK gerekli: https://dotnet.microsoft.com/download/dotnet/10.0'
}

# Preflight: prevent work on source tree when update was not explicitly requested.
$installedTools = & dotnet tool list --global
if ($LASTEXITCODE -ne 0) { throw 'Yüklü .NET global araçları listelenemedi.' }
$installed = @($installedTools | Where-Object { $_ -match '^\s*Softbreak\.NuGetLicenseRadar\s+' })
if ($installed.Count -gt 0 -and -not $Update) {
    throw 'License Radar zaten kurulu. Güncellemek için Install-Local.ps1 -Update çalıştırın; izin olmadan değiştirilmeyecek.'
}

Write-Host '[1/3] Release testleri...' -ForegroundColor Cyan
Invoke-DotNet test $solution '--configuration' 'Release' '--framework' 'net10.0'

Write-Host '[2/3] Yerel .NET Tool paketi oluşturuluyor...' -ForegroundColor Cyan
New-Item -ItemType Directory -Path $localSource -Force | Out-Null
Invoke-DotNet pack $project '--configuration' 'Release' '--output' $localSource

$package = Join-Path $localSource ('{0}.{1}.nupkg' -f $toolId, $toolVersion)
if (-not (Test-Path -LiteralPath $package)) {
    throw ('Tool paketi oluşturulamadı: {0}' -f $package)
}

if ($installed.Count -gt 0) {
    Write-Host '[3/3] Mevcut global araç -Update onayıyla güncelleniyor...' -ForegroundColor Cyan
    Invoke-DotNet tool update '--global' $toolId '--version' $toolVersion '--framework' 'net10.0' '--add-source' $localSource
} else {
    Write-Host '[3/3] Yeni global araç kuruluyor...' -ForegroundColor Cyan
    Invoke-DotNet tool install '--global' $toolId '--version' $toolVersion '--framework' 'net10.0' '--add-source' $localSource
}

$installedCommand = Join-Path $HOME '.dotnet\tools\license-radar.exe'
if (-not (Test-Path -LiteralPath $installedCommand)) {
    throw 'Kurulum tamamlandı ancak license-radar.exe beklenen konumda bulunamadı.'
}
& $installedCommand --help | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Kurulum sonrası license-radar --help testi başarısız.'
}

Write-Host ''
Write-Host 'INSTALLATION: PASS' -ForegroundColor Green
Write-Host ('VERSION: {0}' -f $toolVersion)
Write-Host ('TOOL: {0}' -f $installedCommand)
Write-Host 'Yeni PowerShell penceresinde .NET proje klasörüne gidin: license-radar scan .'
Write-Host 'Komut bulunamazsa %USERPROFILE%\.dotnet\tools dizininin PATH içinde olduğunu kontrol edin.'
