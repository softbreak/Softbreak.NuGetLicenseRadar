namespace Softbreak.LicenseRadar.Cli.Models;

public enum LicenseFileReadStatus
{
    Found,
    NotFound,
    InvalidPath,
    TooLarge,
    InvalidArchive,
    InvalidText,
    InvalidPackageIdentity,
    DownloadFailure,
    DownloadTimeout,
    ArchiveTooLarge
}

public sealed record LicenseFileReadResult(
    LicenseFileReadStatus Status,
    string? Content,
    string? FailureReason = null,
    IReadOnlyList<LicenseTextSignal>? Signals = null
)
{
    public IReadOnlyList<LicenseTextSignal> Signals { get; init; } =
        Signals ?? Array.Empty<LicenseTextSignal>();
}
