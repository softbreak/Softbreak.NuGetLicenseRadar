# Softbreak NuGet License Radar

**.NET projenizde hangi NuGet paketlerinin lisans koşullarını ayrıca incelemeniz gerektiğini görün.**

License Radar, NuGet bağımlılıklarının lisans bildirimlerini ve paket içindeki lisans dosyalarını inceleyen, anlaşılır kanıtlar sunan bir **.NET 10 komut satırı aracıdır**. [Spectre.Console](https://spectreconsole.net/) ile yüzlerce paketin bulunduğu projelerde bile öncelikle inceleme ve doğrulama gerektirenleri gösterir.

> [!IMPORTANT]
> License Radar bir **hukuki uygunluk onay aracı değildir**. `İnceleme gerekli` ücretli lisansın kesin zorunlu olduğunu; `Metadata bulundu` ise ücretsiz kullanım veya hukuki uygunluk anlamına gelmez. Üretici koşullarını kendi kullanım senaryonuz için doğrulayın.

## Başlangıç: iki komut

**Çalıştırma uyumluluğu (v0.2.0 yayın adayı):** `.NET 6, 7, 8, 9 ve 10` çalışma zamanları için ayrı .NET Tool bileşenleri içerir. Bilgisayarda kullanılacak sürüme uygun bir .NET çalışma zamanı ve `dotnet tool` kurulumunu destekleyen .NET SDK bulunmalıdır. Kaynak koddan derleme ve katkı geliştirme için **.NET 10 SDK** kullanın. İlk kurulum ve canlı NuGet lisans sorguları için internet gerekir.

> **Destek notu (Kasım 2026):** Microsoft .NET 6, 7, 8 ve 9 için güvenlik ve bakım desteğini sonlandırdı. Bu sürümlerle uyumluluk sağlamak, Microsoft tarafından desteklendikleri anlamına gelmez. Yeni ve üretim projelerinde .NET 10 LTS önerilir.

**Taranan projeler:** Çalıştırdığınız aracın .NET sürümünden bağımsız olarak, `net6.0`–`net10.0` projelerine ait `packages.lock.json` veya `obj/project.assets.json` bağımlılık kayıtları okunabilir. Araç projeyi yeniden derlemez veya otomatik restore yapmaz.

**1. Bir kez kurun** (PowerShell, CMD, Terminal):

```sh
dotnet tool install --global Softbreak.NuGetLicenseRadar
```

**2. Kendi .NET projenizin klasöründe tarayın:**

```sh
license-radar scan .
```

Kaynak kodu indirmeniz, Visual Studio açmanız, `Install-Local.ps1` çalıştırmanız veya `packages.lock.json` dosyasını elle aramanız gerekmez.


### Sık kullanılan komutlar

| Yapmak istediğiniz | Komut |
| --- | --- |
| Bulunduğunuz projeyi/klasörü tara | `license-radar scan .` |
| Başka bir klasörü tara | `license-radar scan "D:\Projects\MyApp"` |
| Tek `.csproj` dosyasını tara | `license-radar scan .\src\MyApp\MyApp.csproj` |
| Bütün paketleri ve ayrıntılı kanıtları gör | `license-radar scan . --verbose` |
| JSON raporu oluştur | `license-radar scan . --output report.json` |
| Kullanımı öğren | `license-radar --help` |

`--output` **var olan dosyanın üzerine yazmaz**. Aynı hedefe yeniden rapor almak için yeni dosya adı seçin.

### Güncelleme ve kaldırma

```sh
dotnet tool update --global Softbreak.NuGetLicenseRadar
dotnet tool uninstall --global Softbreak.NuGetLicenseRadar
```

`license-radar` komutu kurulumdan sonra bulunamazsa yeni terminal açın. Gerekirse global araçlar klasörünün (`%USERPROFILE%\.dotnet\tools` veya Linux/macOS'ta `$HOME/.dotnet/tools`) `PATH` üzerinde olduğundan emin olun.

## Örnek: 152 paket içeren çözüm

Windows kabul testinde, 10 proje içeren bir .NET çözümünde 152 paket kaydı değerlendirildi:

| Sonuç | Sayı | Ne anlama gelir? |
| --- | ---: | --- |
| İnceleme gerekli | 1 | Lisans dosyası veya sinyal için ek inceleme gerekir. |
| Doğrulanamadı | 1 | NuGet lisans bildirimi eksik veya sorgu sonuçlandırılamadı. |
| Metadata seviyesinde | 150 | Lisans ifadesi bulundu; hukuki uygunluk doğrulanmadı. |

Bu taramada **Fractions 7.3.0** paketinin `license.txt` dosyası inceleme gerektirdi; **xunit.abstractions 2.0.3** için lisans metadata'sı bulunamadı. Lisans dosyasının bulunması **otomatik ücretli lisans kararı değildir**.

## Tarama nasıl çalışır?

- İlgili klasördeki `.csproj` dosyalarını bulur; önce `packages.lock.json`, yoksa `obj/project.assets.json` kullanır. Bu dosyalardan hiçbiri olmayan proje varsa kullanıcıya restore önerisi verir; **sessizce restore çalıştırmaz ve proje dosyalarını değiştirmez**.
- Aynı paket kimliği, sürüm ve hedef framework kombinasyonlarını tekilleştirir.
- NuGet'in herkese açık paket metadata'sını (`.nuspec`) ve dosya lisansı bildirimi varsa paket içindeki lisans dosyasını `api.nuget.org` üzerinden sorgular. Kaynak kodunuzu sunucuya yüklemez.
- Metinde karşılıklılık lisansı, alternatif sözleşme veya ücretli ticari lisans ifadeleri gibi **sinyalleri ve kısa kanıtları** belirler. Bunlar nihai hukuki kararlar değildir.
- Varsayılan ekranda **inceleme gerektiren** ve **doğrulanamayan** paketleri gösterir. `--verbose` tüm bağımlılıkları ve ayrıntılı kanıtları açar.

Bir `.sln` veya `.slnx` dosyası hedeflendiğinde mevcut uygulama çözüm üyelerini ayrıştırmaz; ilgili dosyanın bulunduğu klasör ağacını tarar. `project.assets.json` en son restore işlemini yansıtır; proje sonradan değiştiyse önce restore edilmelidir.

### Durumları yorumlama

| Konsol durumu | Gerçek anlamı |
| --- | --- |
| **İnceleme gerekli** (`ReviewRequired`) | Lisans dosyası, metin sinyali veya bilinen inceleme kuralı nedeniyle koşullar ayrıca incelenmeli. |
| **Doğrulanamadı** (`Unknown`) | Lisans bildirimi eksik, desteklenmeyen türde veya sorgu başarısız. |
| **Metadata bulundu** (`MetadataOnly`) | Örneğin MIT gibi lisans metadata'sı bulundu; kullanımınıza uygunluk ayrıca doğrulanmadı. |

Sinyal örnekleri: AutoMapper 15 / MediatR 13 lisans metinlerinde RPL 1.5 ve alternatif lisans sözleşmesi; FluentAssertions 8'de kullanım koşullarına bağlı ücretli ticari lisans ifadesi. Algılayıcı tüm olası dilleri, lisans modellerini veya üretici politikalarını kapsamaz.

## JSON raporu

```sh
license-radar scan . --output license-report.json
```

JSON şema sürümü **1**'dir. Örneğin `schemaVersion`, `scannedAtUtc` ve `packages` alanlarını içerir; paket kayıtlarında `packageName`, `version`, `reviewStatus`, `reviewReason`, `evidenceUrl` gibi alanlar vardır. Tam lisans metni JSON'a eklenmez. JSON çıktısını başkalarıyla paylaşmadan önce içeriğini gözden geçirin.

## Geliştiriciler için

Kaynak koddan çalıştırmak isteyenler:

```sh
dotnet restore Softbreak.NuGetLicenseRadar.slnx
dotnet test Softbreak.NuGetLicenseRadar.slnx --configuration Release --framework net10.0
dotnet run --project Softbreak.LicenseRadar.Cli --framework net10.0 -- scan .
```

Solution iki proje içerir: `Softbreak.LicenseRadar.Cli` ve `Softbreak.LicenseRadar.Tests`. Kaynak klasörü üzerinden yerel kurulum betiği `scripts/Install-Local.ps1` yalnızca geliştirme ve test senaryoları içindir; **son kullanıcı kurulumu için gerekmez**.

Windows'ta beş çalışma zamanında izole kurulum ve tarama kabul testi, GitHub Actions içinde `scripts/Test-MultiFramework-Tool.ps1` ile yapılır. Yerel sistemde yalnızca kurulu .NET 10 çalışma zamanına bağlı Release testlerini, beş hedef framework'ün paket dosyalarını, NuGet metadata'sını ve global araç kurulumunu değiştirmeyen izole kurulumu doğrulamak için `powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Release.ps1` komutunu çalıştırın. Betik NuGet.org'a yayın yapmaz.

## Sınırlamalar, gizlilik ve güvenlik

- Lisans metni sinyal algılaması sınırlı İngilizce örüntülerle çalışır. Üreticinin harici sayfalarındaki bütün sözleşmeler otomatik doğrulanmaz.
- Lisans dosyaları bulunması ücretli kullanım gerektirmez; lisans ifadesi bulunması da ücretsiz veya uygun kullanım kanıtı değildir.
- Çok projeli taramalarda hangi paketin hangi projeden geldiğinin bütün ilişkileri raporda tutulmaz.
- Otomatik restore, CI politika engeli, güvenlik açığı taraması ve eksiksiz SPDX uyumluluk motoru **yoktur**.
- İnternet gerekir; ağdaki sorunlar `Unknown` olarak raporlanabilir.

## Lisans

**MIT License — Copyright (c) 2026 Softbreak.** Lisans metni kaynak depodaki `LICENSE` dosyasında ve [OSI MIT lisansı sayfasında](https://opensource.org/license/mit) bulunur.

Bu MIT lisansı **License Radar'ın kendi kaynak kodu** içindir; taradığınız NuGet paketlerine herhangi bir lisans hakkı vermez. Güvenlik sorunlarını, özel bilgileri herkese açık paylaşmadan, depo yöneticileriyle iletişime geçerek bildirin.
