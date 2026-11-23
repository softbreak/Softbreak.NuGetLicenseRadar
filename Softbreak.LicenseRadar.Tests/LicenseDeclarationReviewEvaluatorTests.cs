using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public class LicenseDeclarationReviewEvaluatorTests
{
    [Fact]
    public void Evaluate_Expression_ReturnsMetadataOnlyWithoutLegalGuarantee()
    {
        PackageLicenseDeclarationResult queryResult = CreateQueryResult(
            LicenseDeclarationQueryStatus.Success,
            LicenseDeclarationKind.Expression
        );

        LicenseDeclarationReviewResult result =
            new LicenseDeclarationReviewEvaluator().Evaluate(queryResult);

        Assert.Same(queryResult, result.QueryResult);
        Assert.Equal(LicenseReviewStatus.MetadataOnly, result.Status);
        Assert.Contains("hukuki uyumluluk", result.Reason);
        Assert.Contains("ücretsiz kullanım", result.Reason);
        Assert.Null(result.EvidenceUrl);
    }

    [Fact]
    public void Evaluate_File_ReturnsReviewRequiredWithoutMarkingItPaid()
    {
        PackageLicenseDeclarationResult queryResult = CreateQueryResult(
            LicenseDeclarationQueryStatus.Success,
            LicenseDeclarationKind.File
        );

        LicenseDeclarationReviewResult result =
            new LicenseDeclarationReviewEvaluator().Evaluate(queryResult);

        Assert.Equal(LicenseReviewStatus.ReviewRequired, result.Status);
        Assert.Contains("ücretli olduğu anlamına gelmez", result.Reason);
        Assert.Null(result.EvidenceUrl);
    }

    [Fact]
    public void Evaluate_Missing_ReturnsUnknown()
    {
        PackageLicenseDeclarationResult queryResult = CreateQueryResult(
            LicenseDeclarationQueryStatus.Success,
            LicenseDeclarationKind.Missing
        );

        LicenseDeclarationReviewResult result =
            new LicenseDeclarationReviewEvaluator().Evaluate(queryResult);

        Assert.Equal(LicenseReviewStatus.Unknown, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
        Assert.Null(result.EvidenceUrl);
    }

    [Fact]
    public void Evaluate_Unsupported_ReturnsUnknown()
    {
        PackageLicenseDeclarationResult queryResult = CreateQueryResult(
            LicenseDeclarationQueryStatus.Success,
            LicenseDeclarationKind.Unsupported
        );

        LicenseDeclarationReviewResult result =
            new LicenseDeclarationReviewEvaluator().Evaluate(queryResult);

        Assert.Equal(LicenseReviewStatus.Unknown, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
        Assert.Null(result.EvidenceUrl);
    }

    [Theory]
    [InlineData(LicenseDeclarationQueryStatus.HttpFailure)]
    [InlineData(LicenseDeclarationQueryStatus.Timeout)]
    [InlineData(LicenseDeclarationQueryStatus.InvalidMetadata)]
    public void Evaluate_FailedQuery_ReturnsUnknownEvenWithExpression(
        LicenseDeclarationQueryStatus queryStatus)
    {
        PackageLicenseDeclarationResult queryResult = CreateQueryResult(
            queryStatus,
            LicenseDeclarationKind.Expression
        );

        LicenseDeclarationReviewResult result =
            new LicenseDeclarationReviewEvaluator().Evaluate(queryResult);

        Assert.Same(queryResult, result.QueryResult);
        Assert.Equal(LicenseReviewStatus.Unknown, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
        Assert.Null(result.EvidenceUrl);
    }

    private static PackageLicenseDeclarationResult CreateQueryResult(
        LicenseDeclarationQueryStatus queryStatus,
        LicenseDeclarationKind declarationKind)
    {
        var package = new PackageDependency(
            "Example.Package",
            "1.0.0",
            "Direct",
            "net10.0"
        );
        string? value = declarationKind switch
        {
            LicenseDeclarationKind.Expression => "MIT",
            LicenseDeclarationKind.File => "LICENSE.txt",
            LicenseDeclarationKind.Unsupported => "custom",
            _ => null
        };
        var declaration = new NuGetLicenseDeclaration(
            declarationKind,
            value
        );

        return new PackageLicenseDeclarationResult(
            package,
            declaration,
            queryStatus
        );
    }
}
