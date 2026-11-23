using System.Text.Json;
using System.Text.Json.Serialization;
using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;

namespace Softbreak.LicenseRadar.Tests;

public class LicenseScanReportWriterTests
{
    [Fact]
    public void Serialize_RoundTripsSchemaTimestampAndStringEnums()
    {
        DateTimeOffset scannedAt = new(2025, 2, 3, 10, 15, 0, TimeSpan.FromHours(3));
        var writer = new LicenseScanReportWriter();

        string json = writer.Serialize(
            [CreateReview("Example.Package", "1.0.0", "net10.0")],
            scannedAt
        );

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        LicenseScanReport? report =
            JsonSerializer.Deserialize<LicenseScanReport>(json, options);

        Assert.NotNull(report);
        Assert.Equal(1, report.SchemaVersion);
        Assert.Equal(scannedAt.ToUniversalTime(), report.ScannedAtUtc);
        LicenseScanReportPackage package = Assert.Single(report.Packages);
        Assert.Equal(LicenseDeclarationKind.Expression, package.LicenseDeclarationType);
        Assert.Equal(LicenseDeclarationQueryStatus.Success, package.MetadataQueryStatus);
        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"licenseDeclarationType\": \"Expression\"", json);
        Assert.Contains("\"metadataQueryStatus\": \"Success\"", json);
    }

    [Fact]
    public void Serialize_PreservesAndSortsMultipleFrameworkRecords()
    {
        LicenseDeclarationReviewResult[] results =
        [
            CreateReview("Z.Package", "1.0.0", "net8.0"),
            CreateReview("A.Package", "1.0.0", "net9.0"),
            CreateReview("A.Package", "1.0.0", "net8.0")
        ];

        LicenseScanReport report = Deserialize(
            new LicenseScanReportWriter().Serialize(results, DateTimeOffset.UtcNow)
        );

        Assert.Collection(
            report.Packages,
            package =>
            {
                Assert.Equal("A.Package", package.PackageName);
                Assert.Equal("net8.0", package.Framework);
            },
            package =>
            {
                Assert.Equal("A.Package", package.PackageName);
                Assert.Equal("net9.0", package.Framework);
            },
            package => Assert.Equal("Z.Package", package.PackageName)
        );
    }

    [Fact]
    public void Serialize_PreservesQueryFailureStatus()
    {
        LicenseDeclarationReviewResult failedReview = CreateReview(
            "Example.Package",
            "1.0.0",
            "net10.0",
            LicenseDeclarationQueryStatus.HttpFailure,
            declaration: null,
            status: LicenseReviewStatus.Unknown
        );

        string json = new LicenseScanReportWriter().Serialize(
            [failedReview],
            DateTimeOffset.UtcNow
        );
        LicenseScanReportPackage package = Assert.Single(Deserialize(json).Packages);

        Assert.Equal(LicenseDeclarationQueryStatus.HttpFailure, package.MetadataQueryStatus);
        Assert.Null(package.LicenseDeclarationType);
        Assert.Contains("\"metadataQueryStatus\": \"HttpFailure\"", json);
    }

    [Fact]
    public void Serialize_IncludesLicenseFileStatusButNeverLicenseText()
    {
        const string privateLicenseText = "SYNTHETIC LICENSE CONTENT";
        LicenseDeclarationReviewResult fileReview = CreateReview(
            "Example.Package",
            "1.0.0",
            "net10.0",
            declaration: new NuGetLicenseDeclaration(
                LicenseDeclarationKind.File,
                "licenses/LICENSE.md"
            ),
            status: LicenseReviewStatus.ReviewRequired,
            licenseFileResult: new LicenseFileReadResult(
                LicenseFileReadStatus.Found,
                privateLicenseText
            )
        );

        string json = new LicenseScanReportWriter().Serialize(
            [fileReview],
            DateTimeOffset.UtcNow
        );
        LicenseScanReportPackage package = Assert.Single(Deserialize(json).Packages);

        Assert.Equal("licenses/LICENSE.md", package.LicenseFilePath);
        Assert.Equal(LicenseFileReadStatus.Found, package.LicenseFileReadStatus);
        Assert.DoesNotContain(privateLicenseText, json);
    }

    [Fact]
    public async Task WriteNewAsync_DoesNotOverwriteExistingFile()
    {
        string outputPath = Path.Combine(
            Path.GetTempPath(),
            $"license-scan-{Guid.NewGuid():N}.json"
        );
        const string existingContent = "keep this file";

        await File.WriteAllTextAsync(outputPath, existingContent);
        try
        {
            await Assert.ThrowsAsync<IOException>(() =>
                new LicenseScanReportWriter().WriteNewAsync(
                    outputPath,
                    [],
                    DateTimeOffset.UtcNow
                )
            );

            Assert.Equal(existingContent, await File.ReadAllTextAsync(outputPath));
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    private static LicenseScanReport Deserialize(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        return JsonSerializer.Deserialize<LicenseScanReport>(json, options)
            ?? throw new InvalidOperationException("JSON raporu okunamadı.");
    }

    private static LicenseDeclarationReviewResult CreateReview(
        string name,
        string version,
        string framework,
        LicenseDeclarationQueryStatus queryStatus = LicenseDeclarationQueryStatus.Success,
        NuGetLicenseDeclaration? declaration = null,
        LicenseReviewStatus status = LicenseReviewStatus.MetadataOnly,
        LicenseFileReadResult? licenseFileResult = null)
    {
        if (declaration is null &&
            queryStatus == LicenseDeclarationQueryStatus.Success)
        {
            declaration = new NuGetLicenseDeclaration(
                LicenseDeclarationKind.Expression,
                "MIT"
            );
        }

        var package = new PackageDependency(name, version, "Direct", framework);
        var queryResult = new PackageLicenseDeclarationResult(
            package,
            declaration,
            queryStatus
        );

        return new LicenseDeclarationReviewResult(
            queryResult,
            status,
            "Synthetic test reason.",
            null,
            licenseFileResult
        );
    }
}
