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
                    "Lisans sorgusu HTTP hatasıyla başarısız oldu; lisans koşulları değerlendirilemedi.",
                LicenseDeclarationQueryStatus.Timeout =>
                    "Lisans sorgusu zaman aşımına uğradı; lisans koşulları değerlendirilemedi.",
                LicenseDeclarationQueryStatus.InvalidMetadata =>
                    "NuGet metadata'sı geçersiz; lisans koşulları değerlendirilemedi.",
                LicenseDeclarationQueryStatus.InvalidPackageIdentity =>
                    "Paket kimliği veya sürümü geçersiz; lisans sorgusu yapılmadı.",
                _ => "Lisans sorgusu başarılı değil; lisans koşulları değerlendirilemedi."
            };

            return CreateResult(queryResult, LicenseReviewStatus.Unknown, reason);
        }

        if (queryResult.Declaration is null)
        {
            return CreateResult(
                queryResult,
                LicenseReviewStatus.Unknown,
                "Sorgu başarılı olsa da lisans beyanı bulunamadı."
            );
        }

        return queryResult.Declaration.Kind switch
        {
            LicenseDeclarationKind.Expression =>
                CreateResult(
                    queryResult,
                    LicenseReviewStatus.MetadataOnly,
                    "Lisans ifadesi metadata'da bulundu; hukuki uyumluluk veya ücretsiz kullanım doğrulanmadı."
                ),
            LicenseDeclarationKind.File =>
                CreateResult(
                    queryResult,
                    LicenseReviewStatus.ReviewRequired,
                    "Lisans dosyası bildirildi; dosya içeriği ve kullanım koşulları ayrıca incelenmeli. Bu karar, lisansın ücretli olduğu anlamına gelmez."
                ),
            LicenseDeclarationKind.Missing =>
                CreateResult(
                    queryResult,
                    LicenseReviewStatus.Unknown,
                    "NuGet metadata'sında lisans beyanı bulunamadı."
                ),
            LicenseDeclarationKind.Unsupported =>
                CreateResult(
                    queryResult,
                    LicenseReviewStatus.Unknown,
                    "NuGet metadata'sındaki lisans beyanı desteklenen bir türde değil."
                ),
            _ =>
                CreateResult(
                    queryResult,
                    LicenseReviewStatus.Unknown,
                    "Lisans beyanı tanınmayan bir türde."
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
