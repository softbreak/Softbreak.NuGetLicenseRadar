using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Softbreak.LicenseRadar.Cli.Services;

internal static class SolutionProjectReader
{
    public static bool IsSolution(string path) =>
        Path.GetExtension(path).Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
        Path.GetExtension(path).Equals(".slnx", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Read(string path)
    {
        IEnumerable<string> members;
        if (Path.GetExtension(path).Equals(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            var root = XDocument.Load(path).Root;
            if (root?.Name.LocalName != "Solution") throw new FormatException();
            members = root.Descendants().Where(e => e.Name.LocalName == "Project")
                .Select(e => (string?)e.Attribute("Path") ?? throw new FormatException()).ToArray();
        }
        else
        {
            var lines = File.ReadAllLines(path);
            if (!lines.Any(line => line.StartsWith("Microsoft Visual Studio Solution File, Format Version", StringComparison.Ordinal)))
                throw new FormatException();
            var entries = new List<string>();
            foreach (var line in lines.Select(x => x.TrimStart()).Where(x => x.StartsWith("Project(", StringComparison.Ordinal)))
            {
                var match = Regex.Match(line, "^Project\\(\"(?<type>[^\"]+)\"\\)\\s*=\\s*\"[^\"]*\"\\s*,\\s*\"(?<path>[^\"]+)\"\\s*,\\s*\"[^\"]+\"\\s*$");
                if (!match.Success) throw new FormatException();
                if (!match.Groups["type"].Value.Equals("{66A26720-8FB5-11D2-AA7E-00C04F688DDE}", StringComparison.OrdinalIgnoreCase))
                    entries.Add(match.Groups["path"].Value);
            }
            members = entries;
        }

        var directory = Path.GetDirectoryName(path)!;
        return members.Where(p => Path.GetExtension(p).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(p => Path.GetFullPath(Path.Combine(directory, p.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar))))
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .OrderBy(p => p, StringComparer.Ordinal).ToArray();
    }
}
