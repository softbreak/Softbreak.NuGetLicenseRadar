using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public class LicenseChangeDetectorTests
{
    [Fact]
    public void DetectChanges_MitToApacheLicense_FindsValueChange()
    {
        PackageLicenseDeclarationResult[] previous =
        [
            CreateResult("Example.Package", "1.0.0", "net10.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.Expression,
                "MIT")
        ];
        PackageLicenseDeclarationResult[] current =
        [
            CreateResult("example.package", "1.0.0", "net10.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.Expression,
                "Apache-2.0")
        ];

        IReadOnlyList<LicenseChangeResult> changes =
            new LicenseChangeDetector().DetectChanges(previous, current);

        LicenseChangeResult change = Assert.Single(changes);
        Assert.Equal(LicenseChangeType.DeclarationValueChanged, change.ChangeType);
        Assert.Equal("1.0.0", change.PreviousPackage?.Version);
        Assert.Equal("1.0.0", change.CurrentPackage?.Version);
        Assert.Equal("MIT", change.PreviousDeclaration?.Value);
        Assert.Equal("Apache-2.0", change.CurrentDeclaration?.Value);
        Assert.False(string.IsNullOrWhiteSpace(change.Reason));
    }

    [Fact]
    public void DetectChanges_VersionOnlyChanged_DoesNotReportLicenseChange()
    {
        PackageLicenseDeclarationResult[] previous =
        [
            CreateResult("Example.Package", "1.0.0", "net10.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.Expression,
                "MIT")
        ];
        PackageLicenseDeclarationResult[] current =
        [
            CreateResult("Example.Package", "2.0.0", "net10.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.Expression,
                "MIT")
        ];

        IReadOnlyList<LicenseChangeResult> changes =
            new LicenseChangeDetector().DetectChanges(previous, current);

        LicenseChangeResult change = Assert.Single(changes);
        Assert.Equal(LicenseChangeType.PackageVersionChanged, change.ChangeType);
        Assert.Equal("1.0.0", change.PreviousPackage?.Version);
        Assert.Equal("2.0.0", change.CurrentPackage?.Version);
        Assert.Equal("MIT", change.PreviousDeclaration?.Value);
        Assert.Equal("MIT", change.CurrentDeclaration?.Value);
        Assert.Contains("tek başına lisans bildiriminin değiştiği anlamına gelmez", change.Reason);
    }

    [Fact]
    public void DetectChanges_ExpressionToFile_ReportsTypeAndValueSeparately()
    {
        PackageLicenseDeclarationResult[] previous =
        [
            CreateResult("Example.Package", "1.0.0", "net10.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.Expression,
                "MIT")
        ];
        PackageLicenseDeclarationResult[] current =
        [
            CreateResult("Example.Package", "1.0.0", "net10.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.File,
                "LICENSE.txt")
        ];

        IReadOnlyList<LicenseChangeResult> changes =
            new LicenseChangeDetector().DetectChanges(previous, current);

        Assert.Equal(2, changes.Count);
        Assert.Contains(
            changes,
            change => change.ChangeType == LicenseChangeType.DeclarationTypeChanged
        );
        Assert.Contains(
            changes,
            change => change.ChangeType == LicenseChangeType.DeclarationValueChanged
        );
        Assert.All(changes, change =>
        {
            Assert.Equal(LicenseDeclarationKind.Expression, change.PreviousDeclaration?.Kind);
            Assert.Equal(LicenseDeclarationKind.File, change.CurrentDeclaration?.Kind);
            Assert.Equal("1.0.0", change.PreviousPackage?.Version);
            Assert.Equal("1.0.0", change.CurrentPackage?.Version);
        });
    }

    [Fact]
    public void DetectChanges_LicenseInformationLost_ReportsMissing()
    {
        PackageLicenseDeclarationResult[] previous =
        [
            CreateResult("Example.Package", "1.0.0", "net10.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.Expression,
                "MIT")
        ];
        PackageLicenseDeclarationResult[] current =
        [
            CreateResult("Example.Package", "1.0.0", "net10.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.Missing,
                null)
        ];

        IReadOnlyList<LicenseChangeResult> changes =
            new LicenseChangeDetector().DetectChanges(previous, current);

        LicenseChangeResult change = Assert.Single(changes);
        Assert.Equal(LicenseChangeType.LicenseInformationRemoved, change.ChangeType);
        Assert.Equal("MIT", change.PreviousDeclaration?.Value);
        Assert.Equal(LicenseDeclarationKind.Missing, change.CurrentDeclaration?.Kind);
    }

    [Fact]
    public void DetectChanges_NewAndRemovedDependencies_ReportsBoth()
    {
        PackageLicenseDeclarationResult[] previous =
        [
            CreateResult("Removed.Package", "1.0.0", "net10.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.Expression,
                "MIT")
        ];
        PackageLicenseDeclarationResult[] current =
        [
            CreateResult("Added.Package", "1.0.0", "net10.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.Expression,
                "Apache-2.0")
        ];

        IReadOnlyList<LicenseChangeResult> changes =
            new LicenseChangeDetector().DetectChanges(previous, current);

        Assert.Equal(2, changes.Count);
        Assert.Contains(
            changes,
            change => change.ChangeType == LicenseChangeType.PackageAdded &&
                change.CurrentPackage?.Name == "Added.Package"
        );
        Assert.Contains(
            changes,
            change => change.ChangeType == LicenseChangeType.PackageRemoved &&
                change.PreviousPackage?.Name == "Removed.Package"
        );
    }

    [Theory]
    [InlineData(LicenseDeclarationQueryStatus.HttpFailure)]
    [InlineData(LicenseDeclarationQueryStatus.Timeout)]
    [InlineData(LicenseDeclarationQueryStatus.InvalidMetadata)]
    public void DetectChanges_FailedQuery_ReturnsNotComparable(
        LicenseDeclarationQueryStatus failureStatus)
    {
        PackageLicenseDeclarationResult[] previous =
        [
            CreateResult("Example.Package", "1.0.0", "net10.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.Expression,
                "MIT")
        ];
        PackageLicenseDeclarationResult[] current =
        [
            CreateResult("Example.Package", "1.0.0", "net10.0",
                failureStatus,
                LicenseDeclarationKind.Expression,
                "Apache-2.0")
        ];

        IReadOnlyList<LicenseChangeResult> changes =
            new LicenseChangeDetector().DetectChanges(previous, current);

        LicenseChangeResult change = Assert.Single(changes);
        Assert.Equal(LicenseChangeType.NotComparable, change.ChangeType);
        Assert.Equal("MIT", change.PreviousDeclaration?.Value);
        Assert.Null(change.CurrentDeclaration);
        Assert.Equal(failureStatus, change.CurrentQueryStatus);
        Assert.Contains(failureStatus.ToString(), change.Reason);
    }

    [Fact]
    public void DetectChanges_MissingDeclarationEvidence_ReturnsNotComparable()
    {
        PackageLicenseDeclarationResult[] previous =
        [
            CreateResult("Example.Package", "1.0.0", "net10.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.Expression,
                "MIT")
        ];
        PackageLicenseDeclarationResult[] current =
        [
            CreateResult("Example.Package", "1.0.0", "net10.0",
                LicenseDeclarationQueryStatus.Success,
                null,
                null)
        ];

        IReadOnlyList<LicenseChangeResult> changes =
            new LicenseChangeDetector().DetectChanges(previous, current);

        LicenseChangeResult change = Assert.Single(changes);
        Assert.Equal(LicenseChangeType.NotComparable, change.ChangeType);
        Assert.Null(change.CurrentDeclaration);
        Assert.Contains("kanıtı yok", change.Reason);
    }

    [Fact]
    public void DetectChanges_MultipleFrameworks_PreservesEachRecord()
    {
        PackageLicenseDeclarationResult[] previous =
        [
            CreateResult("Example.Package", "1.0.0", "net8.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.Expression,
                "MIT"),
            CreateResult("Example.Package", "1.0.0", "net10.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.Expression,
                "MIT")
        ];
        PackageLicenseDeclarationResult[] current =
        [
            CreateResult("example.package", "1.0.0", "net8.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.Expression,
                "MIT"),
            CreateResult("example.package", "1.0.0", "net10.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.Expression,
                "Apache-2.0")
        ];

        IReadOnlyList<LicenseChangeResult> changes =
            new LicenseChangeDetector().DetectChanges(previous, current);

        LicenseChangeResult change = Assert.Single(changes);
        Assert.Equal(LicenseChangeType.DeclarationValueChanged, change.ChangeType);
        Assert.Equal("net10.0", change.PreviousPackage?.Framework);
        Assert.Equal("net10.0", change.CurrentPackage?.Framework);
    }

    [Fact]
    public void DetectChanges_SpdxExpression_IsComparedAsOpaqueText()
    {
        PackageLicenseDeclarationResult[] previous =
        [
            CreateResult("Example.Package", "1.0.0", "net10.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.Expression,
                "MIT OR Apache-2.0")
        ];
        PackageLicenseDeclarationResult[] current =
        [
            CreateResult("Example.Package", "1.0.0", "net10.0",
                LicenseDeclarationQueryStatus.Success,
                LicenseDeclarationKind.Expression,
                "Apache-2.0 OR MIT")
        ];

        IReadOnlyList<LicenseChangeResult> changes =
            new LicenseChangeDetector().DetectChanges(previous, current);

        LicenseChangeResult change = Assert.Single(changes);
        Assert.Equal(LicenseChangeType.DeclarationValueChanged, change.ChangeType);
        Assert.Equal("MIT OR Apache-2.0", change.PreviousDeclaration?.Value);
        Assert.Equal("Apache-2.0 OR MIT", change.CurrentDeclaration?.Value);
    }

    private static PackageLicenseDeclarationResult CreateResult(
        string packageId,
        string version,
        string framework,
        LicenseDeclarationQueryStatus queryStatus,
        LicenseDeclarationKind? declarationKind,
        string? value)
    {
        var package = new PackageDependency(
            packageId,
            version,
            "Direct",
            framework
        );
        NuGetLicenseDeclaration? declaration = declarationKind is null
            ? null
            : new NuGetLicenseDeclaration(declarationKind.Value, value);

        return new PackageLicenseDeclarationResult(
            package,
            declaration,
            queryStatus
        );
    }
}
