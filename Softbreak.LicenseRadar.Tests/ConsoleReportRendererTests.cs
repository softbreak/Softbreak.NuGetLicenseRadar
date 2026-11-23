using Spectre.Console;
using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public class ConsoleReportRendererTests
{
    [Fact]
    public void Render_EscapesMarkupCharacters_DoesNotThrowFormatException()
    {
        using var stringWriter = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(stringWriter),
            ColorSystem = ColorSystemSupport.NoColors
        });
        console.Profile.Width = 120;

        var renderer = new ConsoleReportRenderer(console);

        var package = new PackageDependency(
            "[Markup.Package]<T>",
            "[1.0.0, )",
            "Direct",
            "net10.0"
        );
        var queryResult = new PackageLicenseDeclarationResult(
            package,
            new NuGetLicenseDeclaration(LicenseDeclarationKind.File, "license/[LICENSE].txt"),
            LicenseDeclarationQueryStatus.Success
        );
        var signal = new LicenseTextSignal(
            LicenseTextSignalKind.PaidCommercialLicenseMention,
            "Commercial [use] <required> without [restriction].",
            "https://example.com/license?param=[val]"
        );
        var fileResult = new LicenseFileReadResult(
            LicenseFileReadStatus.Found,
            "content",
            FailureReason: null,
            Signals: [signal]
        );
        var review = new LicenseDeclarationReviewResult(
            queryResult,
            LicenseReviewStatus.ReviewRequired,
            "Special reason with [brackets] and <angles>.",
            "https://example.com/docs/[spec]",
            fileResult,
            [signal]
        );

        var exception = Record.Exception(() => renderer.Render(
            "C:\\path\\[lock]\\packages.lock.json",
            [package],
            [review]
        ));

        Assert.Null(exception);
        string output = stringWriter.ToString();
        Assert.Contains("[Markup.Package]<T>", output);
        Assert.Contains("İnceleme Gerektiren Paketler", output);
        Assert.Contains("Ücretli ticari lisans ifadesi", output);
        Assert.Contains("Commercial [use] <required> without [restriction].", output);
    }

    [Fact]
    public void Render_ReviewRequiredAndOtherPackages_OutputsBothSections()
    {
        using var stringWriter = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(stringWriter),
            ColorSystem = ColorSystemSupport.NoColors
        });
        console.Profile.Width = 120;

        var renderer = new ConsoleReportRenderer(console);

        var revPkg = new PackageDependency("AutoMapper", "15.0.0", "Direct", "net10.0");
        var revQuery = new PackageLicenseDeclarationResult(
            revPkg,
            new NuGetLicenseDeclaration(LicenseDeclarationKind.File, "LICENSE.md"),
            LicenseDeclarationQueryStatus.Success
        );
        var signal = new LicenseTextSignal(
            LicenseTextSignalKind.ReciprocalLicenseMention,
            "Reciprocal Public License 1.5",
            "https://opensource.org/license/rpl-1-5/"
        );
        var revResult = new LicenseDeclarationReviewResult(
            revQuery,
            LicenseReviewStatus.ReviewRequired,
            "AutoMapper 15.0 ve sonrasında lisanslama koşulları değişti.",
            "https://docs.automapper.io/en/stable/15.0-Upgrade-Guide.html",
            new LicenseFileReadResult(LicenseFileReadStatus.Found, "content", null, [signal]),
            [signal]
        );

        var otherPkg = new PackageDependency("Microsoft.Extensions.Logging", "9.0.0", "Transitive", "net10.0");
        var otherQuery = new PackageLicenseDeclarationResult(
            otherPkg,
            new NuGetLicenseDeclaration(LicenseDeclarationKind.Expression, "MIT"),
            LicenseDeclarationQueryStatus.Success
        );
        var otherResult = new LicenseDeclarationReviewResult(
            otherQuery,
            LicenseReviewStatus.MetadataOnly,
            "Metadata only.",
            null
        );

        renderer.Render(
            "C:\\repo\\packages.lock.json",
            [revPkg, otherPkg],
            [revResult, otherResult]
        );

        string output = stringWriter.ToString();

        Assert.Contains("İnceleme Gerektiren Paketler", output);
        Assert.Contains("AutoMapper", output);
        Assert.Contains("15.0.0", output);
        Assert.Contains("Karşılıklı lisans", output);
        Assert.Contains("https://opensource.org/license/rpl-1-5/", output);
        Assert.Contains("https://docs.automapper.io/en/stable/15.0-Upgrade-Guide.html", output);

        Assert.Contains("Metadata Seviyesinde İncelenen Paketler", output);
        Assert.Contains("Microsoft.Extensions.Logging", output);
        Assert.Contains("MIT", output);
    }

    [Fact]
    public void Render_SummaryCounts_DisplaysExpectedNumbers()
    {
        using var stringWriter = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(stringWriter),
            ColorSystem = ColorSystemSupport.NoColors
        });
        console.Profile.Width = 120;

        var renderer = new ConsoleReportRenderer(console);

        var p1 = new PackageDependency("Direct.Pkg", "1.0.0", "Direct", "net10.0");
        var p2 = new PackageDependency("Transitive.Pkg", "2.0.0", "Transitive", "net10.0");

        var r1 = new LicenseDeclarationReviewResult(
            new PackageLicenseDeclarationResult(p1, null, LicenseDeclarationQueryStatus.Success),
            LicenseReviewStatus.ReviewRequired,
            "Review reason",
            null
        );
        var r2 = new LicenseDeclarationReviewResult(
            new PackageLicenseDeclarationResult(p2, null, LicenseDeclarationQueryStatus.Success),
            LicenseReviewStatus.Unknown,
            "Unknown reason",
            null
        );

        renderer.Render("C:\\test\\packages.lock.json", [p1, p2], [r1, r2]);

        string output = stringWriter.ToString();

        Assert.Contains("Toplam: 2", output);
        Assert.Contains("Doğrudan: 1", output);
        Assert.Contains("Dolaylı: 1", output);
        Assert.Contains("İnceleme gerekli: 1", output);
        Assert.Contains("Doğrulanamadı: 1", output);
    }

    [Fact]
    public void Render_NoColor_ProducesCleanOutputWithoutAnsiEscapeCodes()
    {
        using var stringWriter = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(stringWriter),
            ColorSystem = ColorSystemSupport.NoColors,
            Ansi = AnsiSupport.No
        });
        console.Profile.Width = 120;

        var renderer = new ConsoleReportRenderer(console);
        var p = new PackageDependency("Pkg", "1.0.0", "Direct", "net10.0");
        var r = new LicenseDeclarationReviewResult(
            new PackageLicenseDeclarationResult(p, null, LicenseDeclarationQueryStatus.Success),
            LicenseReviewStatus.MetadataOnly,
            "reason",
            null
        );

        renderer.Render("C:\\test\\packages.lock.json", [p], [r]);

        string output = stringWriter.ToString();
        Assert.DoesNotContain("\u001b[", output);
    }

    [Fact]
    public void Render_NarrowTerminal_RendersWithoutErrors()
    {
        using var stringWriter = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(stringWriter),
            ColorSystem = ColorSystemSupport.NoColors
        });
        console.Profile.Width = 50;

        var renderer = new ConsoleReportRenderer(console);

        var p = new PackageDependency("A.Very.Long.Package.Name.That.Wraps", "1.0.0", "Direct", "net10.0");
        var r = new LicenseDeclarationReviewResult(
            new PackageLicenseDeclarationResult(p, null, LicenseDeclarationQueryStatus.Success),
            LicenseReviewStatus.ReviewRequired,
            "A long review reason that should be formatted safely.",
            "https://example.com/evidence"
        );

        var exception = Record.Exception(() => renderer.Render("C:\\short\\p.json", [p], [r]));

        Assert.Null(exception);
        Assert.NotEmpty(stringWriter.ToString());
    }
}
