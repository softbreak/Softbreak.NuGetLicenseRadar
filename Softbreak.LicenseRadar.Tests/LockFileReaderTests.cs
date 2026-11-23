using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public sealed class LockFileReaderTests
{
    [Fact]
    public void Read_ProjectReferencesWithoutResolved_AreNotNuGetPackages()
    {
        const string json = """
            {
              "version": 2,
              "dependencies": {
                "net10.0": {
                  "Softbreak.OS.Core": {
                    "type": "Project",
                    "dependencies": {
                      "Microsoft.Extensions.Logging.Abstractions": "10.0.0"
                    }
                  },
                  "Newtonsoft.Json": {
                    "type": "Direct",
                    "requested": "[13.0.3, )",
                    "resolved": "13.0.3"
                  },
                  "Microsoft.Extensions.Logging.Abstractions": {
                    "type": "Transitive",
                    "resolved": "10.0.0"
                  },
                  "Polly": {
                    "type": "CentralTransitive",
                    "resolved": "8.6.0"
                  }
                }
              }
            }
            """;

        IReadOnlyList<PackageDependency> packages = ReadJson(json);

        Assert.Equal(3, packages.Count);
        Assert.DoesNotContain(packages, p => p.Name == "Softbreak.OS.Core");

        PackageDependency direct = Assert.Single(packages, p => p.Name == "Newtonsoft.Json");
        Assert.Equal("Direct", direct.Type);
        Assert.Equal("13.0.3", direct.Version);

        PackageDependency transitive = Assert.Single(
            packages, p => p.Name == "Microsoft.Extensions.Logging.Abstractions");
        Assert.Equal("Transitive", transitive.Type);

        PackageDependency centrallyPinned = Assert.Single(packages, p => p.Name == "Polly");
        Assert.Equal("Transitive", centrallyPinned.Type);
        Assert.Equal("8.6.0", centrallyPinned.Version);
    }

    [Fact]
    public void Read_ProjectOnly_ReturnsNoPackages()
    {
        const string json = """
            {
              "dependencies": {
                "net10.0": {
                  "Another.Project": {
                    "type": "Project"
                  }
                }
              }
            }
            """;

        Assert.Empty(ReadJson(json));
    }

    [Fact]
    public void Read_NuGetPackageMissingResolved_FailsWithContext()
    {
        const string json = """
            {
              "dependencies": {
                "net10.0": {
                  "Missing.Version": {
                    "type": "Direct"
                  }
                }
              }
            }
            """;

        FormatException error = Assert.Throws<FormatException>(() => ReadJson(json));
        Assert.Contains("net10.0", error.Message);
        Assert.Contains("Missing.Version", error.Message);
        Assert.Contains("resolved", error.Message);
    }

    [Fact]
    public void Read_PackageMissingType_FailsWithContext()
    {
        const string json = """
            {
              "dependencies": {
                "net10.0": {
                  "Missing.Type": {
                    "resolved": "1.0.0"
                  }
                }
              }
            }
            """;

        FormatException error = Assert.Throws<FormatException>(() => ReadJson(json));
        Assert.Contains("Missing.Type", error.Message);
        Assert.Contains("type", error.Message);
    }

    [Fact]
    public void Read_UnsupportedType_DoesNotSilentlySkipDependency()
    {
        const string json = """
            {
              "dependencies": {
                "net10.0": {
                  "Unexpected.Type": {
                    "type": "UnknownNewKind",
                    "resolved": "1.0.0"
                  }
                }
              }
            }
            """;

        FormatException error = Assert.Throws<FormatException>(() => ReadJson(json));
        Assert.Contains("Unexpected.Type", error.Message);
        Assert.Contains("UnknownNewKind", error.Message);
    }

    [Fact]
    public void Read_MissingDependencyGroups_FailsClearly()
    {
        FormatException error = Assert.Throws<FormatException>(() => ReadJson("{}"));
        Assert.Contains("dependencies", error.Message);
    }

    private static IReadOnlyList<PackageDependency> ReadJson(string json)
    {
        string filePath = Path.Combine(
            Path.GetTempPath(), $"radar-lockfile-test-{Guid.NewGuid():N}.json");

        File.WriteAllText(filePath, json);
        try
        {
            return new LockFileReader().Read(filePath);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
