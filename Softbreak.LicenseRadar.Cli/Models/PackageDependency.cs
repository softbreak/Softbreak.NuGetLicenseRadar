
namespace Softbreak.LicenseRadar.Cli.Models;

public sealed record PackageDependency(
    string Name,
    string Version,
    string Type,
    string Framework
);
