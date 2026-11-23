
using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public class LicenseReviewEvaluatorTests
{
    [Theory]
    [InlineData("14.0.0", LicenseReviewStatus.MetadataOnly)]
    [InlineData("15.0.0", LicenseReviewStatus.ReviewRequired)]
    [InlineData("16.0.0", LicenseReviewStatus.ReviewRequired)]
    [InlineData("15.0.0-preview.1", LicenseReviewStatus.Unknown)]
    public void Evaluate_AutoMapperVersion_ReturnsExpectedStatus(
        string version,
        LicenseReviewStatus expectedStatus)
    {
        // ARRANGE
        var package = new PackageDependency(
            "AutoMapper",
            version,
            "Direct",
            "net10.0"
        );

        var licenseResult = new PackageLicenseResult(
            package,
            "TEST-ONLY",
            LicenseLookupStatus.ExpressionFound
        );

        var evaluator = new LicenseReviewEvaluator();

        // ACT
        LicenseReviewResult result =
            evaluator.Evaluate(licenseResult);

        // ASSERT
        Assert.Equal(expectedStatus, result.Status);

        if (expectedStatus == LicenseReviewStatus.ReviewRequired)
        {
            Assert.Equal(
                "https://docs.automapper.io/en/stable/15.0-Upgrade-Guide.html",
                result.EvidenceUrl
            );

            Assert.False(string.IsNullOrWhiteSpace(result.Reason));
        }
    }
}
