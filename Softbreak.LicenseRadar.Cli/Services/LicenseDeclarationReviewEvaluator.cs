using L = Softbreak.LicenseRadar.Cli.Localization.UiText;
using Softbreak.LicenseRadar.Cli.Models;

namespace Softbreak.LicenseRadar.Cli.Services;

public sealed class LicenseDeclarationReviewEvaluator
{
    public LicenseDeclarationReviewResult Evaluate(
        PackageLicenseDeclarationResult queryResult)
    {
        if (queryResult.Status != LicenseDeclarationQueryStatus.Success)
        {
            string reason = queryResult.Status switch
            {
                LicenseDeclarationQueryStatus.HttpFailure =>
                    L.Get("Eval.HttpFailure"),
                LicenseDeclarationQueryStatus.Timeout =>
                    L.Get("Eval.Timeout"),
                LicenseDeclarationQueryStatus.InvalidMetadata =>
                    L.Get("Eval.InvalidMetadata"),
                LicenseDeclarationQueryStatus.InvalidPackageIdentity =>
                    L.Get("Eval.InvalidPackage"),
                _ => L.Get("Eval.QueryFailed")
            };

            return CreateResult(queryResult, LicenseReviewStatus.Unknown, reason);
        }

        if (queryResult.Declaration is null)
        {
            return CreateResult(
                queryResult,
                LicenseReviewStatus.Unknown,
                L.Get("Eval.NoDeclaration")
            );
        }

        return queryResult.Declaration.Kind switch
        {
            LicenseDeclarationKind.Expression =>
                CreateResult(
                    queryResult,
                    LicenseReviewStatus.MetadataOnly,
                    L.Get("Eval.Expression")
                ),
            LicenseDeclarationKind.File =>
                CreateResult(
                    queryResult,
                    LicenseReviewStatus.ReviewRequired,
                    L.Get("Eval.File")
                ),
            LicenseDeclarationKind.Missing =>
                CreateResult(
                    queryResult,
                    LicenseReviewStatus.Unknown,
                    L.Get("Eval.Missing")
                ),
            LicenseDeclarationKind.Unsupported =>
                CreateResult(
                    queryResult,
                    LicenseReviewStatus.Unknown,
                    L.Get("Eval.Unsupported")
                ),
            _ =>
                CreateResult(
                    queryResult,
                    LicenseReviewStatus.Unknown,
                    L.Get("Eval.Unknown")
                )
        };
    }

    private static LicenseDeclarationReviewResult CreateResult(
        PackageLicenseDeclarationResult queryResult,
        LicenseReviewStatus status,
        string reason)
    {
        return new LicenseDeclarationReviewResult(
            queryResult,
            status,
            reason,
            null
        );
    }
}
