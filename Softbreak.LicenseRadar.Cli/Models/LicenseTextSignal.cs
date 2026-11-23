namespace Softbreak.LicenseRadar.Cli.Models;

public enum LicenseTextSignalKind
{
    ReciprocalLicenseMention,
    AlternativeLicenseAgreement,
    PaidCommercialLicenseMention
}

public sealed record LicenseTextSignal(
    LicenseTextSignalKind Kind,
    string EvidenceText,
    string? EvidenceUrl = null
)
{
    public LicenseTextSignalKind Type => Kind;
    public string Evidence => EvidenceText;
}
