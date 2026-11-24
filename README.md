# Softbreak NuGet License Radar

**.NET projenizde hangi NuGet paketlerinin lisans koşullarını ayrıca incelemeniz gerektiğini görün.**

License Radar, NuGet bağımlılıklarının lisans bildirimlerini ve paket içindeki lisans dosyalarını inceleyerek **hangi paketlerin ayrıca değerlendirilmesi gerektiğini** kanıtlarıyla gösteren açık kaynak bir .NET komut satırı aracıdır. [Spectre.Console](https://spectreconsole.net/) ile yüzlerce paketi tararken inceleme önceliklerini anlaşılır biçimde sunar.

> [!IMPORTANT]
> License Radar bir **hukuki uygunluk onay aracı değildir**. `İnceleme gerekli` ücretli lisansın kesin zorunlu olduğunu; `Metadata bulundu` ise ücretsiz kullanım veya hukuki uygunluk anlamına gelmez. Üretici koşullarını kendi kullanım senaryonuz için doğrulayın.

## Sürümler ve .NET uyumluluğu

| Sürüm | NuGet.org durumu | Aracı çalıştırabileceğiniz .NET sürümleri |
| --- | --- | --- |
| **0.1.1** | Yayımlanmış mevcut sürüm | .NET 10 |
| **0.2.0** | **Kaynak kodu `main` üzerinde hazır; NuGet.org yayını bekliyor** | .NET 6, 7, 8, 9 ve 10 |

**Önemli:** GitHub'daki `main` artık 0.2.0 kodunu içeriyor; ancak aşağıdaki `dotnet tool install` komutu **NuGet.org'da yayımlanmış sürümü** indirir. 0.2.0 yayımlanana kadar bu komutun .NET 6–9 uyumluluğu sağladığını varsaymayın.

0.2.0 paketi `net6.0;net7.0;net8.0;net9.0;net10.0` için ayrı araç bileşenleri içerir. Aracı çalıştırmak için uygun .NET çalışma zamanı ve araç kurulumunu destekleyen bir .NET SDK gerekir. Kaynak koddan derleme ve katkı geliştirme için **.NET 10 SDK** kullanın.

> **Destek notu (Kasım 2026):** Microsoft .NET 6, 7, 8 ve 9'un güvenlik ve bakım desteğini sonlandırmıştır. Geriye dönük çalışabilirlik, bu çalışma zamanlarını yeniden desteklenen sürümlere dönüştürmez. Yeni uygulamalarda .NET 10 LTS tercih edin.

**Taranan projeler:** Aracın çalıştığı .NET sürümünden bağımsız olarak .NET 6–10 projelerinin `packages.lock.json` ve `obj/project.assets.json` dosyalarındaki NuGet bağımlılıkları incelenebilir. Tarama için projeyi derlemek gerekmez; araç otomatik `restore` yapmaz. İlk araç kurulumu ve canlı NuGet sorguları internet gerektirir.

## Başlangıç: iki komut

**1. NuGet.org'da bulunan sürümü kurun** (PowerShell, CMD, Terminal):

```sh
dotnet tool install --global Softbreak.NuGetLicenseRadar
```

**2. Kendi .NET projenizin klasöründe tarayın:**

```sh
license-radar scan .
```

Yayımlanmış araç için kaynak kodu indirmeniz, Visual Studio açmanız, `Install-Local.ps1` çalıştırmanız veya `packages.lock.json` dosyasını elle aramanız gerekmez. **0.2.0 yayımlandıktan sonra** özellikle bu sürümü kurmak için `dotnet tool install --global Softbreak.NuGetLicenseRadar --version 0.2.0` komutunu kullanabilirsiniz. Daha önce araç kurduysanız `install` yerine `update` kullanın.


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

## Gerçek proje örneği: 157 bağımlılık

0.2.0 yayın adayı Windows üzerinde gerçek, çok projeli bir .NET çözümünde çalıştırıldı. Çıktı, 0.1.1 sürümünün aynı projedeki önceki bulgularıyla **birebir örtüştü**:

| Sonuç | Sayı | Açıklama |
| --- | ---: | --- |
| Keşfedilen proje | 7 | Yedi `packages.lock.json` dosyası kullanıldı. |
| Benzersiz NuGet bağımlılığı | **157** | 34 doğrudan, 123 dolaylı bağımlılık. |
| **İnceleme gerekli** | **1** | FluentAssertions **8.8.0** lisans metninde ücretli ticari lisans ifadesi tespit edildi. |
| **Doğrulanamadı** | **5** | Paketlerin NuGet metadata'sında lisans beyanı bulunamadı. |
| **Metadata bulundu** | **151** | Lisans bildirimi mevcut; hukuki uygunluk kararı verilmedi. |
| Sorgu hatası | **0** | Canlı NuGet sorgularında hata oluşmadı. |

Tespit edilen örnek kanıt: `Any use outside of these parameters requires a paid Commercial License from Xceed.` Bu ifade **o kullanım için kesin ödeme zorunluluğu tespit edildiği anlamına gelmez**; lisans koşullarının ayrıca değerlendirilmesi gerektiğini gösterir.

`Unknown` durumundaki bir paket de otomatik olarak ücretli veya hukuka aykırı sayılmaz.

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

### Uyumluluk kabul testleri

[.NET 6–10 GitHub Actions kabul çalışması](https://github.com/softbreak/Softbreak.NuGetLicenseRadar/actions/runs/45419708989) başarılıdır:

- **137 test × 5 hedef framework = 685 başarılı test** (0 başarısız).
- Beş framework'ün tamamında NuGet Tool paket bileşenleri doğrulandı; izole araç kurulumu, `--help` ve örnek lockfile taraması **5/5 PASS**.
- Windows üzerinde `scripts/Test-Release.ps1` ile paket manifesti, README, MIT lisansı ve global araç kurulumunu etkilemeyen izole kurulum doğrulandı. Gerçek projedeki 157 bağımlılığın sonuçları önceki sürümle karşılaştırıldı.

Yerel yayın adayı kontrolü için PowerShell'de:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-Release.ps1
```

İzolasyonlu beş framework kabul testi `scripts/Test-MultiFramework-Tool.ps1` içindedir ve GitHub Actions tarafından çalıştırılır. Testlerin geçmesi NuGet.org'a otomatik yayın yapmaz. Sürüm etiketleme ve NuGet.org yüklemesi ayrı bir yayın adımıdır.

## Sınırlamalar, gizlilik ve güvenlik

- Lisans metni sinyal algılaması sınırlı İngilizce örüntülerle çalışır. Üreticinin harici sayfalarındaki bütün sözleşmeler otomatik doğrulanmaz.
- Lisans dosyaları bulunması ücretli kullanım gerektirmez; lisans ifadesi bulunması da ücretsiz veya uygun kullanım kanıtı değildir.
- Çok projeli taramalarda hangi paketin hangi projeden geldiğinin bütün ilişkileri raporda tutulmaz.
- Otomatik restore, CI politika engeli, güvenlik açığı taraması ve eksiksiz SPDX uyumluluk motoru **yoktur**.
- İnternet gerekir; ağdaki sorunlar `Unknown` olarak raporlanabilir.

## Lisans

**MIT License — Copyright (c) 2026 Softbreak.** Lisans metni kaynak depodaki `LICENSE` dosyasında ve [OSI MIT lisansı sayfasında](https://opensource.org/license/mit) bulunur.

Bu MIT lisansı **License Radar'ın kendi kaynak kodu** içindir; taradığınız NuGet paketlerine herhangi bir lisans hakkı vermez. Güvenlik sorunlarını, özel bilgileri herkese açık paylaşmadan, depo yöneticileriyle iletişime geçerek bildirin.
