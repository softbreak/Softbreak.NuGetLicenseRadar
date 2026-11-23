
using NuGet.Versioning;
using Softbreak.LicenseRadar.Cli.Models;

namespace Softbreak.LicenseRadar.Cli.Services;

public sealed class LicenseReviewEvaluator
{
    // AutoMapper lisanslama değişikliğinin başladığı sürüm
    private static readonly NuGetVersion AutoMapperMinVersion =
        NuGetVersion.Parse("15.0.0");

    // Üreticinin resmi belgesi
    private const string AutoMapperEvidenceUrl =
        "https://docs.automapper.io/en/stable/15.0-Upgrade-Guide.html";

    public LicenseReviewResult Evaluate(
        PackageLicenseResult licenseResult)
    {
        PackageDependency package = licenseResult.Package;

        // Önce paket adını kontrol et
        if (package.Name.Equals(
            "AutoMapper",
            StringComparison.OrdinalIgnoreCase))
        {
            // Sürümü NuGet kurallarına göre ayrıştır
            if (!NuGetVersion.TryParse(
                package.Version, out var version))
            {
                return new LicenseReviewResult(
                    licenseResult,
                    LicenseReviewStatus.Unknown,
                    "AutoMapper sürümü değerlendirilemedi.",
                    AutoMapperEvidenceUrl
                );
            }

            // 15.0 önizleme sürümleri bu kuralın dışında
            if (version.IsPrerelease &&
                version.Major == 15 &&
                version.Minor == 0)
            {
                return new LicenseReviewResult(
                    licenseResult,
                    LicenseReviewStatus.Unknown,
                    "Önizleme sürümünün lisans koşulları ayrıca incelenmeli.",
                    AutoMapperEvidenceUrl
                );
            }

            // AutoMapper 15.0.0 ve sonraki sürümler
            if (VersionComparer.VersionRelease.Compare(
                version, AutoMapperMinVersion) >= 0)
            {
                return new LicenseReviewResult(
                    licenseResult,
                    LicenseReviewStatus.ReviewRequired,
                    "AutoMapper 15.0 ve sonrasında lisanslama koşulları değişti. Kullanım koşullarını kontrol edin.",
                    AutoMapperEvidenceUrl
                );
            }
        }

        // Önceden oluşturduğumuz genel kurallar
        return licenseResult.Status switch
        {
            LicenseLookupStatus.ExpressionFound =>
                new LicenseReviewResult(
                    licenseResult,
                    LicenseReviewStatus.MetadataOnly,
                    "Lisans ifadesi bulundu. Kullanım koşulları ayrıca doğrulanmadı.",
                    null
                ),

            LicenseLookupStatus.ExpressionNotFound =>
                new LicenseReviewResult(
                    licenseResult,
                    LicenseReviewStatus.Unknown,
                    "NuGet metadata'sında lisans ifadesi bulunamadı.",
                    null
                ),

            LicenseLookupStatus.QueryFailed =>
                new LicenseReviewResult(
                    licenseResult,
                    LicenseReviewStatus.Unknown,
                    "Lisans bilgisi sorgulanamadı.",
                    null
                ),

            _ => new LicenseReviewResult(
                licenseResult,
                LicenseReviewStatus.Unknown,
                "Bilinmeyen sorgu durumu.",
                null
            )
        };
    }
}
