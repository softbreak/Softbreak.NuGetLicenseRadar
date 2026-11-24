using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;
using Softbreak.LicenseRadar.Cli.Localization;
using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public sealed class UiLocalizationTests
{
    [Theory]
    [InlineData("tr-TR", "İnceleme gerekli: 1", "KULLANIM:")]
    [InlineData("tr-CY", "İnceleme gerekli: 1", "KULLANIM:")]
    [InlineData("en-US", "Review required: 1", "USAGE:")]
    [InlineData("en-GB", "Review required: 1", "USAGE:")]
    [InlineData("de-DE", "Review required: 1", "USAGE:")]
    public void UsesCurrentUiCulture_WithEnglishFallback(
        string uiCulture, string expectedSummary, string expectedHelp)
    {
        using var scope = new UiCultureScope(uiCulture);

        Assert.Equal(expectedSummary, UiText.Format("Renderer.ReviewCount", 1));
        Assert.Contains(expectedHelp, UiText.Get("Cli.Help"));
        Assert.Contains("license-radar scan .", UiText.Get("Cli.Help"));
    }

    [Theory]
    [InlineData("tr-TR", "İnceleme Gerektiren Paketler", "Ücretli ticari lisans ifadesi", "Toplam: 1")]
    [InlineData("en-US", "Packages Requiring Review", "Paid commercial license wording", "Total: 1")]
    public void ConsoleReport_TranslatesLabels_ButPreservesVendorEvidence(
        string uiCulture, string title, string signalLabel, string summary)
    {
        using var scope = new UiCultureScope(uiCulture);
        using var output = new StringWriter();
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Out = new AnsiConsoleOutput(output),
            ColorSystem = ColorSystemSupport.NoColors,
            Ansi = AnsiSupport.No,
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false }
        });
        console.Profile.Width = 120;

        var pkg = new PackageDependency("FluentAssertions", "8.8.0", "Direct", "net8.0");
        const string vendorEvidence =
            "Any use outside of these parameters requires a paid Commercial License from Xceed.";
        var signal = new LicenseTextSignal(
            LicenseTextSignalKind.PaidCommercialLicenseMention,
            vendorEvidence);
        var queryResult = new PackageLicenseDeclarationResult(
            pkg,
            new NuGetLicenseDeclaration(LicenseDeclarationKind.File, "LICENSE"),
            LicenseDeclarationQueryStatus.Success);

        var review = new LicenseDeclarationReviewResult(
            queryResult,
            LicenseReviewStatus.ReviewRequired,
            UiText.Get("Eval.File"),
            null,
            new LicenseFileReadResult(LicenseFileReadStatus.Found, vendorEvidence, null, [signal]),
            [signal]);

        new ConsoleReportRenderer(console).Render(
            "test/packages.lock.json",
            [pkg],
            [review]);

        string text = output.ToString();
        Assert.Contains(title, text);
        Assert.Contains(signalLabel, text);
        Assert.Contains(summary, text);
        Assert.Contains(vendorEvidence, Regex.Replace(text, @"\s+", " "));
        Assert.DoesNotContain("\u001b[", text);
    }

    [Fact]
    public void EnglishAndTurkishResources_HaveIdenticalKeys()
    {
        var manager = new ResourceManager(
            "Softbreak.LicenseRadar.Cli.Localization.Messages",
            typeof(UiText).Assembly);

        var english = manager.GetResourceSet(CultureInfo.GetCultureInfo("en"), true, true)!;
        var turkish = manager.GetResourceSet(CultureInfo.GetCultureInfo("tr"), true, true)!;

        var englishKeys = english.Cast<DictionaryEntry>()
            .Select(e => (string)e.Key).OrderBy(x => x).ToArray();
        var turkishKeys = turkish.Cast<DictionaryEntry>()
            .Select(e => (string)e.Key).OrderBy(x => x).ToArray();

        Assert.NotEmpty(englishKeys);
        Assert.Equal(englishKeys, turkishKeys);
    }

    [Fact]
    public void JsonStatusAndPropertyNames_StayInvariantAcrossLocales()
    {
        string Encode(string culture)
        {
            using var scope = new UiCultureScope(culture);
            var package = new PackageDependency("Example.Pkg", "1.2.3", "Direct", "net8.0");
            var query = new PackageLicenseDeclarationResult(
                package,
                new NuGetLicenseDeclaration(LicenseDeclarationKind.File, "LICENSE"),
                LicenseDeclarationQueryStatus.Success);
            LicenseDeclarationReviewResult review =
                new LicenseDeclarationReviewEvaluator().Evaluate(query);
            return new LicenseScanReportWriter().Serialize(
                [review],
                new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero));
        }

        string tr = Encode("tr-TR");
        string en = Encode("en-US");

        foreach (string report in new[] { tr, en })
        {
            Assert.Contains("\"schemaVersion\": 1", report);
            Assert.Contains("\"packages\"", report);
            Assert.Contains("\"reviewStatus\": \"ReviewRequired\"", report);
            Assert.Contains("\"packageName\": \"Example.Pkg\"", report);
        }

        using JsonDocument trDocument = JsonDocument.Parse(tr);
        using JsonDocument enDocument = JsonDocument.Parse(en);
        string trReason = trDocument.RootElement
            .GetProperty("packages")[0].GetProperty("reviewReason").GetString()!;
        string enReason = enDocument.RootElement
            .GetProperty("packages")[0].GetProperty("reviewReason").GetString()!;
        Assert.Contains("Lisans dosyası bildirildi", trReason);
        Assert.Contains("A license file was declared", enReason);
    }

    private sealed class UiCultureScope : IDisposable
    {
        private readonly CultureInfo _previous;

        public UiCultureScope(string name)
        {
            _previous = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
        }

        public void Dispose() => CultureInfo.CurrentUICulture = _previous;
    }
}
