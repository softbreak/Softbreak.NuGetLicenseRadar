
namespace Softbreak.LicenseRadar.Cli.Models;

public enum LicenseDeclarationKind
{
    Expression,
    File,
    Missing,
    Unsupported
}

public sealed record NuGetLicenseDeclaration(
    LicenseDeclarationKind Kind,
    string? Value
);
