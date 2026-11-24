using L = Softbreak.LicenseRadar.Cli.Localization.UiText;
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
    out bool recursive,
    out bool showHelp,
    out string? argumentError))
{
    Console.Error.WriteLine(L.Format("Cli.InvalidArguments", argumentError));
    Console.Error.WriteLine(L.Get("Cli.Usage"));
    Console.Error.WriteLine(L.Get("Cli.HelpTip"));
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
        Console.Error.WriteLine(L.Format("Cli.InvalidOutputPath", exception.Message));
        Environment.ExitCode = 2;
        return;
    }

    if (File.Exists(outputPath))
    {
        Console.Error.WriteLine(L.Get("Cli.OutputExists"));
        Environment.ExitCode = 1;
        return;
    }
}

var locator = new ProjectDependencyLocator();
DiscoveryResult discovery;
try
{
    if (recursive && File.Exists(targetPath))
    {
        Console.Error.WriteLine(L.Get("Locator.RecursiveDirectory"));
        Environment.ExitCode = 2;
        return;
    }
    discovery = locator.Discover(targetPath, recursive);
}
catch (Exception exception) when (
    exception is ArgumentException or
        NotSupportedException or
        PathTooLongException or
        UnauthorizedAccessException or
        IOException or
        System.Security.SecurityException)
{
    Console.Error.WriteLine(L.Format("Cli.UnreadablePath", exception.Message));
    Environment.ExitCode = 1;
    return;
}

if (!discovery.IsSuccess)
{
    Console.Error.WriteLine(L.Format("Cli.Error", discovery.ErrorMessage));
    if (!string.IsNullOrWhiteSpace(discovery.SuggestedSolution))
    {
        Console.Error.WriteLine(L.Format("Cli.Suggestion", discovery.SuggestedSolution));
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
        Console.Error.WriteLine(L.Format("Cli.UnreadableDependencies", proj.ProjectName, ex.Message));
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
        .StartAsync(L.Get("Cli.Scanning"), async _ =>
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
        // NoColors disables colors, but bold/underline can still emit ANSI codes.
        // Redirected output and NO_COLOR must stay plain text.
        ColorSystem = ColorSystemSupport.NoColors,
        Ansi = AnsiSupport.No,
        // GitHub Actions enrichment can switch ANSI back on, even if explicitly disabled.
        Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false }
    })
    : AnsiConsole.Console;
new ConsoleReportRenderer(reportConsole).Render(discovery.SolutionPath ?? targetPath, discovery.Projects, packages, reviewResults, isVerbose, discovery.SolutionPath is not null);

if (outputPath is not null)
{
    try
    {
        await new LicenseScanReportWriter().WriteNewAsync(
            outputPath,
            reviewResults,
            DateTimeOffset.UtcNow
        );
        Console.WriteLine(L.Format("Cli.ReportSaved", outputPath));
    }
    catch (Exception exception) when (
        exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException or
            System.Security.SecurityException or
            System.Text.Json.JsonException)
    {
        Console.Error.WriteLine(L.Format("Cli.ReportWriteFailure", exception.Message));
        Environment.ExitCode = 1;
    }
}

static bool TryParseCommandLine(
    string[] arguments,
    out string targetPath,
    out string? outputPath,
    out bool isVerbose,
    out bool recursive,
    out bool showHelp,
    out string? error)
{
    targetPath = string.Empty;
    outputPath = null;
    isVerbose = false;
    recursive = false;
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

        if (arg.Equals("--recursive", StringComparison.OrdinalIgnoreCase))
        {
            recursive = true;
            continue;
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
                error = L.Get("Cli.OutputDuplicate");
                return false;
            }

            if (index + 1 >= arguments.Length ||
                string.IsNullOrWhiteSpace(arguments[index + 1]) ||
                arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                error = L.Get("Cli.OutputRequired");
                return false;
            }

            outputPath = arguments[++index];
            continue;
        }

        if (arg.StartsWith("--", StringComparison.Ordinal))
        {
            error = L.Format("Cli.UnknownOption", arg);
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
            error = L.Get("Cli.MultiplePaths");
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
        error = L.Get("Cli.MissingTarget");
        return false;
    }

    return true;
}

static void PrintHelp() => Console.WriteLine(L.Get("Cli.Help"));
