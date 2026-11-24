using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public class ProjectAssetsFileReaderTests
{
    private const string SampleAssetsJson = """
        {
          "version": 4,
          "targets": {
            "net10.0": {
              "NuGet.Versioning/7.9.0": {
                "type": "package"
              },
              "Spectre.Console/0.57.2": {
                "type": "package"
              },
              "Spectre.Console.Ansi/0.57.2": {
                "type": "package"
              },
              "MyInternalProject/1.0.0": {
                "type": "project"
              }
            }
          },
          "projectFileDependencyGroups": {
            "net10.0": [
              "NuGet.Versioning >= 7.9.0",
              "Spectre.Console >= 0.57.2"
            ]
          }
        }
        """;

    private readonly ProjectAssetsFileReader _reader = new();

    [Fact]
    public void Parse_ValidJson_DistinguishesDirectAndTransitivePackages()
    {
        IReadOnlyList<PackageDependency> packages = _reader.Parse(SampleAssetsJson);

        Assert.Equal(3, packages.Count);

        PackageDependency direct1 = Assert.Single(packages, p => p.Name == "NuGet.Versioning");
        Assert.Equal("7.9.0", direct1.Version);
        Assert.Equal("Direct", direct1.Type);
        Assert.Equal("net10.0", direct1.Framework);

        PackageDependency direct2 = Assert.Single(packages, p => p.Name == "Spectre.Console");
        Assert.Equal("0.57.2", direct2.Version);
        Assert.Equal("Direct", direct2.Type);

        PackageDependency transitive = Assert.Single(packages, p => p.Name == "Spectre.Console.Ansi");
        Assert.Equal("0.57.2", transitive.Version);
        Assert.Equal("Transitive", transitive.Type);

        // Project references (type == "project") must not be included
        Assert.DoesNotContain(packages, p => p.Name == "MyInternalProject");
    }

    [Fact]
    public void Parse_TargetWithRuntimeIdentifier_ExtractsBaseFramework()
    {
        const string jsonWithRid = """
            {
              "version": 4,
              "targets": {
                "net10.0/win-x64": {
                  "Example.Pkg/1.2.3": {
                    "type": "package"
                  }
                }
              },
              "projectFileDependencyGroups": {
                "net10.0": [
                  "Example.Pkg >= 1.2.3"
                ]
              }
            }
            """;

        IReadOnlyList<PackageDependency> packages = _reader.Parse(jsonWithRid);

        PackageDependency pkg = Assert.Single(packages);
        Assert.Equal("net10.0", pkg.Framework);
        Assert.Equal("Direct", pkg.Type);
    }

    [Theory]
    [InlineData("net6.0")]
    [InlineData("net7.0")]
    [InlineData("net8.0")]
    [InlineData("net9.0")]
    [InlineData("net10.0")]
    public void Parse_MultipleDotnetTargets_IdentifiesDirectAndTransitivePackages(string framework)
    {
        string json = $"""
            {
              "version": 3,
              "targets": {
                "{{framework}}": {
                  "Example.Direct/1.2.3": { "type": "package" },
                  "Example.Transitive/4.5.6": { "type": "package" },
                  "Example.SourceProject/1.0.0": { "type": "project" }
                }
              },
              "projectFileDependencyGroups": {
                "{{framework}}": [ "Example.Direct >= 1.2.3" ]
              }
            }
            """;

        IReadOnlyList<PackageDependency> packages = _reader.Parse(json);
        Assert.Equal(2, packages.Count);
        Assert.Equal("Direct", Assert.Single(packages, p => p.Name == "Example.Direct").Type);
        Assert.Equal("Transitive", Assert.Single(packages, p => p.Name == "Example.Transitive").Type);
        Assert.All(packages, p => Assert.Equal(framework, p.Framework));
    }

    [Fact]
    public void Parse_MissingTargets_ThrowsFormatException()
    {
        const string invalidJson = """
            {
              "version": 4
            }
            """;

        Assert.Throws<FormatException>(() => _reader.Parse(invalidJson));
    }

    [Fact]
    public void Read_NonExistentFile_ThrowsFileNotFoundException()
    {
        Assert.Throws<FileNotFoundException>(() => _reader.Read("non_existent_project.assets.json"));
    }
}
