using System.Text.Json;
using Softbreak.LicenseRadar.Cli.Models;

namespace Softbreak.LicenseRadar.Cli.Services;

public sealed class ProjectAssetsFileReader
{
    public IReadOnlyList<PackageDependency> Read(string filePath)
    {
        ArgumentGuards.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("project.assets.json dosyası bulunamadı.", filePath);
        }

        string json = File.ReadAllText(filePath);
        return Parse(json);
    }

    public IReadOnlyList<PackageDependency> Parse(string json)
    {
        ArgumentGuards.ThrowIfNullOrWhiteSpace(json);

        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        if (!root.TryGetProperty("targets", out JsonElement targetsElement))
        {
            throw new FormatException("project.assets.json içinde 'targets' bölümü bulunamadı.");
        }

        var directDependenciesByFramework = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("projectFileDependencyGroups", out JsonElement depGroups))
        {
            foreach (JsonProperty group in depGroups.EnumerateObject())
            {
                string frameworkKey = group.Name;
                var directSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (group.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in group.Value.EnumerateArray())
                    {
                        string? spec = item.GetString();
                        if (!string.IsNullOrWhiteSpace(spec))
                        {
                            string pkgName = spec.Split(
                                new[] { ' ', '>', '<', '=', '[' },
                                StringSplitOptions.RemoveEmptyEntries
                            )[0].Trim();

                            if (pkgName.Length > 0)
                            {
                                directSet.Add(pkgName);
                            }
                        }
                    }
                }

                directDependenciesByFramework[frameworkKey] = directSet;
            }
        }

        var dependencies = new List<PackageDependency>();

        foreach (JsonProperty frameworkTarget in targetsElement.EnumerateObject())
        {
            string targetName = frameworkTarget.Name;
            string framework = targetName.Split('/')[0];

            directDependenciesByFramework.TryGetValue(targetName, out var directSet);
            if (directSet is null && targetName != framework)
            {
                directDependenciesByFramework.TryGetValue(framework, out directSet);
            }
            directSet ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (JsonProperty packageProp in frameworkTarget.Value.EnumerateObject())
            {
                string packageKey = packageProp.Name;
                JsonElement packageObj = packageProp.Value;

                if (packageObj.TryGetProperty("type", out JsonElement typeEl) &&
                    typeEl.GetString() != "package")
                {
                    continue;
                }

                int slashIndex = packageKey.IndexOf('/');
                if (slashIndex <= 0)
                {
                    continue;
                }

                string packageName = packageKey.Substring(0, slashIndex);
                string version = packageKey.Substring(slashIndex + 1);

                string depType = directSet.Contains(packageName) ? "Direct" : "Transitive";

                dependencies.Add(new PackageDependency(
                    packageName,
                    version,
                    depType,
                    framework
                ));
            }
        }

        return dependencies;
    }
}
