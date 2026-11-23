
using System.Text.Json;
using Softbreak.LicenseRadar.Cli.Models;

namespace Softbreak.LicenseRadar.Cli.Services;

public sealed class LockFileReader
{
    public List<PackageDependency> Read(string filePath)
    {
        var packages = new List<PackageDependency>();

        string json = File.ReadAllText(filePath);

        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement frameworks =
            document.RootElement.GetProperty("dependencies");

        foreach (JsonProperty framework in frameworks.EnumerateObject())
        {
            foreach (JsonProperty package in framework.Value.EnumerateObject())
            {
                JsonElement metadata = package.Value;

                string type = metadata.GetProperty("type").GetString()!;
                string version = metadata.GetProperty("resolved").GetString()!;

                var dependency = new PackageDependency(
                    package.Name,
                    version,
                    type,
                    framework.Name
                );

                packages.Add(dependency);
            }
        }

        return packages;
    }
}
