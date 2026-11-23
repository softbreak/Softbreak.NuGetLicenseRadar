using Spectre.Console;
using Softbreak.LicenseRadar.Cli.Services;
using Softbreak.LicenseRadar.Cli.Models;

if (args.Length == 0)
{
    PrintHelp();
    return;
}

if (!TryParseCommandLine(
    args,
    out string targetPath,
    out string? outputPathArgument,
    out bool isVerbose,
    out bool showHelp,
    out string? argumentError))
{
    Console.Error.WriteLine($"Hata: {argumentError}");
    Console.Error.WriteLine("Kullanım: license-radar scan [dizin-veya-proje] [--output <dosya.json>] [--verbose]");
    Console.Error.WriteLine("Yardım için: license-radar --help");
    Environment.ExitCode = 2;
    return;
}

if (showHelp)
{
    PrintHelp();
    return;
}

string? outputPath = null;
if (outputPathArgument is not null)
{
    try
    {
        outputPath = Path.GetFullPath(outputPathArgument);
    }
    catch (Exception exception) when (
        exception is ArgumentException or
            NotSupportedException or
            PathTooLongException or
            System.Security.SecurityException)
    {
        Console.Error.WriteLine($"Hata: JSON çıktı yolu geçersiz: {exception.Message}");
        Environment.ExitCode = 2;
        return;
    }

    if (File.Exists(outputPath))
    {
        Console.Error.WriteLine("Hata: JSON çıktı dosyası zaten mevcut.");
        Environment.ExitCode = 1;
        return;
    }
}

var locator = new ProjectDependencyLocator();
DiscoveryResult discovery;
try
{
    discovery = locator.Discover(targetPath);
}
catch (Exception exception) when (
    exception is ArgumentException or
        NotSupportedException or
        PathTooLongException or
        UnauthorizedAccessException or
        IOException or
        System.Security.SecurityException)
{
    Console.Error.WriteLine($"Hata: Hedef yol okunamadı: {exception.Message}");
    Environment.ExitCode = 1;
    return;
}

if (!discovery.IsSuccess)
{
    Console.Error.WriteLine($"Hata: {discovery.ErrorMessage}");
    if (!string.IsNullOrWhiteSpace(discovery.SuggestedSolution))
    {
        Console.Error.WriteLine($"Öneri: {discovery.SuggestedSolution}");
    }
    Environment.ExitCode = 1;
    return;
}

var lockReader = new LockFileReader();
var assetsReader = new ProjectAssetsFileReader();
var allPackages = new List<PackageDependency>();

foreach (DiscoveredProject proj in discovery.Projects)
{
    try
    {
        IReadOnlyList<PackageDependency> projPackages = proj.SourceKind == DependencySourceKind.LockFile
            ? lockReader.Read(proj.DependencyFilePath)
            : assetsReader.Read(proj.DependencyFilePath);

        allPackages.AddRange(projPackages);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Hata: '{proj.ProjectName}' bağımlılıkları okunamadı: {ex.Message}");
        Environment.ExitCode = 1;
        return;
    }
}

// Deduplicate packages with same Name, Version, and Framework
var packages = allPackages
    .GroupBy(p => (p.Name.ToLowerInvariant(), p.Version, p.Framework))
    .Select(g => g.OrderByDescending(p =>
        p.Type.Equals("Direct", StringComparison.OrdinalIgnoreCase)).First())
    .ToList();

using var httpClient = new HttpClient(new HttpClientHandler
{
    AllowAutoRedirect = false
})
{
    Timeout = TimeSpan.FromSeconds(15)
};

var licenseReader = new NuGetLicenseReader(httpClient);
var scanService = new LicenseScanService(
    licenseReader,
    new LicenseDeclarationReviewEvaluator()
);
var decisionCombiner = new LicenseReviewDecisionCombiner(
    new LicenseReviewEvaluator()
);
var reviewResults = new List<LicenseDeclarationReviewResult>();

bool isInteractive = !Console.IsOutputRedirected &&
    Environment.GetEnvironmentVariable("NO_COLOR") is null;

if (isInteractive)
{
    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("Paket lisansları taranıyor...", async _ =>
        {
            foreach (var package in packages)
            {
                LicenseDeclarationReviewResult result =
                    await scanService.ScanAsync(package);

                reviewResults.Add(decisionCombiner.Combine(result));
            }
        });
}
else
{
    foreach (var package in packages)
    {
        LicenseDeclarationReviewResult result =
            await scanService.ScanAsync(package);

        reviewResults.Add(decisionCombiner.Combine(result));
    }
}

// NO_COLOR ve yönlendirilmiş çıktıda ANSI renk kodlarını kesin olarak devre dışı bırak.
IAnsiConsole reportConsole = Console.IsOutputRedirected ||
    Environment.GetEnvironmentVariable("NO_COLOR") is not null
    ? AnsiConsole.Create(new AnsiConsoleSettings
    {
        Out = new AnsiConsoleOutput(Console.Out),
        ColorSystem = ColorSystemSupport.NoColors
    })
    : AnsiConsole.Console;
new ConsoleReportRenderer(reportConsole).Render(targetPath, discovery.Projects, packages, reviewResults, isVerbose);

if (outputPath is not null)
{
    try
    {
        await new LicenseScanReportWriter().WriteNewAsync(
            outputPath,
            reviewResults,
            DateTimeOffset.UtcNow
        );
        Console.WriteLine($"JSON raporu kaydedildi: {outputPath}");
    }
    catch (Exception exception) when (
        exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            System.Security.SecurityException or
            System.Text.Json.JsonException)
    {
        Console.Error.WriteLine($"Hata: JSON raporu yazılamadı: {exception.Message}");
        Environment.ExitCode = 1;
    }
}

static bool TryParseCommandLine(
    string[] arguments,
    out string targetPath,
    out string? outputPath,
    out bool isVerbose,
    out bool showHelp,
    out string? error)
{
    targetPath = string.Empty;
    outputPath = null;
    isVerbose = false;
    showHelp = false;
    error = null;

    bool isScanCommand = false;

    for (int index = 0; index < arguments.Length; index++)
    {
        string arg = arguments[index];

        if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("/?", StringComparison.OrdinalIgnoreCase))
        {
            showHelp = true;
            return true;
        }

        if (arg.Equals("--verbose", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("-v", StringComparison.OrdinalIgnoreCase))
        {
            isVerbose = true;
            continue;
        }

        if (arg.Equals("--output", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("-o", StringComparison.OrdinalIgnoreCase))
        {
            if (outputPath is not null)
            {
                error = "--output birden fazla kez verilemez.";
                return false;
            }

            if (index + 1 >= arguments.Length ||
                string.IsNullOrWhiteSpace(arguments[index + 1]) ||
                arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                error = "--output için bir dosya yolu gerekli.";
                return false;
            }

            outputPath = arguments[++index];
            continue;
        }

        if (arg.StartsWith("--", StringComparison.Ordinal))
        {
            error = $"Bilinmeyen seçenek: {arg}";
            return false;
        }

        // Positional argument
        if (!isScanCommand && arg.Equals("scan", StringComparison.OrdinalIgnoreCase))
        {
            isScanCommand = true;
            continue;
        }

        if (targetPath.Length > 0)
        {
            error = "Birden fazla hedef yolu verilemez.";
            return false;
        }

        targetPath = arg;
    }

    if (isScanCommand && targetPath.Length == 0)
    {
        targetPath = ".";
    }

    if (targetPath.Length == 0)
    {
        error = "Taranacak bir proje, lock dosyası veya klasör yolu belirtilmelidir.";
        return false;
    }

    return true;
}

static void PrintHelp()
{
    Console.WriteLine("Softbreak NuGet License Radar");
    Console.WriteLine("-----------------------------");
    Console.WriteLine("Tek komutla NuGet lisanslarını inceleyin: license-radar scan .");
    Console.WriteLine();
    Console.WriteLine("KULLANIM:");
    Console.WriteLine("  license-radar scan [hedef-yol] [seçenekler]");
    Console.WriteLine("  license-radar <dosya-veya-klasör-yolu> [seçenekler]  (eski kullanım)");
    Console.WriteLine();
    Console.WriteLine("AÇIKLAMA:");
    Console.WriteLine("  .NET projelerindeki NuGet bağımlılıklarını otomatik keşfeder, lisans");
    Console.WriteLine("  dosyalarını ve bildirimlerini inceleyerek kanıta dayalı lisanslama");
    Console.WriteLine("  sinyalleri (karşılıklı lisans, alternatif sözleşme, ücretli ticari) üretir.");
    Console.WriteLine();
    Console.WriteLine("ARGÜMANLAR:");
    Console.WriteLine("  scan [hedef]                Belirtilen klasör, proje (.csproj) veya solution (.sln)");
    Console.WriteLine("                              içindeki bağımlılıkları keşfeder (varsayılan: .)");
    Console.WriteLine("  <dosya-yolu>                Doğrudan packages.lock.json veya project.assets.json yolu");
    Console.WriteLine();
    Console.WriteLine("SEÇENEKLER:");
    Console.WriteLine("  -o, --output <dosya.json>   Tarama sonucunu v1 JSON formatında kaydeder.");
    Console.WriteLine("  -v, --verbose               Tüm paketleri ve kanıt ayrıntılarını gösterir.");
    Console.WriteLine("  -h, --help                  Bu yardım açıklamasını görüntüler.");
    Console.WriteLine();
    Console.WriteLine("ÖRNEKLER:");
    Console.WriteLine("  license-radar scan .");
    Console.WriteLine("  license-radar scan ./src/MyProject");
    Console.WriteLine("  license-radar scan . --output lisans-raporu.json");
    Console.WriteLine("  license-radar scan . --verbose");
    Console.WriteLine("  license-radar ./packages.lock.json --output rapor.json");
    Console.WriteLine();
    Console.WriteLine("NOT: Sonuçlar hukuki onay veya otomatik satın alma önerisi değildir.");
}
