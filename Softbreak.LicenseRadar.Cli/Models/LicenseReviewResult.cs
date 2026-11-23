
namespace Softbreak.LicenseRadar.Cli.Models;

public enum LicenseReviewStatus
{
    MetadataOnly,
    ReviewRequired,
    Unknown
}

public sealed record LicenseReviewResult(
    PackageLicenseResult LicenseResult,
    LicenseReviewStatus Status,
    string Reason,
    string? EvidenceUrl
);
