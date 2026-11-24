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
        bool isVerbose)
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

        RenderHeader(targetPath, projects, isVerbose);
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
            _console.MarkupLine($"[grey]Diğer {metadata.Count} pakette lisans metadata'sı bulundu; hukuki uygunluk doğrulanmadı.[/]");
        }
        else if (isVerbose && metadata.Count > 0)
        {
            RenderMetadataTable(metadata);
        }

        if (reviews.Count == 0 && unknown.Count == 0)
        {
            _console.MarkupLine("[grey]Özel bir inceleme sinyali bulunmadı; bu sonuç hukuki onay anlamına gelmez.[/]");
        }

        RenderNextSteps(reviews.Count, unknown.Count, isVerbose);
    }

    private void RenderHeader(string targetPath, IReadOnlyList<DiscoveredProject> projects, bool isVerbose)
    {
        _console.WriteLine();
        _console.Write(new Rule("[bold]Softbreak NuGet License Radar[/]") { Justification = Justify.Left });
        _console.MarkupLine($"[grey]Hedef:[/] {Markup.Escape(targetPath)}");

        if (projects.Count == 1)
        {
            var p = projects[0];
            _console.MarkupLine($"[grey]Proje:[/] {Markup.Escape(p.ProjectName)} [grey]({SourceLabel(p.SourceKind)})[/]");
        }
        else if (projects.Count > 1)
        {
            int lockCount = projects.Count(p => p.SourceKind == DependencySourceKind.LockFile);
            int assetsCount = projects.Count - lockCount;
            _console.MarkupLine($"[grey]{projects.Count} proje keşfedildi · Lock: {lockCount} · Assets: {assetsCount}[/]");
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
            _console.MarkupLine("[grey]Not: project.assets.json son restore sırasında çözümlenen bağımlılıkları yansıtır.[/]");
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
        _console.MarkupLine($"[bold]Toplam: {packages.Count}[/]  [grey]Doğrudan: {direct} · Dolaylı: {transitive}[/]");
        _console.MarkupLine($"[yellow]İnceleme gerekli: {reviews}[/]  [bold]Doğrulanamadı: {unknown}[/]  [grey]Metadata: {metadata}[/]");
        _console.MarkupLine(failures > 0
            ? $"[red]Sorgu hatası: {failures}[/] [grey](doğrulanamayan sonuçlara dahildir)[/]"
            : "[grey]Sorgu hatası: 0[/]");
        _console.WriteLine();
    }

    private void RenderReviewTable(IReadOnlyList<LicenseDeclarationReviewResult> items)
    {
        var table = new Table
        {
            Border = TableBorder.Simple,
            Title = new TableTitle($"[bold yellow]İnceleme Gerektiren Paketler ({items.Count})[/]")
        };
        table.AddColumn("[bold]Paket / sürüm[/]");
        table.AddColumn("[bold]Neden[/]");

        foreach (var item in items)
        {
            var pkg = item.QueryResult.Package;
            table.AddRow(
                Markup.Escape($"{pkg.Name} {pkg.Version}"),
                FormatSignalSummary(item));
        }

        _console.Write(table);
        _console.WriteLine();
    }

    private void RenderReviewDetails(IReadOnlyList<LicenseDeclarationReviewResult> items, bool isVerbose)
    {
        _console.MarkupLine("[bold]İnceleme gerekçeleri[/]");
        int limit = isVerbose ? items.Count : Math.Min(items.Count, DefaultDetailsLimit);

        foreach (var item in items.Take(limit))
        {
            var pkg = item.QueryResult.Package;
            _console.MarkupLine($"[yellow]•[/] [bold]{Markup.Escape(pkg.Name)} {Markup.Escape(pkg.Version)}[/]");
            _console.MarkupLine($"  [grey]Neden:[/] {Markup.Escape(item.Reason)}");

            var decl = item.QueryResult.Declaration;
            if (decl?.Kind == LicenseDeclarationKind.File)
            {
                _console.MarkupLine($"  [grey]Lisans dosyası:[/] {Markup.Escape(decl.Value ?? "Bilinmiyor")}");
            }

            if (item.LicenseFileResult is { Status: not LicenseFileReadStatus.Found } fileResult)
            {
                _console.MarkupLine($"  [grey]Dosya okuma:[/] {Markup.Escape(fileResult.Status.ToString())}");
                if (isVerbose && !string.IsNullOrWhiteSpace(fileResult.FailureReason))
                {
                    _console.MarkupLine($"  [grey]Hata:[/] {Markup.Escape(fileResult.FailureReason)}");
                }
            }

            IEnumerable<LicenseTextSignal> signals = isVerbose ? item.Signals : item.Signals.Take(1);
            foreach (var signal in signals)
            {
                string evidence = isVerbose
                    ? signal.EvidenceText
                    : Shorten(signal.EvidenceText, DefaultEvidenceLength);
                _console.MarkupLine($"  [grey]Kanıt ({SignalLabel(signal.Kind)}):[/] \"{Markup.Escape(evidence)}\"");
                if (isVerbose && !string.IsNullOrWhiteSpace(signal.EvidenceUrl))
                {
                    _console.MarkupLine($"  [grey]Bağlantı:[/] {Markup.Escape(signal.EvidenceUrl)}");
                }
            }

            // Kısa ekranda en fazla bir bağlantı, ayrıntılı ekranda bütün kanıtlar sunulur.
            string? link = item.EvidenceUrl ?? item.Signals
                .Select(s => s.EvidenceUrl)
                .FirstOrDefault(u => !string.IsNullOrWhiteSpace(u));
            if (!string.IsNullOrWhiteSpace(link))
            {
                _console.MarkupLine($"  [grey]Kaynak:[/] {Markup.Escape(link)}");
            }
            _console.WriteLine();
        }

        if (limit < items.Count)
        {
            _console.MarkupLine($"[grey]{items.Count - limit} ek inceleme gerekçesi için --verbose kullanın.[/]");
        }
    }

    private void RenderUnknownTable(IReadOnlyList<LicenseDeclarationReviewResult> items)
    {
        var table = new Table
        {
            Border = TableBorder.Simple,
            Title = new TableTitle($"[bold]Doğrulanamayan Paketler ({items.Count})[/]")
        };
        table.AddColumn("[bold]Paket / sürüm[/]");
        table.AddColumn("[bold]Neden[/]");

        foreach (var item in items)
        {
            var pkg = item.QueryResult.Package;
            string reason = item.QueryResult.Status == LicenseDeclarationQueryStatus.Success
                ? item.Reason
                : $"{QueryFailureLabel(item.QueryResult.Status)}: {item.Reason}";
            table.AddRow(Markup.Escape($"{pkg.Name} {pkg.Version}"), Markup.Escape(reason));
        }

        _console.Write(table);
        _console.WriteLine();
    }

    private void RenderMetadataTable(IReadOnlyList<LicenseDeclarationReviewResult> items)
    {
        var table = new Table
        {
            Border = TableBorder.Simple,
            Title = new TableTitle($"[bold]Metadata Seviyesinde İncelenen Paketler ({items.Count})[/]")
        };
        table.AddColumn("[bold]Paket / sürüm[/]");
        table.AddColumn("[bold]Lisans bildirimi[/]");
        foreach (var item in items)
        {
            var pkg = item.QueryResult.Package;
            var decl = item.QueryResult.Declaration;
            string declaration = decl is null ? "Bildirim yok" : $"{decl.Kind}: {decl.Value}";
            table.AddRow(Markup.Escape($"{pkg.Name} {pkg.Version}"), Markup.Escape(declaration));
        }
        _console.Write(table);
        _console.WriteLine();
        _console.MarkupLine("[grey]Metadata bulunması, kullanım için hukuki onay anlamına gelmez.[/]");
    }

    private void RenderNextSteps(int reviewCount, int unknownCount, bool isVerbose)
    {
        if (reviewCount > 0 || unknownCount > 0)
        {
            _console.MarkupLine("[grey]Sonraki adım: İlgili lisans metnini ve kullanım koşullarınızı karşılaştırın;[/]");
            _console.MarkupLine("[grey]gerekirse üreticinin resmî koşullarını doğrulayın. Satın alma kararı otomatik verilmez.[/]");
        }
        if (!isVerbose)
        {
            _console.MarkupLine("[grey]Ayrıntılar için komutunuza [bold]--verbose[/], JSON raporu için [bold]--output rapor.json[/] ekleyin.[/]");
        }
    }

    private static string SourceLabel(DependencySourceKind kind) => kind == DependencySourceKind.LockFile
        ? "packages.lock.json"
        : "obj/project.assets.json";

    private static string FormatSignalSummary(LicenseDeclarationReviewResult item)
    {
        if (item.Signals.Count == 0)
        {
            return item.QueryResult.Declaration?.Kind == LicenseDeclarationKind.File
                ? "[grey]Lisans dosyası[/]"
                : "[grey]Özel inceleme[/]";
        }

        var parts = item.Signals.Select(s => SignalLabel(s.Kind)).Distinct();
        return Markup.Escape(string.Join(", ", parts));
    }

    private static string SignalLabel(LicenseTextSignalKind kind) => kind switch
    {
        LicenseTextSignalKind.ReciprocalLicenseMention => "Karşılıklı lisans",
        LicenseTextSignalKind.AlternativeLicenseAgreement => "Alternatif sözleşme",
        LicenseTextSignalKind.PaidCommercialLicenseMention => "Ücretli ticari lisans ifadesi",
        _ => "Lisans metni sinyali"
    };

    private static string QueryFailureLabel(LicenseDeclarationQueryStatus status) => status switch
    {
        LicenseDeclarationQueryStatus.HttpFailure => "HTTP bağlantı hatası",
        LicenseDeclarationQueryStatus.Timeout => "Zaman aşımı",
        LicenseDeclarationQueryStatus.InvalidMetadata => "Geçersiz metadata",
        LicenseDeclarationQueryStatus.InvalidPackageIdentity => "Geçersiz paket kimliği",
        _ => "Lisans sorgusu doğrulanamadı"
    };

    private static string Shorten(string text, int maxLength)
    {
        string normalized = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength ? normalized : normalized[..(maxLength - 1)] + "…";
    }
}
