using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public sealed class SolutionDiscoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    public SolutionDiscoveryTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);

    private void Project(string name, bool restored)
    {
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name + ".csproj"), "<Project />");
        if (restored) File.WriteAllText(Path.Combine(dir, "packages.lock.json"), "{}");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SolutionScopeExcludesUnrestoredNonMembers(bool xml)
    {
        Project("App", true);
        Project("Tool", false);
        var solution = Path.Combine(root, xml ? "App.slnx" : "App.sln");
        File.WriteAllText(solution, xml
            ? "<Solution><Folder Name=\"/src/\"><Project Path=\"App/App.csproj\" /></Folder></Solution>"
            : "Microsoft Visual Studio Solution File, Format Version 12.00\nProject(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"App\", \"App\\App.csproj\", \"{11111111-1111-1111-1111-111111111111}\"\nEndProject\nGlobal\nEndGlobal");
        var locator = new ProjectDependencyLocator();
        foreach (var target in new[] {root, solution})
        {
            var result = locator.Discover(target);
            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.Equal("App", Assert.Single(result.Projects).ProjectName);
            Assert.Equal(solution, result.SolutionPath);
        }
        Assert.False(locator.Discover(root, recursive: true).IsSuccess);
        File.Delete(Path.Combine(root, "App", "packages.lock.json"));
        Assert.False(locator.Discover(root).IsSuccess);
        File.Delete(Path.Combine(root, "App", "App.csproj"));
        Assert.False(locator.Discover(root).IsSuccess);
    }

    [Fact]
    public void MultipleSolutionsRequireExplicitSelection()
    {
        Project("App", true);
        const string xml = "<Solution><Project Path=\"App/App.csproj\" /></Solution>";
        var a = Path.Combine(root, "A.slnx");
        File.WriteAllText(a, xml);
        File.WriteAllText(Path.Combine(root, "B.slnx"), xml);
        var locator = new ProjectDependencyLocator();
        Assert.False(locator.Discover(root).IsSuccess);
        Assert.True(locator.Discover(a).IsSuccess);
        Assert.True(locator.Discover(root, recursive: true).IsSuccess);
    }

    [Fact]
    public void MalformedSolutionFailsClosed()
    {
        Project("App", true);
        File.WriteAllText(Path.Combine(root, "Bad.slnx"), "<Solution>");
        Assert.False(new ProjectDependencyLocator().Discover(root).IsSuccess);
    }
}
