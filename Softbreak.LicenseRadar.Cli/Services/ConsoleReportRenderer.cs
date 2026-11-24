using L = Softbreak.LicenseRadar.Cli.Localization.UiText;
using Spectre.Console;
using Softbreak.LicenseRadar.Cli.Models;

namespace Softbreak.LicenseRadar.Cli.Services;

/// <summary>
/// Kullanıcıya inceleme önceliklerini gösterir. Hukuki uygunluk kararı vermez.
/// Varsayılan mod kısa; --verbose kanıtları ve bütün paketleri gösterir.
/// </summary>
public sealed class ConsoleReportRenderer
{
    private const int DefaultEvidenceLength = 145;
    private const int DefaultDetailsLimit = 5;
    private readonly IAnsiConsole _console;

    public ConsoleReportRenderer(IAnsiConsole? console = null)
    {
        _console = console ?? AnsiConsole.Console;
    }

    // Önceki çağıranlar için imza ve ayrıntılı rapor davranışı korunur.
    public void Render(
        string lockFilePath,
        IReadOnlyList<PackageDependency> packages,
        IReadOnlyList<LicenseDeclarationReviewResult> reviewResults)
    {
        var project = new DiscoveredProject(
            Path.GetFileNameWithoutExtension(lockFilePath),
            lockFilePath,
            lockFilePath,
            DependencySourceKind.LockFile);

        Render(lockFilePath, [project], packages, reviewResults, isVerbose: true);
    }

    public void Render(
        string targetPath,
        IReadOnlyList<DiscoveredProject> projects,
        IReadOnlyList<PackageDependency> packages,
        IReadOnlyList<LicenseDeclarationReviewResult> reviewResults,
        bool isVerbose, bool solutionScope = false)
    {
        ArgumentGuards.ThrowIfNullOrWhiteSpace(targetPath);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(reviewResults);

        var reviews = reviewResults
            .Where(r => r.Status == LicenseReviewStatus.ReviewRequired)
            .ToList();
        var unknown = reviewResults
            .Where(r => r.Status == LicenseReviewStatus.Unknown)
            .ToList();
        var metadata = reviewResults
            .Where(r => r.Status == LicenseReviewStatus.MetadataOnly)
            .ToList();

        RenderHeader(targetPath, projects, isVerbose, solutionScope);
        RenderSummary(packages, reviewResults, reviews.Count, unknown.Count, metadata.Count);

        if (reviews.Count > 0)
        {
            RenderReviewTable(reviews);
            RenderReviewDetails(reviews, isVerbose);
        }

        if (unknown.Count > 0)
        {
            RenderUnknownTable(unknown);
        }

        // MetadataOnly, Unknown ve ReviewRequired birbirinden kesin biçimde ayrılır.
        if (!isVerbose && metadata.Count > 0)
        {
            _console.MarkupLine($"[grey]{Markup.Escape(L.Format("Renderer.OtherMetadata", metadata.Count))}[/]");
        }
        else if (isVerbose && metadata.Count > 0)
        {
            RenderMetadataTable(metadata);
        }

        if (reviews.Count == 0 && unknown.Count == 0)
        {
            _console.MarkupLine($"[grey]{Markup.Escape(L.Get("Renderer.NoSignals"))}[/]");
        }

        RenderNextSteps(reviews.Count, unknown.Count, isVerbose);
    }

    private void RenderHeader(string targetPath, IReadOnlyList<DiscoveredProject> projects, bool isVerbose, bool solutionScope)
    {
        _console.WriteLine();
        _console.Write(new Rule("[bold]Softbreak NuGet License Radar[/]") { Justification = Justify.Left });
        _console.MarkupLine($"[grey]{L.Get("Renderer.Target")}[/] {Markup.Escape(targetPath)}");

        _console.MarkupLine($"[cyan]{Markup.Escape(L.Get(solutionScope ? "Renderer.SolutionScope" : "Renderer.PathScope"))}[/]");

        if (projects.Count == 1)
        {
            var p = projects[0];
            _console.MarkupLine($"[grey]{L.Get("Renderer.Project")}[/] {Markup.Escape(p.ProjectName)} [grey]({SourceLabel(p.SourceKind)})[/]");
        }
        else if (projects.Count > 1)
        {
            int lockCount = projects.Count(p => p.SourceKind == DependencySourceKind.LockFile);
            int assetsCount = projects.Count - lockCount;
            _console.MarkupLine($"[grey]{Markup.Escape(L.Format("Renderer.ProjectsDiscovered", projects.Count, lockCount, assetsCount))}[/]");
            if (isVerbose)
            {
                foreach (var p in projects)
                {
                    _console.MarkupLine($"  [grey]•[/] {Markup.Escape(p.ProjectName)} [grey]({SourceLabel(p.SourceKind)})[/]");
                }
            }
        }

        if (projects.Any(p => p.SourceKind == DependencySourceKind.ProjectAssets))
        {
            _console.MarkupLine($"[grey]{Markup.Escape(L.Get("Renderer.AssetsNote"))}[/]");
        }
    }

    private void RenderSummary(
        IReadOnlyList<PackageDependency> packages,
        IReadOnlyList<LicenseDeclarationReviewResult> results,
        int reviews,
        int unknown,
        int metadata)
    {
        int direct = packages.Count(p => p.Type.Equals("Direct", StringComparison.OrdinalIgnoreCase));
        int transitive = packages.Count(p => p.Type.Equals("Transitive", StringComparison.OrdinalIgnoreCase));
        int failures = results.Count(r => r.QueryResult.Status != LicenseDeclarationQueryStatus.Success);

        _console.WriteLine();
        _console.MarkupLine($"[bold]{L.Format("Renderer.Total", packages.Count)}[/]  [grey]{L.Format("Renderer.Direct", direct)} · {L.Format("Renderer.Transitive", transitive)}[/]");
        _console.MarkupLine($"[yellow]{L.Format("Renderer.ReviewCount", reviews)}[/]  [bold]{L.Format("Renderer.UnknownCount", unknown)}[/]  [grey]{L.Format("Renderer.MetadataCount", metadata)}[/]");
        _console.MarkupLine(failures > 0
            ? $"[red]{L.Format("Renderer.QueryFailures", failures)}[/] [grey]{L.Get("Renderer.QueryFailuresIncluded")}[/]"
            : $"[grey]{L.Format("Renderer.QueryFailures", 0)}[/]");
        _console.WriteLine();
    }

    private void RenderReviewTable(IReadOnlyList<LicenseDeclarationReviewResult> items)
    {
        var table = new Table
        {
            Border = TableBorder.Simple,
            Title = new TableTitle($"[bold yellow]{L.Format("Renderer.ReviewTable", items.Count)}[/]")
        };
        table.AddColumn($"[bold]{L.Get("Renderer.PackageVersion")}[/]");
        table.AddColumn($"[bold]{L.Get("Renderer.Framework")}[/]");
        table.AddColumn($"[bold]{L.Get("Renderer.Reason")}[/]");

        foreach (var item in items)
        {
            var pkg = item.QueryResult.Package;
            table.AddRow(
                PackageLabel(item), Markup.Escape(pkg.Framework ?? "-"),
                FormatSignalSummary(item));
        }

        _console.Write(table);
        _console.WriteLine();
    }

    private void RenderReviewDetails(IReadOnlyList<LicenseDeclarationReviewResult> items, bool isVerbose)
    {
        _console.MarkupLine($"[bold]{L.Get("Renderer.ReviewDetails")}[/]");
        int limit = isVerbose ? items.Count : Math.Min(items.Count, DefaultDetailsLimit);

        foreach (var item in items.Take(limit))
        {
            var pkg = item.QueryResult.Package;
            _console.MarkupLine($"[yellow]•[/] [bold]{Markup.Escape(pkg.Name)} {Markup.Escape(pkg.Version)}[/] [grey]({Markup.Escape(pkg.Framework ?? "-")})[/]");
            _console.MarkupLine($"  [grey]{L.Get("Renderer.Reason")}:[/] {Markup.Escape(item.Reason)}");

            var decl = item.QueryResult.Declaration;
            if (decl?.Kind == LicenseDeclarationKind.File)
            {
                _console.MarkupLine($"  [grey]{L.Get("Renderer.LicenseFile")}[/] {Markup.Escape(decl.Value ?? L.Get("Renderer.Unknown"))}");
            }

            if (item.LicenseFileResult is { Status: not LicenseFileReadStatus.Found } fileResult)
            {
                _console.MarkupLine($"  [grey]{L.Get("Renderer.FileRead")}[/] {Markup.Escape(fileResult.Status.ToString())}");
                if (isVerbose && !string.IsNullOrWhiteSpace(fileResult.FailureReason))
                {
                    _console.MarkupLine($"  [grey]{L.Get("Renderer.Error")}[/] {Markup.Escape(fileResult.FailureReason)}");
                }
            }

            IEnumerable<LicenseTextSignal> signals = isVerbose ? item.Signals : item.Signals.Take(1);
            foreach (var signal in signals)
            {
                string evidence = isVerbose
                    ? signal.EvidenceText
                    : Shorten(signal.EvidenceText, DefaultEvidenceLength);
                _console.MarkupLine($"  [grey]{L.Format("Renderer.Evidence", SignalLabel(signal.Kind))}[/] \"{Markup.Escape(evidence)}\"");
                if (isVerbose && !string.IsNullOrWhiteSpace(signal.EvidenceUrl))
                {
                    _console.MarkupLine($"  [grey]{L.Get("Renderer.Link")}[/] {Markup.Escape(signal.EvidenceUrl)}");
                }
            }

            // Kısa ekranda en fazla bir bağlantı, ayrıntılı ekranda bütün kanıtlar sunulur.
            string? link = item.EvidenceUrl ?? item.Signals
                .Select(s => s.EvidenceUrl)
                .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
            if (IsWebLink(link))
                _console.MarkupLine($"  [grey]{L.Get("Renderer.Source")}[/] {Markup.Escape(link!)}");
            var (url, license) = PackageReference(item);
            _console.MarkupLine($"  [grey]{L.Get(license ? "Renderer.LicensePage" : "Renderer.PackagePage")}:[/] {Markup.Escape(url)}");
            _console.WriteLine();
        }

        if (limit < items.Count)
        {
            _console.MarkupLine($"[grey]{L.Format("Renderer.AdditionalReasons", items.Count - limit)}[/]");
        }
    }

    private void RenderUnknownTable(IReadOnlyList<LicenseDeclarationReviewResult> items)
    {
        var table = new Table
        {
            Border = TableBorder.Simple,
            Title = new TableTitle($"[bold]{L.Format("Renderer.UnknownTable", items.Count)}[/]")
        };
        table.AddColumn($"[bold]{L.Get("Renderer.PackageVersion")}[/]");
        table.AddColumn($"[bold]{L.Get("Renderer.Framework")}[/]");
        table.AddColumn($"[bold]{L.Get("Renderer.Reason")}[/]");

        foreach (var item in items)
        {
            var pkg = item.QueryResult.Package;
            string reason = item.QueryResult.Status == LicenseDeclarationQueryStatus.Success
                ? item.Reason
                : $"{QueryFailureLabel(item.QueryResult.Status)}: {item.Reason}";
            table.AddRow(PackageLabel(item), Markup.Escape(pkg.Framework ?? "-"), Markup.Escape(reason));
        }

        _console.Write(table);
        _console.WriteLine();
    }

    private void RenderMetadataTable(IReadOnlyList<LicenseDeclarationReviewResult> items)
    {
        var table = new Table
        {
            Border = TableBorder.Simple,
            Title = new TableTitle($"[bold]{L.Format("Renderer.MetadataTable", items.Count)}[/]")
        };
        table.AddColumn($"[bold]{L.Get("Renderer.PackageVersion")}[/]");
        table.AddColumn($"[bold]{L.Get("Renderer.Framework")}[/]");
        table.AddColumn($"[bold]{L.Get("Renderer.LicenseDeclaration")}[/]");
        foreach (var item in items)
        {
            var pkg = item.QueryResult.Package;
            var decl = item.QueryResult.Declaration;
            string declaration = decl is null ? L.Get("Renderer.NoDeclaration") : $"{decl.Kind}: {decl.Value}";
            table.AddRow(PackageLabel(item), Markup.Escape(pkg.Framework ?? "-"), Markup.Escape(declaration));
        }
        _console.Write(table);
        _console.WriteLine();
        _console.MarkupLine($"[grey]{L.Get("Renderer.MetadataWarning")}[/]");
    }

    private void RenderNextSteps(int reviewCount, int unknownCount, bool isVerbose)
    {
        if (reviewCount > 0 || unknownCount > 0)
        {
            _console.MarkupLine($"[grey]{L.Get("Renderer.Next1")}[/]");
            _console.MarkupLine($"[grey]{L.Get("Renderer.Next2")}[/]");
        }
        if (!isVerbose)
        {
            _console.MarkupLine($"[grey]{L.Get("Renderer.Hint")}[/]");
        }
    }

    private static bool IsWebLink(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    private static (string Url, bool License) PackageReference(LicenseDeclarationReviewResult item)
    {
        var package = item.QueryResult.Package;
        string url = "https://www.nuget.org/packages/" +
            Uri.EscapeDataString(package.Name) + "/" + Uri.EscapeDataString(package.Version);
        bool license = item.QueryResult.Declaration is { Kind: LicenseDeclarationKind.File or LicenseDeclarationKind.Expression };
        return (license ? url + "/License" : url, license);
    }

    private string PackageLabel(LicenseDeclarationReviewResult item)
    {
        var package = item.QueryResult.Package;
        string label = Markup.Escape($"{package.Name} {package.Version}");
        var (url, license) = PackageReference(item);
        // Only use hyperlink markup when the console explicitly supports it.
        // Plain output retains a copyable URL for every row.
        return _console.Profile.Capabilities.Links
            ? $"[link={url}]{label}[/]"
            : $"{label}\n{Markup.Escape(url)}";
    }

    private static string SourceLabel(DependencySourceKind kind) => kind == DependencySourceKind.LockFile
        ? "packages.lock.json"
        : "obj/project.assets.json";

    private static string FormatSignalSummary(LicenseDeclarationReviewResult item)
    {
        if (item.Signals.Count == 0)
        {
            return item.QueryResult.Declaration?.Kind == LicenseDeclarationKind.File
                ? $"[grey]{L.Get("Renderer.LicenseFileSignal")}[/]"
                : $"[grey]{L.Get("Renderer.SpecialReview")}[/]";
        }

        var parts = item.Signals.Select(s => SignalLabel(s.Kind)).Distinct();
        return Markup.Escape(string.Join(", ", parts));
    }

    private static string SignalLabel(LicenseTextSignalKind kind) => kind switch
    {
        LicenseTextSignalKind.ReciprocalLicenseMention => L.Get("Renderer.Reciprocal"),
        LicenseTextSignalKind.AlternativeLicenseAgreement => L.Get("Renderer.Alternative"),
        LicenseTextSignalKind.PaidCommercialLicenseMention => L.Get("Renderer.PaidCommercial"),
        _ => L.Get("Renderer.TextSignal")
    };

    private static string QueryFailureLabel(LicenseDeclarationQueryStatus status) => status switch
    {
        LicenseDeclarationQueryStatus.HttpFailure => L.Get("Renderer.HttpFailure"),
        LicenseDeclarationQueryStatus.Timeout => L.Get("Renderer.Timeout"),
        LicenseDeclarationQueryStatus.InvalidMetadata => L.Get("Renderer.InvalidMetadata"),
        LicenseDeclarationQueryStatus.InvalidPackageIdentity => L.Get("Renderer.InvalidIdentity"),
        _ => L.Get("Renderer.UnverifiedQuery")
    };

    private static string Shorten(string text, int maxLength)
    {
        string normalized = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength ? normalized : normalized[..(maxLength - 1)] + "…";
    }
}
