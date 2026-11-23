using Softbreak.LicenseRadar.Cli.Models;

namespace Softbreak.LicenseRadar.Cli.Services;

public enum DependencySourceKind
{
    LockFile,
    ProjectAssets
}

public sealed record DiscoveredProject(
    string ProjectName,
    string ProjectPath,
    string DependencyFilePath,
    DependencySourceKind SourceKind
);

public sealed record DiscoveryResult(
    IReadOnlyList<DiscoveredProject> Projects,
    IReadOnlyList<string> UnrestoredProjects,
    string? ErrorMessage = null,
    string? SuggestedSolution = null
)
{
    public bool IsSuccess => Projects.Count > 0 && ErrorMessage is null;
}

public sealed class ProjectDependencyLocator
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", ".git", ".vs", ".idea", "node_modules", "TestResults", ".local-test", "artifacts"
    };

    public DiscoveryResult Discover(string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        string fullPath = Path.GetFullPath(targetPath);

        if (File.Exists(fullPath))
        {
            return DiscoverFromFile(fullPath);
        }

        if (Directory.Exists(fullPath))
        {
            return DiscoverFromDirectory(fullPath);
        }

        return new DiscoveryResult(
            Array.Empty<DiscoveredProject>(),
            Array.Empty<string>(),
            $"Belirtilen yol bulunamadı: {targetPath}",
            "Lütfen geçerli bir klasör veya dosya yolu belirtin."
        );
    }

    private static DiscoveryResult DiscoverFromFile(string filePath)
    {
        string fileName = Path.GetFileName(filePath);

        if (fileName.Equals("packages.lock.json", StringComparison.OrdinalIgnoreCase))
        {
            string projName = Path.GetFileName(Path.GetDirectoryName(filePath) ?? filePath);
            return new DiscoveryResult(
                [new DiscoveredProject(projName, filePath, filePath, DependencySourceKind.LockFile)],
                Array.Empty<string>()
            );
        }

        if (fileName.Equals("project.assets.json", StringComparison.OrdinalIgnoreCase))
        {
            string projDir = Path.GetDirectoryName(Path.GetDirectoryName(filePath) ?? "") ?? "";
            string projName = Path.GetFileName(projDir);
            return new DiscoveryResult(
                [new DiscoveredProject(projName, filePath, filePath, DependencySourceKind.ProjectAssets)],
                Array.Empty<string>()
            );
        }

        if (fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            string projDir = Path.GetDirectoryName(filePath) ?? "";
            string projName = Path.GetFileNameWithoutExtension(filePath);

            string lockPath = Path.Combine(projDir, "packages.lock.json");
            if (File.Exists(lockPath))
            {
                return new DiscoveryResult(
                    [new DiscoveredProject(projName, filePath, lockPath, DependencySourceKind.LockFile)],
                    Array.Empty<string>()
                );
            }

            string assetsPath = Path.Combine(projDir, "obj", "project.assets.json");
            if (File.Exists(assetsPath))
            {
                return new DiscoveryResult(
                    [new DiscoveredProject(projName, filePath, assetsPath, DependencySourceKind.ProjectAssets)],
                    Array.Empty<string>()
                );
            }

            return new DiscoveryResult(
                Array.Empty<DiscoveredProject>(),
                [projName],
                $"'{projName}' projesinde ne 'packages.lock.json' ne de 'obj/project.assets.json' bulunamadı. Proje henüz restore edilmemiş.",
                "dotnet restore --use-lock-file"
            );
        }

        if (fileName.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            string slnDir = Path.GetDirectoryName(filePath) ?? "";
            return DiscoverFromDirectory(slnDir);
        }

        return new DiscoveryResult(
            Array.Empty<DiscoveredProject>(),
            Array.Empty<string>(),
            $"Desteklenmeyen dosya türü: '{fileName}'. Lütfen bir proje (.csproj), solution (.sln/.slnx), packages.lock.json veya klasör belirtin.",
            "license-radar scan ."
        );
    }

    private static DiscoveryResult DiscoverFromDirectory(string directoryPath)
    {
        // Prefer actual projects over a root lock file so multi-project scans are complete.
        // 1. Discover .csproj files
        var projectFiles = FindCsprojFiles(directoryPath);
        if (projectFiles.Count > 0)
        {
            var discovered = new List<DiscoveredProject>();
            var unrestored = new List<string>();

            foreach (string csproj in projectFiles)
            {
                string projDir = Path.GetDirectoryName(csproj) ?? "";
                string projName = Path.GetFileNameWithoutExtension(csproj);

                string lockPath = Path.Combine(projDir, "packages.lock.json");
                if (File.Exists(lockPath))
                {
                    discovered.Add(new DiscoveredProject(projName, csproj, lockPath, DependencySourceKind.LockFile));
                    continue;
                }

                string assetsPath = Path.Combine(projDir, "obj", "project.assets.json");
                if (File.Exists(assetsPath))
                {
                    discovered.Add(new DiscoveredProject(projName, csproj, assetsPath, DependencySourceKind.ProjectAssets));
                    continue;
                }

                unrestored.Add(projName);
            }

            // A partial scan must not silently appear complete. Do not restore or edit
            // projects without explicit user action.
            if (unrestored.Count > 0)
            {
                return new DiscoveryResult(
                    discovered,
                    unrestored,
                    $"{unrestored.Count} .NET projesi restore edilmemiş veya bağımlılık verileri bulunamadı: {string.Join(", ", unrestored)}. Eksik projeler sessizce atlanmayacak.",
                    "Solution klasöründe 'dotnet restore --use-lock-file' çalıştırıp taramayı yeniden başlatın."
                );
            }

            if (discovered.Count > 0)
            {
                return new DiscoveryResult(discovered, unrestored);
            }

            return new DiscoveryResult(
                Array.Empty<DiscoveredProject>(),
                unrestored,
                $"{unrestored.Count} adet .NET projesi bulundu ancak hiçbirinde 'packages.lock.json' veya 'obj/project.assets.json' mevcut değil. Projeler henüz restore edilmemiş.",
                "dotnet restore --use-lock-file"
            );
        }

        // 2. Fallback for standalone lock files without a .csproj file.
        string directLockPath = Path.Combine(directoryPath, "packages.lock.json");
        if (File.Exists(directLockPath))
        {
            string dirName = Path.GetFileName(directoryPath);
            return new DiscoveryResult(
                [new DiscoveredProject(dirName, directLockPath, directLockPath, DependencySourceKind.LockFile)],
                Array.Empty<string>()
            );
        }

        // 3. Fallback: check obj/project.assets.json directly in directory
        string directAssetsPath = Path.Combine(directoryPath, "obj", "project.assets.json");
        if (File.Exists(directAssetsPath))
        {
            string dirName = Path.GetFileName(directoryPath);
            return new DiscoveryResult(
                [new DiscoveredProject(dirName, directAssetsPath, directAssetsPath, DependencySourceKind.ProjectAssets)],
                Array.Empty<string>()
            );
        }

        return new DiscoveryResult(
            Array.Empty<DiscoveredProject>(),
            Array.Empty<string>(),
            $"'{directoryPath}' dizininde herhangi bir .NET projesi veya bağımlılık dosyası ('packages.lock.json' / 'obj/project.assets.json') bulunamadı.",
            "Taramak istediğiniz .NET projesinin veya solution'ın bulunduğu klasöre geçip 'license-radar scan .' komutunu çalıştırın."
        );
    }

    private static List<string> FindCsprojFiles(string rootPath)
    {
        var results = new List<string>();
        CollectCsprojRecursive(new DirectoryInfo(rootPath), results);
        results.Sort(StringComparer.OrdinalIgnoreCase);
        return results;
    }

    private static void CollectCsprojRecursive(DirectoryInfo dir, List<string> results)
    {
        if (IgnoredDirectories.Contains(dir.Name))
        {
            return;
        }

        try
        {
            foreach (FileInfo file in dir.GetFiles("*.csproj"))
            {
                results.Add(file.FullName);
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                // Avoid recursive loops and scans outside the requested tree.
                if ((subDir.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    continue;
                }
                CollectCsprojRecursive(subDir, results);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip inaccessible directories
        }
        catch (IOException)
        {
            // Skip entries that disappear or become unavailable during discovery.
        }
    }
}
