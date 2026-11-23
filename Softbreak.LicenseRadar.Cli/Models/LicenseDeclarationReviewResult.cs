namespace Softbreak.LicenseRadar.Cli.Models;

public sealed record LicenseDeclarationReviewResult(
    PackageLicenseDeclarationResult QueryResult,
    LicenseReviewStatus Status,
    string Reason,
    string? EvidenceUrl,
    LicenseFileReadResult? LicenseFileResult = null,
    IReadOnlyList<LicenseTextSignal>? Signals = null
)
{
    public IReadOnlyList<LicenseTextSignal> Signals { get; init; } =
        Signals ?? LicenseFileResult?.Signals ?? Array.Empty<LicenseTextSignal>();
}
