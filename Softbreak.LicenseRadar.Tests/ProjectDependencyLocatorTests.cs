using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public class ProjectDependencyLocatorTests
{
    private static readonly string SolutionRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")
    );

    private readonly ProjectDependencyLocator _locator = new();

    [Fact]
    public void Discover_DirectPackagesLockFile_ReturnsLockFileTarget()
    {
        // This behavior test owns its fixture. A repository-relative fixture can
        // disappear or resolve differently under VSTest, CI, or packaged ZIP runs.
        string tempDir = Path.Combine(Path.GetTempPath(), $"radar-lock-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string lockFilePath = Path.Combine(tempDir, "packages.lock.json");
        File.WriteAllText(lockFilePath, "{\"version\":1,\"dependencies\":{}}");

        try
        {
            DiscoveryResult result = _locator.Discover(lockFilePath);

            Assert.True(result.IsSuccess, result.ErrorMessage);
            DiscoveredProject project = Assert.Single(result.Projects);
            Assert.Equal(DependencySourceKind.LockFile, project.SourceKind);
            Assert.Equal(Path.GetFullPath(lockFilePath), project.DependencyFilePath);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Discover_DirectoryWithMultipleProjects_DiscoversAllProjectsWithoutSilentRandomSelection()
    {
        // Directory contains Softbreak.LicenseRadar.Cli and Softbreak.LicenseRadar.Tests
        DiscoveryResult result = _locator.Discover(SolutionRoot);

        Assert.True(result.IsSuccess);
        Assert.True(result.Projects.Count >= 2);
        Assert.Contains(result.Projects, p => p.ProjectName.Contains("Cli", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Projects, p => p.ProjectName.Contains("Tests", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Discover_ProjectWithOnlyProjectAssets_FallsBackToAssetsFile()
    {
        string cliDir = Path.Combine(SolutionRoot, "Softbreak.LicenseRadar.Cli");

        DiscoveryResult result = _locator.Discover(cliDir);

        Assert.True(result.IsSuccess);
        DiscoveredProject project = Assert.Single(result.Projects);
        Assert.Equal("Softbreak.LicenseRadar.Cli", project.ProjectName);
        Assert.EndsWith("project.assets.json", project.DependencyFilePath);
        Assert.Equal(DependencySourceKind.ProjectAssets, project.SourceKind);
    }

    [Fact]
    public void Discover_UnrestoredProject_ReturnsDescriptiveErrorAndRestoreCommand()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"radar-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string csproj = Path.Combine(tempDir, "UnrestoredProject.csproj");
        File.WriteAllText(csproj, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        try
        {
            DiscoveryResult result = _locator.Discover(tempDir);

            Assert.False(result.IsSuccess);
            Assert.Empty(result.Projects);
            Assert.Contains("UnrestoredProject", result.UnrestoredProjects);
            Assert.Contains("restore edilmemiş", result.ErrorMessage);
            Assert.Contains("dotnet restore --use-lock-file", result.SuggestedSolution);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Discover_MixedRestoredAndUnrestoredProjects_FailsClosed()
    {
        string root = Path.Combine(Path.GetTempPath(), $"radar-partial-{Guid.NewGuid():N}");
        string ready = Path.Combine(root, "Ready");
        string missing = Path.Combine(root, "NotRestored");
        Directory.CreateDirectory(ready);
        Directory.CreateDirectory(missing);
        File.WriteAllText(Path.Combine(ready, "Ready.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(missing, "NotRestored.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(ready, "packages.lock.json"), "{\"dependencies\":{}}");

        try
        {
            DiscoveryResult result = _locator.Discover(root);
            Assert.False(result.IsSuccess);
            Assert.Single(result.Projects);
            Assert.Contains("NotRestored", result.UnrestoredProjects);
            Assert.Contains("dotnet restore", result.SuggestedSolution);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Discover_ProjectsTakePrecedenceOverUnrelatedRootLockFile()
    {
        string root = Path.Combine(Path.GetTempPath(), $"radar-projects-{Guid.NewGuid():N}");
        string child = Path.Combine(root, "Worker");
        Directory.CreateDirectory(child);
        File.WriteAllText(Path.Combine(root, "packages.lock.json"), "{\"dependencies\":{}}");
        File.WriteAllText(Path.Combine(child, "Worker.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(child, "packages.lock.json"), "{\"dependencies\":{}}");

        try
        {
            DiscoveredProject project = Assert.Single(_locator.Discover(root).Projects);
            Assert.Equal("Worker", project.ProjectName);
            Assert.Equal(Path.Combine(child, "packages.lock.json"), project.DependencyFilePath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Discover_NonExistentDirectory_ReturnsDescriptiveError()
    {
        string nonExistent = Path.Combine(Path.GetTempPath(), $"non-existent-{Guid.NewGuid():N}");

        DiscoveryResult result = _locator.Discover(nonExistent);

        Assert.False(result.IsSuccess);
        Assert.Contains("bulunamadı", result.ErrorMessage);
    }
}
