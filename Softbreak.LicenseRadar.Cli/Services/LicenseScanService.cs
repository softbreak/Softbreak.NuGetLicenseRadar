using Softbreak.LicenseRadar.Cli.Models;

namespace Softbreak.LicenseRadar.Cli.Services;

public sealed class LicenseScanService
{
    private readonly NuGetLicenseReader _licenseReader;
    private readonly LicenseDeclarationReviewEvaluator _reviewEvaluator;
    private readonly LicenseTextSignalDetector _signalDetector;

    public LicenseScanService(
        NuGetLicenseReader licenseReader,
        LicenseDeclarationReviewEvaluator reviewEvaluator,
        LicenseTextSignalDetector? signalDetector = null)
    {
        _licenseReader = licenseReader;
        _reviewEvaluator = reviewEvaluator;
        _signalDetector = signalDetector ?? new LicenseTextSignalDetector();
    }

    public async Task<LicenseDeclarationReviewResult> ScanAsync(
        PackageDependency package)
    {
        PackageLicenseDeclarationResult queryResult =
            await _licenseReader.GetLicenseDeclarationResultAsync(package);

        LicenseDeclarationReviewResult review =
            _reviewEvaluator.Evaluate(queryResult);
        NuGetLicenseDeclaration? declaration = queryResult.Declaration;

        if (queryResult.Status != LicenseDeclarationQueryStatus.Success ||
            declaration is null ||
            declaration.Kind != LicenseDeclarationKind.File)
        {
            return review;
        }

        LicenseFileReadResult fileResult =
            await _licenseReader.ReadLicenseFileAsync(
                package,
                declaration.Value ?? ""
            );

        if (fileResult.Status != LicenseFileReadStatus.Found ||
            string.IsNullOrWhiteSpace(fileResult.Content))
        {
            return review with
            {
                Status = LicenseReviewStatus.ReviewRequired,
                LicenseFileResult = fileResult
            };
        }

        IReadOnlyList<LicenseTextSignal> signals =
            _signalDetector.Detect(fileResult.Content);

        LicenseFileReadResult fileResultWithSignals = fileResult with
        {
            Signals = signals
        };

        if (signals.Count == 0)
        {
            return review with
            {
                Status = LicenseReviewStatus.ReviewRequired,
                LicenseFileResult = fileResultWithSignals
            };
        }

        string reason = BuildSignalReviewReason(signals);
        string? evidenceUrl = signals.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.EvidenceUrl))?.EvidenceUrl;

        return review with
        {
            Status = LicenseReviewStatus.ReviewRequired,
            Reason = reason,
            EvidenceUrl = evidenceUrl,
            LicenseFileResult = fileResultWithSignals,
            Signals = signals
        };
    }

    private static string BuildSignalReviewReason(IReadOnlyList<LicenseTextSignal> signals)
    {
        var descriptions = new List<string>();
        foreach (var signal in signals)
        {
            string desc = signal.Kind switch
            {
                LicenseTextSignalKind.ReciprocalLicenseMention =>
                    "karşılıklı lisans (reciprocal)",
                LicenseTextSignalKind.AlternativeLicenseAgreement =>
                    "alternatif lisans sözleşmesi",
                LicenseTextSignalKind.PaidCommercialLicenseMention =>
                    "ücretli ticari lisans gereksinimi",
                _ => signal.Kind.ToString()
            };
            descriptions.Add(desc);
        }

        string joined = string.Join(" ve ", descriptions);
        return $"Lisans metninde {joined} sinyali tespit edildi; kullanım koşulları ve lisans metni ayrıca incelenmeli.";
    }
}
