namespace Softbreak.LicenseRadar.Cli.Models;

public enum LicenseDeclarationQueryStatus
{
    Success,
    HttpFailure,
    Timeout,
    InvalidMetadata,
    InvalidPackageIdentity
}

public sealed record PackageLicenseDeclarationResult(
    PackageDependency Package,
    NuGetLicenseDeclaration? Declaration,
    LicenseDeclarationQueryStatus Status
);
