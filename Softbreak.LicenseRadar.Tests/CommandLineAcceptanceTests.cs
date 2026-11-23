using Spectre.Console;
using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public class CommandLineAcceptanceTests
{
    [Fact]
    public void ConsoleRenderer_DefaultMode_DoesNotDumpMetadataTableAndShowsNextSteps()
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            ColorSystem = ColorSystemSupport.NoColors
        });
        console.Profile.Width = 120;

        var renderer = new ConsoleReportRenderer(console);

        var p1 = new PackageDependency("AutoMapper", "15.0.0", "Direct", "net10.0");
        var p2 = new PackageDependency("Microsoft.Extensions.Logging", "9.0.0", "Transitive", "net10.0");

        var signal = new LicenseTextSignal(
            LicenseTextSignalKind.ReciprocalLicenseMention,
            "RPL1.5 license",
            "https://opensource.org/license/rpl-1-5/"
        );
        var r1 = new LicenseDeclarationReviewResult(
            new PackageLicenseDeclarationResult(p1, new NuGetLicenseDeclaration(LicenseDeclarationKind.File, "LICENSE.md"), LicenseDeclarationQueryStatus.Success),
            LicenseReviewStatus.ReviewRequired,
            "Lisans incelemesi gerekli.",
            "https://opensource.org/license/rpl-1-5/",
            new LicenseFileReadResult(LicenseFileReadStatus.Found, "content", null, [signal]),
            [signal]
        );
        var r2 = new LicenseDeclarationReviewResult(
            new PackageLicenseDeclarationResult(p2, new NuGetLicenseDeclaration(LicenseDeclarationKind.Expression, "MIT"), LicenseDeclarationQueryStatus.Success),
            LicenseReviewStatus.MetadataOnly,
            "Metadata only.",
            null
        );

        var proj = new DiscoveredProject("DemoProject", "DemoProject.csproj", "packages.lock.json", DependencySourceKind.LockFile);

        // Render in DEFAULT mode (isVerbose: false)
        renderer.Render(".", [proj], [p1, p2], [r1, r2], isVerbose: false);

        string output = writer.ToString();

        // Must show ReviewRequired table & details
        Assert.Contains("İnceleme Gerektiren Paketler", output);
        Assert.Contains("AutoMapper", output);
        Assert.Contains("15.0.0", output);

        // Must NOT dump full "Diğer Paketler" table in default mode
        Assert.DoesNotContain("Metadata Seviyesinde İncelenen Paketler", output);
        // Instead must show summary note pointing to --verbose
        Assert.Contains("Diğer 1 pakette lisans metadata'sı bulundu", output);
        Assert.Contains("--verbose", output);

        // Must show Next Steps panel
        Assert.Contains("Sonraki adım:", output);
        Assert.False(output.Contains("ticari lisans temin edin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConsoleRenderer_VerboseMode_DumpsOtherPackagesTable()
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            ColorSystem = ColorSystemSupport.NoColors
        });
        console.Profile.Width = 120;

        var renderer = new ConsoleReportRenderer(console);

        var p = new PackageDependency("Microsoft.Extensions.Logging", "9.0.0", "Transitive", "net10.0");
        var r = new LicenseDeclarationReviewResult(
            new PackageLicenseDeclarationResult(p, new NuGetLicenseDeclaration(LicenseDeclarationKind.Expression, "MIT"), LicenseDeclarationQueryStatus.Success),
            LicenseReviewStatus.MetadataOnly,
            "Metadata only.",
            null
        );

        var proj = new DiscoveredProject("Demo", "Demo.csproj", "packages.lock.json", DependencySourceKind.LockFile);

        // Render in VERBOSE mode
        renderer.Render(".", [proj], [p], [r], isVerbose: true);

        string output = writer.ToString();

        // Must render Diğer Paketler table in verbose mode
        Assert.Contains("Metadata Seviyesinde İncelenen Paketler", output);
        Assert.Contains("Microsoft.Extensions.Logging", output);
    }

    [Fact]
    public void ConsoleRenderer_MultipleDiscoveredProjects_SummarizesDefaultHeader()
    {
        using var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(writer),
            ColorSystem = ColorSystemSupport.NoColors
        });
        console.Profile.Width = 120;

        var renderer = new ConsoleReportRenderer(console);

        var p1 = new DiscoveredProject("Core.Api", "src/Core.Api/Core.Api.csproj", "src/Core.Api/packages.lock.json", DependencySourceKind.LockFile);
        var p2 = new DiscoveredProject("Core.Worker", "src/Core.Worker/Core.Worker.csproj", "src/Core.Worker/obj/project.assets.json", DependencySourceKind.ProjectAssets);

        var pkg = new PackageDependency("TestPkg", "1.0.0", "Direct", "net10.0");
        var rev = new LicenseDeclarationReviewResult(
            new PackageLicenseDeclarationResult(pkg, null, LicenseDeclarationQueryStatus.Success),
            LicenseReviewStatus.Unknown,
            "Unknown",
            null
        );

        renderer.Render(".", [p1, p2], [pkg], [rev], isVerbose: false);

        string output = writer.ToString();

        Assert.Contains("2 proje keşfedildi", output);
        Assert.Contains("Lock: 1", output);
        Assert.Contains("Assets: 1", output);
        Assert.DoesNotContain("Core.Worker", output);
        Assert.Contains("son restore", output);
    }
}
