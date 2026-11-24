using L = Softbreak.LicenseRadar.Cli.Localization.UiText;
using Spectre.Console;

namespace Softbreak.LicenseRadar.Cli.Services;

public sealed class DiscoveryErrorRenderer
{
    private readonly IAnsiConsole _console;
    public DiscoveryErrorRenderer(IAnsiConsole console) => _console = console;

    public void Render(string targetPath, DiscoveryResult result, bool recursive)
    {
        _console.MarkupLine("[bold cyan]Softbreak NuGet License Radar[/]");
        _console.MarkupLine($"[grey]{Markup.Escape(L.Get("Renderer.Target"))}[/] {Markup.Escape(result.SolutionPath ?? targetPath)}");
        string scope = result.SolutionPath is not null ? "Renderer.SolutionScope"
            : recursive ? "Renderer.RecursiveScope" : "Renderer.PathScope";
        _console.MarkupLine($"[grey]{Markup.Escape(L.Get(scope))}[/]");
        _console.WriteLine();
        _console.MarkupLine($"[bold red]{Markup.Escape(L.Get("Renderer.ScanFailed"))}[/]");
        if (result.UnrestoredProjects.Count > 0)
        {
            _console.MarkupLine(Markup.Escape(L.Format("Renderer.DiscoveryCounts",
                result.Projects.Count + result.UnrestoredProjects.Count, result.Projects.Count, result.UnrestoredProjects.Count)));
            _console.MarkupLine($"[yellow]{Markup.Escape(L.Get("Renderer.MissingDependencies"))}[/]");
            foreach (var project in result.UnrestoredProjectPaths ?? result.UnrestoredProjects)
                _console.MarkupLine("  • " + Markup.Escape(project));
            _console.MarkupLine(Markup.Escape(L.Get("Renderer.NoPartialScan")));
        }
        else if (result.ErrorMessage is not null)
            _console.MarkupLine(Markup.Escape(result.ErrorMessage));

        if (!string.IsNullOrWhiteSpace(result.SuggestedSolution))
        {
            _console.WriteLine();
            _console.MarkupLine($"[bold]{Markup.Escape(L.Get("Renderer.Actions"))}[/]");
            _console.MarkupLine(Markup.Escape(result.SuggestedSolution));
            if (result.UnrestoredProjectPaths is { Count: > 0 } paths)
                foreach (var path in paths)
                    _console.WriteLine($"  dotnet restore \"{path}\"");
            if (recursive && result.UnrestoredProjects.Count > 0)
                _console.MarkupLine(Markup.Escape(L.Get("Renderer.TrySolution")));
        }
    }
}
