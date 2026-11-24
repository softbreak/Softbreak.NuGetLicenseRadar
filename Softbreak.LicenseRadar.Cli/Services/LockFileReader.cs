using L = Softbreak.LicenseRadar.Cli.Localization.UiText;
using System.Text.Json;
using Softbreak.LicenseRadar.Cli.Models;

namespace Softbreak.LicenseRadar.Cli.Services;

public sealed class LockFileReader
{
    public List<PackageDependency> Read(string filePath)
    {
        string json = File.ReadAllText(filePath);
        using JsonDocument document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("dependencies", out JsonElement frameworks) ||
            frameworks.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException(L.Get("Lock.MissingDependencies"));
        }

        var packages = new List<PackageDependency>();

        foreach (JsonProperty framework in frameworks.EnumerateObject())
        {
            if (framework.Value.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException(
                    L.Format("Lock.InvalidTarget", framework.Name));
            }

            foreach (JsonProperty package in framework.Value.EnumerateObject())
            {
                JsonElement metadata = package.Value;
                string context = $"packages.lock.json: '{framework.Name}' hedefindeki '{package.Name}'";

                if (metadata.ValueKind != JsonValueKind.Object ||
                    !metadata.TryGetProperty("type", out JsonElement typeElement) ||
                    typeElement.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(typeElement.GetString()))
                {
                    throw new FormatException(L.Format("Lock.InvalidType", context));
                }

                string type = typeElement.GetString()!;

                // NuGet lock files may include project-to-project references with no
                // resolved NuGet package version. They are not NuGet packages.
                if (type.Equals("Project", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // A centrally pinned transitive dependency remains a NuGet package.
                string dependencyType;
                if (type.Equals("Direct", StringComparison.OrdinalIgnoreCase))
                {
                    dependencyType = "Direct";
                }
                else if (type.Equals("Transitive", StringComparison.OrdinalIgnoreCase) ||
                         type.Equals("CentralTransitive", StringComparison.OrdinalIgnoreCase))
                {
                    dependencyType = "Transitive";
                }
                else
                {
                    throw new FormatException(
                        L.Format("Lock.UnsupportedType", context, type));
                }

                if (!metadata.TryGetProperty("resolved", out JsonElement versionElement) ||
                    versionElement.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(versionElement.GetString()))
                {
                    throw new FormatException(
                        L.Format("Lock.InvalidVersion", context));
                }

                packages.Add(new PackageDependency(
                    package.Name,
                    versionElement.GetString()!,
                    dependencyType,
                    framework.Name));
            }
        }

        return packages;
    }
}
