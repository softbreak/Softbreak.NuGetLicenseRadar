using Spectre.Console;
using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public class ConsoleReportExperienceTests
{
    [Fact]
    public void DefaultReport_Realistic152Packages_Distinguishes150MetadataFromOneUnknown()
    {
        using var output = new StringWriter();
        var renderer = CreateRenderer(output);

        var packages = Enumerable.Range(1, 150)
            .Select(i => new PackageDependency($"Library.{i}", "1.0.0", "Transitive", "net10.0"))
            .ToList();
        var fraction = new PackageDependency("Fractions", "7.3.0", "Transitive", "net10.0");
        var xunit = new PackageDependency("xunit.abstractions", "2.0.3", "Transitive", "net10.0");
        packages.AddRange([fraction, xunit]);

        var results = packages.Take(150)
            .Select(p => Review(p, LicenseReviewStatus.MetadataOnly, "Metadata bulundu", LicenseDeclarationKind.Expression, "MIT"))
            .ToList();
        results.Add(Review(fraction, LicenseReviewStatus.ReviewRequired,
            "Lisans dosyası bildirildi; koşulları inceleyin.", LicenseDeclarationKind.File, "license.txt"));
        results.Add(Review(xunit, LicenseReviewStatus.Unknown,
            "NuGet metadata'sında lisans beyanı bulunamadı."));

        renderer.Render(".", [], packages, results, isVerbose: false);
        string text = output.ToString();

        Assert.Contains("Toplam: 152", text);
        Assert.Contains("İnceleme gerekli: 1", text);
        Assert.Contains("Doğrulanamadı: 1", text);
        Assert.Contains("Lisans beyanı mevcut: 150", text);
        Assert.Contains("Fractions 7.3.0", text);
        Assert.Contains("xunit.abstractions 2.0.3", text);
        Assert.Contains("NuGet metadata'sında lisans beyanı bulunamadı", text);
        Assert.Contains("Diğer 150 pakette lisans beyanı bulundu", text);
        Assert.DoesNotContain("Diğer 151 paket", text);
        Assert.DoesNotContain("Library.1 1.0.0", text);
        Assert.False(text.Contains("ticari lisans temin edin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DefaultReport_HttpFailure_ListsUnknownPackageAndFailureReason()
    {
        using var output = new StringWriter();
        var renderer = CreateRenderer(output);
        var package = new PackageDependency("Network.Package", "2.0.0", "Direct", "net10.0");
        var result = new LicenseDeclarationReviewResult(
            new PackageLicenseDeclarationResult(package, null, LicenseDeclarationQueryStatus.Timeout),
            LicenseReviewStatus.Unknown, "NuGet yanıt vermedi.", null);

        renderer.Render(".", [], [package], [result], isVerbose: false);
        string text = output.ToString();

        Assert.Contains("Sorgu hatası: 1", text);
        Assert.Contains("Network.Package 2.0.0", text);
        Assert.Contains("Zaman aşımı", text);
        Assert.Contains("NuGet yanıt vermedi", text);
        Assert.DoesNotContain("Diğer 1 pakette lisans beyanı bulundu", text);
    }

    [Fact]
    public void DefaultReport_KeepsEvidenceShort_ButVerboseShowsFullEvidence()
    {
        var package = new PackageDependency("Vendor.Package", "8.0.0", "Direct", "net10.0");
        string longEvidence = "Any use outside of the permitted scope requires a paid commercial license. " + new string('X', 250);
        var signal = new LicenseTextSignal(LicenseTextSignalKind.PaidCommercialLicenseMention, longEvidence);
        var result = new LicenseDeclarationReviewResult(
            new PackageLicenseDeclarationResult(package,
                new NuGetLicenseDeclaration(LicenseDeclarationKind.File, "LICENSE.md"),
                LicenseDeclarationQueryStatus.Success),
            LicenseReviewStatus.ReviewRequired,
            "Lisans metninde ticari kullanım koşulu var.",
            null,
            new LicenseFileReadResult(LicenseFileReadStatus.Found, longEvidence, null, [signal]),
            [signal]);

        using var normalOutput = new StringWriter();
        CreateRenderer(normalOutput).Render(".", [], [package], [result], isVerbose: false);
        string normal = normalOutput.ToString();
        Assert.Contains("Ücretli ticari lisans ifadesi", normal);
        Assert.DoesNotContain(new string('X', 200), normal);
        Assert.Contains("Kanıt", normal);

        using var verboseOutput = new StringWriter();
        CreateRenderer(verboseOutput).Render(".", [], [package], [result], isVerbose: true);
        Assert.Contains(new string('X', 25), verboseOutput.ToString());
        Assert.True(verboseOutput.ToString().Length > normalOutput.ToString().Length);
        Assert.DoesNotContain("ReciprocalLicenseMention", normal);
    }

    [Fact]
    public void DefaultReport_ManyReviews_ShowsAllPackageNamesButLimitsEvidenceDetails()
    {
        using var output = new StringWriter();
        var renderer = CreateRenderer(output);
        var packages = Enumerable.Range(1, 8)
            .Select(i => new PackageDependency($"Review.{i}", "1.0.0", "Direct", "net10.0"))
            .ToList();
        var results = packages.Select(p => Review(p, LicenseReviewStatus.ReviewRequired,
            "Dosya incelemesi gerekli.", LicenseDeclarationKind.File, "LICENSE.md")).ToList();

        renderer.Render(".", [], packages, results, isVerbose: false);
        string text = output.ToString();
        Assert.Contains("Review.8 1.0.0", text); // all names in the table
        Assert.Contains("3 ek inceleme gerekçesi", text);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(120)]
    public void Report_PlainTerminal_PreservesFrameworksAndVersionedReferences(int width)
    {
        using var output = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(output),
            ColorSystem = ColorSystemSupport.NoColors,
            Ansi = AnsiSupport.No,
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false }
        });
        console.Profile.Width = width;
        var packages = new[]
        {
            new PackageDependency("Vendor.Pkg", "1.2.3", "Direct", "net8.0"),
            new PackageDependency("Vendor.Pkg", "1.2.3", "Direct", "net10.0"),
            new PackageDependency("Unknown.Pkg", "2.0.0", "Transitive", "net8.0")
        };
        var results = new[]
        {
            Review(packages[0], LicenseReviewStatus.ReviewRequired, "Lisans dosyası", LicenseDeclarationKind.File, "LICENSE"),
            Review(packages[1], LicenseReviewStatus.ReviewRequired, "Lisans dosyası", LicenseDeclarationKind.File, "LICENSE"),
            Review(packages[2], LicenseReviewStatus.Unknown, "Bildirim yok")
        };
        new ConsoleReportRenderer(console).Render("App.slnx", [], packages, results, false, true);
        string text = output.ToString();
        Assert.Contains("App.slnx", text);
        Assert.Contains("net8.0", text);
        Assert.Contains("net10.0", text);
        Assert.Contains("https://www.nuget.org/packages/Vendor.Pkg/1.2.3/License", text);
        Assert.Contains("https://www.nuget.org/packages/Unknown.Pkg/2.0.0", text);
        Assert.DoesNotContain("https://www.nuget.org/packages/Unknown.Pkg/2.0.0/License", text);
        // ESC is ignorable in some culture-sensitive string comparisons.
        Assert.False(text.Contains('\u001b'), "Plain terminal output must not contain an ESC character.");
    }

    private static ConsoleReportRenderer CreateRenderer(StringWriter output)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(output),
            ColorSystem = ColorSystemSupport.NoColors
        });
        console.Profile.Width = 120;
        return new ConsoleReportRenderer(console);
    }

    private static LicenseDeclarationReviewResult Review(
        PackageDependency package,
        LicenseReviewStatus status,
        string reason,
        LicenseDeclarationKind? kind = null,
        string? value = null)
    {
        NuGetLicenseDeclaration? declaration = kind.HasValue
            ? new NuGetLicenseDeclaration(kind.Value, value)
            : null;
        return new LicenseDeclarationReviewResult(
            new PackageLicenseDeclarationResult(package, declaration, LicenseDeclarationQueryStatus.Success),
            status,
            reason,
            null);
    }
}
