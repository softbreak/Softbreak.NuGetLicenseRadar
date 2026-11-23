using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public class LicenseReviewDecisionCombinerTests
{
    [Fact]
    public void Combine_AutoMapper150_ReturnsReviewRequiredWithEvidence()
    {
        LicenseDeclarationReviewResult generalReview = CreateReview(
            "AutoMapper",
            "15.0.0",
            LicenseDeclarationQueryStatus.Success,
            LicenseDeclarationKind.Expression,
            "MIT"
        );

        LicenseDeclarationReviewResult result = CreateCombiner().Combine(generalReview);

        Assert.Equal(LicenseReviewStatus.ReviewRequired, result.Status);
        Assert.Contains("AutoMapper 15.0", result.Reason);
        Assert.Equal(
            "https://docs.automapper.io/en/stable/15.0-Upgrade-Guide.html",
            result.EvidenceUrl
        );
    }

    [Fact]
    public void Combine_AutoMapper140_PreservesGeneralExpressionDecision()
    {
        LicenseDeclarationReviewResult generalReview = CreateReview(
            "AutoMapper",
            "14.0.0",
            LicenseDeclarationQueryStatus.Success,
            LicenseDeclarationKind.Expression,
            "MIT"
        );

        LicenseDeclarationReviewResult result = CreateCombiner().Combine(generalReview);

        Assert.Equal(LicenseReviewStatus.MetadataOnly, result.Status);
        Assert.Null(result.EvidenceUrl);
    }

    [Fact]
    public void Combine_FailedAutoMapperQuery_RemainsUnknown()
    {
        LicenseDeclarationReviewResult generalReview = CreateReview(
            "AutoMapper",
            "15.0.0",
            LicenseDeclarationQueryStatus.HttpFailure,
            null,
            null
        );

        LicenseDeclarationReviewResult result = CreateCombiner().Combine(generalReview);

        Assert.Equal(LicenseReviewStatus.Unknown, result.Status);
        Assert.Equal(
            LicenseDeclarationQueryStatus.HttpFailure,
            result.QueryResult.Status
        );
        Assert.Null(result.EvidenceUrl);
    }

    [Fact]
    public void Combine_FileDeclaration_PreservesReviewRequiredDecision()
    {
        LicenseDeclarationReviewResult generalReview = CreateReview(
            "AutoMapper",
            "14.0.0",
            LicenseDeclarationQueryStatus.Success,
            LicenseDeclarationKind.File,
            "LICENSE.txt"
        );

        LicenseDeclarationReviewResult result = CreateCombiner().Combine(generalReview);

        Assert.Equal(LicenseReviewStatus.ReviewRequired, result.Status);
        Assert.Contains("Lisans dosyası bildirildi", result.Reason);
        Assert.Contains("ücretli olduğu anlamına gelmez", result.Reason);
    }

    private static LicenseReviewDecisionCombiner CreateCombiner()
    {
        return new LicenseReviewDecisionCombiner(new LicenseReviewEvaluator());
    }

    private static LicenseDeclarationReviewResult CreateReview(
        string packageName,
        string version,
        LicenseDeclarationQueryStatus queryStatus,
        LicenseDeclarationKind? declarationKind,
        string? declarationValue)
    {
        var package = new PackageDependency(
            packageName,
            version,
            "Direct",
            "net10.0"
        );
        NuGetLicenseDeclaration? declaration = declarationKind is null
            ? null
            : new NuGetLicenseDeclaration(declarationKind.Value, declarationValue);
        var queryResult = new PackageLicenseDeclarationResult(
            package,
            declaration,
            queryStatus
        );

        return new LicenseDeclarationReviewEvaluator().Evaluate(queryResult);
    }
}
