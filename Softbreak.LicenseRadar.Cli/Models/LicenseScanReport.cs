namespace Softbreak.LicenseRadar.Cli.Models;

public sealed record LicenseScanReport(
    int SchemaVersion,
    DateTimeOffset ScannedAtUtc,
    IReadOnlyList<LicenseScanReportPackage> Packages
);

public sealed record LicenseScanReportPackage(
    string PackageName,
    string Version,
    string DependencyType,
    string Framework,
    LicenseDeclarationKind? LicenseDeclarationType,
    string? LicenseDeclarationValue,
    LicenseDeclarationQueryStatus MetadataQueryStatus,
    string? LicenseFilePath,
    LicenseFileReadStatus? LicenseFileReadStatus,
    LicenseReviewStatus ReviewStatus,
    string ReviewReason,
    string? EvidenceUrl
);
