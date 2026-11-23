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
            throw new FormatException("packages.lock.json içinde geçerli bir 'dependencies' bölümü bulunamadı.");
        }

        var packages = new List<PackageDependency>();

        foreach (JsonProperty framework in frameworks.EnumerateObject())
        {
            if (framework.Value.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException(
                    $"packages.lock.json: '{framework.Name}' hedefindeki bağımlılıklar geçersiz.");
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
                    throw new FormatException($"{context} için geçerli bir 'type' bulunamadı.");
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
                        $"{context} için desteklenmeyen bağımlılık türü: '{type}'. Tarama eksik gösterilmeyecek.");
                }

                if (!metadata.TryGetProperty("resolved", out JsonElement versionElement) ||
                    versionElement.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(versionElement.GetString()))
                {
                    throw new FormatException(
                        $"{context} için geçerli bir 'resolved' NuGet sürümü bulunamadı.");
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
