# Softbreak NuGet License Radar

**Dil / Language:** 🇹🇷 **Türkçe** (bu sayfa) · [🇬🇧 English](https://github.com/softbreak/Softbreak.NuGetLicenseRadar/blob/main/README.md)

[![CI](https://github.com/softbreak/Softbreak.NuGetLicenseRadar/actions/workflows/ci.yml/badge.svg)](https://github.com/softbreak/Softbreak.NuGetLicenseRadar/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/Softbreak.NuGetLicenseRadar.svg)](https://www.nuget.org/packages/Softbreak.NuGetLicenseRadar)
[![Lisans: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/softbreak/Softbreak.NuGetLicenseRadar/blob/main/LICENSE)

**NuGet bağımlılıklarınızdan hangilerinin lisans koşullarını ayrıca incelemeniz gerektiğini belirleyin.**

Softbreak NuGet License Radar, .NET projelerinde çözümlenmiş NuGet bağımlılıklarını tarayan, paketlerin lisans bildirimlerini ve erişilebilir lisans dosyalarını okuyarak incelemeye değer kanıtları gösteren açık kaynak bir komut satırı aracıdır. Araç **hukuki uygunluğu onaylamaz** veya ticari lisans satın almanın zorunlu olup olmadığına karar vermez.

## Özellikler

- .NET projelerinde ve klasör ağaçlarında doğrudan ve dolaylı bağımlılıkları keşfeder.
- Öncelikle `packages.lock.json`, yoksa `obj/project.assets.json` kullanır.
- Paket metadata'sını ve paket içindeki lisans dosyalarını [NuGet.org](https://www.nuget.org/) üzerinden sorgular.
- Ücretli ticari lisans ifadeleri, karşılıklılık lisansları ve alternatif sözleşmeler gibi metin sinyallerini işaretler.
- Kısa terminal özeti, `--verbose` ile ayrıntılı sonuçlar ve JSON raporu sunar.
- Proje dosyalarını değiştirmeden, `dotnet restore` çalıştırmadan ve kaynak kodu yüklemeden tarama yapar.

### Dil seçimi

Komut satırı aracı, işletim sisteminin **kullanıcı arayüzü dilini** (`CultureInfo.CurrentUICulture`) otomatik algılar:

- **Türkçe (`tr-*`)**: Yardım ekranı, tarama özeti, inceleme gerekçeleri, yönlendirmeler ve hatalar Türkçedir.
- **İngilizce (`en-*`)** ve diğer diller: İngilizce çıktı kullanılır (varsayılan).

Ek bir dil parametresi gerekmez. Üçüncü taraf lisans dosyalarından alınan orijinal kanıtlar çevrilmez; JSON alan adları, şema sürümü ve durum kodları (`ReviewRequired`, `Unknown`, `MetadataOnly`) sabit kalır. Kullanıcıya yönelik `reviewReason` metni seçilen dile göre değişir.

## Kurulum

NuGet.org'da **yayımlanmış son sürümü** kurun:

```bash
dotnet tool install --global Softbreak.NuGetLicenseRadar
```

Araç daha önce kurulduysa güncelleyin:

```bash
dotnet tool update --global Softbreak.NuGetLicenseRadar
```

Kullanılabilir seçenekleri görmek için `license-radar --help` çalıştırın. Komut bulunamıyorsa terminali yeniden açın ve [.NET global araçlar klasörünün](https://learn.microsoft.com/dotnet/core/tools/global-tools#install-a-global-tool) `PATH` içinde olduğundan emin olun.

### .NET uyumluluğu ve yayın durumu

| Sürüm | Yayın durumu | Aracın çalıştığı .NET sürümleri |
| --- | --- | --- |
| **0.1.1** | NuGet.org'da yayımlandı | .NET 10 |
| **0.2.0** | `main` dalında hazır; **henüz yayımlanmadı** | .NET 6, 7, 8, 9 ve 10 |

GitHub'daki `main` kodu ile NuGet.org'daki paket **şu anda aynı sürüm değildir**. 0.2.0 yayımlanana kadar NuGet.org üzerinden yapılan kurulumların .NET 6–9 çalışma zamanı desteği sağladığını varsaymayın. 0.2.0 yayın adayı [beş framework için CI kabul testlerini](https://github.com/softbreak/Softbreak.NuGetLicenseRadar/actions/runs/45419708989) ve gerçek proje taramasını geçmiştir.

Aracın üzerinde çalıştığı .NET sürümünden bağımsız olarak .NET 6–10 projelerinin bağımlılık dosyaları taranabilir. Global araç kurulumu için uyumlu bir .NET SDK, çalıştırma için uygun .NET runtime gerekir. **Kasım 2026 itibarıyla .NET 6–9'un Microsoft desteği sona ermiştir.** Bu sürümlerde çalışabilmesi, güvenlik desteğinin devam ettiği anlamına gelmez. Yeni projelerde .NET 10 LTS kullanın.

## Hızlı başlangıç

.NET projenizin veya çözümünüzün klasörüne gidin:

```bash
license-radar scan .
```

Belirli bir proje dosyasını veya klasörü de tarayabilirsiniz:

```bash
license-radar scan ./src/MyApp/MyApp.csproj
license-radar scan ./src
```

### Komutlar

| Komut | Açıklama |
| --- | --- |
| `license-radar scan .` | Bulunduğunuz klasörü tarar. |
| `license-radar scan <yol>` | Proje, çözüm konumu, lock dosyası veya klasörü tarar. |
| `license-radar scan . --verbose` | Tüm paketleri ve ek kanıtları gösterir. |
| `license-radar scan . --output report.json` | Sürümlendirilmiş JSON raporu oluşturur. |
| `license-radar --help` | Komut satırı yardımını gösterir. |

`--output` seçeneği **var olan bir dosyanın üzerine yazmaz**. Güncel JSON şema sürümü 1'dir.

## Sonuçları yorumlama

| Durum | Anlamı |
| --- | --- |
| **ReviewRequired** | Lisans dosyası veya metin sinyali manuel inceleme gerektirir. Mutlaka ödeme yapılacağı anlamına gelmez. |
| **Unknown** | Lisans bildirimi doğrulanamadı veya alınamadı. Paketin ücretli ya da uygunsuz olduğu anlamına gelmez. |
| **MetadataOnly** | Lisans metadata'sı bulundu; kullanım hakları ayrıca doğrulanmadı. |

Örneğin bir lisans metni, belirli ücretsiz kullanım koşullarının dışında ücretli ticari lisans gerektiğini belirtebilir. License Radar bu ifadeyi **inceleme kanıtı** olarak raporlar, otomatik satın alma önerisi olarak değil.

## Nasıl çalışır?

1. Proje dosyalarını ve `packages.lock.json` ya da `obj/project.assets.json` içindeki çözümlenmiş bağımlılıkları keşfeder.
2. Paketleri kimlik, sürüm ve hedef framework bilgisine göre tekilleştirir.
3. NuGet lisans bildirimlerini ve erişilebilir paket içi lisans dosyalarını okur.
4. Desteklenen metin sinyallerini değerlendirir; inceleme gerektiren veya doğrulanamayan sonuçları gösterir.

**Önemli sınırlamalar:**

- Bütün lisanslar, üreticilerin harici sözleşmeleri veya ticari politikaları yorumlanamaz.
- `Unknown` durumu eksik metadata veya ağ hatası nedeniyle oluşabilir.
- `project.assets.json` son restore işleminin sonucudur; bağımlılıklar değiştiyse restore işlemini kendiniz yapmalısınız.
- Bu geliştirme dalında `.sln` / `.slnx` hedefleri veya tek çözüm içeren dizinler yalnızca çözüm üyesi C# projelerini tarar. Birden fazla çözüm varsa açıkça birini belirtin. Çözüm yoksa dizin ağacı taranır. Tüm dizin ağacını taramak için `license-radar scan . --recursive` kullanın (mevcut dizin istisnaları geçerlidir). Kapsam içindeki eksik bağımlılık verileri taramayı durdurur.
- Araç güvenlik açığı tarayıcısı, zorunlu politika denetimi veya hukuki danışmanlık hizmeti değildir.

NuGet sorguları paket metadata'sı ve lisans içeriği için `api.nuget.org` adresine gönderilir. **Projenizin kaynak kodu yüklenmez.**

## Geliştirme

Depoyu klonlayın ve kaynak koddan derlemek için **.NET 10 SDK** kullanın:

```bash
git clone https://github.com/softbreak/Softbreak.NuGetLicenseRadar.git
cd Softbreak.NuGetLicenseRadar
dotnet restore Softbreak.NuGetLicenseRadar.slnx
dotnet build Softbreak.NuGetLicenseRadar.slnx -c Release --no-restore
dotnet test Softbreak.NuGetLicenseRadar.slnx -c Release --no-build --no-restore
```

Aracı kaynak koddan çalıştırın:

```bash
dotnet run --project Softbreak.LicenseRadar.Cli --framework net10.0 -- scan .
```

[GitHub Actions iş akışı](https://github.com/softbreak/Softbreak.NuGetLicenseRadar/blob/main/.github/workflows/ci.yml), .NET 6–10 için derleme, test, paketleme ve izole araç kurulumlarını doğrular. Windows'ta `scripts/Test-Release.ps1` betiği, global kurulumları değiştirmeden veya NuGet.org'a yayın yapmadan yayın adayını kontrol eder.

## Katkıda bulunma

Hata bildirimleri, özellik önerileri ve pull request'ler kabul edilir. Büyük değişikliklerden önce bir [GitHub issue](https://github.com/softbreak/Softbreak.NuGetLicenseRadar/issues) açarak yaklaşımı tartışın. Pull request gönderirken ilgili testleri çalıştırın ve beklenen davranışı açıklayın.

## Lisans

Bu proje **MIT Lisansı** ile dağıtılır. Ayrıntılar için [LICENSE](https://github.com/softbreak/Softbreak.NuGetLicenseRadar/blob/main/LICENSE) dosyasına bakın.

MIT lisansı **bu araca** aittir; taranan üçüncü taraf NuGet paketlerine herhangi bir lisans hakkı sağlamaz.
