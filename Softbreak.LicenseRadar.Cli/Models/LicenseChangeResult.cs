namespace Softbreak.LicenseRadar.Cli.Models;

public enum LicenseChangeType
{
    PackageAdded,
    PackageRemoved,
    PackageVersionChanged,
    DeclarationTypeChanged,
    DeclarationValueChanged,
    LicenseInformationAdded,
    LicenseInformationRemoved,
    NotComparable
}

public sealed record LicenseChangeResult(
    PackageDependency? PreviousPackage,
    PackageDependency? CurrentPackage,
    NuGetLicenseDeclaration? PreviousDeclaration,
    NuGetLicenseDeclaration? CurrentDeclaration,
    LicenseDeclarationQueryStatus? PreviousQueryStatus,
    LicenseDeclarationQueryStatus? CurrentQueryStatus,
    LicenseChangeType ChangeType,
    string Reason
);
