using Spectre.Console;
using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public sealed class DiscoveryErrorRendererTests
{
    [Theory]
    [InlineData(60)]
    [InlineData(120)]
    public void RecursiveFailure_ReportsExactMissingProjectAndNoPartialScan(int width)
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var ready = Path.Combine(root, "Ready");
            var missing = Path.Combine(root, "Missing");
            Directory.CreateDirectory(ready);
            Directory.CreateDirectory(missing);
            File.WriteAllText(Path.Combine(ready, "Ready.csproj"), "<Project />");
            File.WriteAllText(Path.Combine(ready, "packages.lock.json"), "{}");
            string missingPath = Path.Combine(missing, "Missing.csproj");
            File.WriteAllText(missingPath, "<Project />");
            var discovery = new ProjectDependencyLocator().Discover(root, recursive: true);
            Assert.False(discovery.IsSuccess);
            Assert.Equal(missingPath, Assert.Single(discovery.UnrestoredProjectPaths!));
            using var writer = new StringWriter();
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(writer),
                ColorSystem = ColorSystemSupport.NoColors,
                Ansi = AnsiSupport.No,
                Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false }
            });
            console.Profile.Width = width;
            new DiscoveryErrorRenderer(console).Render(root, discovery, true);
            string text = writer.ToString();
            Assert.Contains("Tarama tamamlanamadı", text);
            Assert.Contains("2 proje keşfedildi", text);
            Assert.Contains("1 hazır", text);
            Assert.Contains("1 eksik", text);
            Assert.Contains("Missing.csproj", text);
            Assert.Contains("dotnet restore", text);
            Assert.Contains("--recursive", text);
            Assert.False(text.Contains('\u001b'));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
