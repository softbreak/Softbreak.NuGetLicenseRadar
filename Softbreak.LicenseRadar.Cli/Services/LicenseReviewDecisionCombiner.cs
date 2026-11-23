using Softbreak.LicenseRadar.Cli.Models;

namespace Softbreak.LicenseRadar.Cli.Services;

public sealed class LicenseReviewDecisionCombiner
{
    private readonly LicenseReviewEvaluator _licenseReviewEvaluator;

    public LicenseReviewDecisionCombiner(
        LicenseReviewEvaluator licenseReviewEvaluator)
    {
        _licenseReviewEvaluator = licenseReviewEvaluator;
    }

    public LicenseDeclarationReviewResult Combine(
        LicenseDeclarationReviewResult declarationReview)
    {
        PackageLicenseDeclarationResult queryResult =
            declarationReview.QueryResult;

        if (queryResult.Status != LicenseDeclarationQueryStatus.Success)
        {
            return declarationReview;
        }

        string? expression = queryResult.Declaration is
            { Kind: LicenseDeclarationKind.Expression } declaration
                ? declaration.Value
                : null;

        var legacyQueryResult = new PackageLicenseResult(
            queryResult.Package,
            expression,
            string.IsNullOrWhiteSpace(expression)
                ? LicenseLookupStatus.ExpressionNotFound
                : LicenseLookupStatus.ExpressionFound
        );

        LicenseReviewResult packageReview =
            _licenseReviewEvaluator.Evaluate(legacyQueryResult);

        if (packageReview.Status == LicenseReviewStatus.ReviewRequired)
        {
            return declarationReview with
            {
                Status = packageReview.Status,
                Reason = packageReview.Reason,
                EvidenceUrl = packageReview.EvidenceUrl
            };
        }

        if (packageReview.Status == LicenseReviewStatus.Unknown &&
            packageReview.EvidenceUrl is not null)
        {
            return declarationReview with
            {
                Status = declarationReview.Status == LicenseReviewStatus.ReviewRequired
                    ? declarationReview.Status
                    : LicenseReviewStatus.Unknown,
                Reason = declarationReview.Status == LicenseReviewStatus.ReviewRequired
                    ? $"{declarationReview.Reason} {packageReview.Reason}"
                    : packageReview.Reason,
                EvidenceUrl = packageReview.EvidenceUrl
            };
        }

        return declarationReview;
    }
}
