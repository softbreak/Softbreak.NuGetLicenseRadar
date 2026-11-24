using L = Softbreak.LicenseRadar.Cli.Localization.UiText;
﻿
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
                    L.Get("Review.AutoMapperVersion"),
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
                    L.Get("Review.Preview"),
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
                    L.Get("Review.AutoMapper15"),
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
                    L.Get("Review.Expression"),
                    null
                ),

            LicenseLookupStatus.ExpressionNotFound =>
                new LicenseReviewResult(
                    licenseResult,
                    LicenseReviewStatus.Unknown,
                    L.Get("Review.NotFound"),
                    null
                ),

            LicenseLookupStatus.QueryFailed =>
                new LicenseReviewResult(
                    licenseResult,
                    LicenseReviewStatus.Unknown,
                    L.Get("Review.QueryFailed"),
                    null
                ),

            _ => new LicenseReviewResult(
                licenseResult,
                LicenseReviewStatus.Unknown,
                L.Get("Review.UnknownStatus"),
                null
            )
        };
    }
}
