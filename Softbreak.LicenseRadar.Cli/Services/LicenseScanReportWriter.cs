using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Softbreak.LicenseRadar.Cli.Models;

namespace Softbreak.LicenseRadar.Cli.Services;

public sealed class LicenseScanReportWriter
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Serialize(
        IEnumerable<LicenseDeclarationReviewResult> scanResults,
        DateTimeOffset scannedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(scanResults);

        LicenseScanReportPackage[] packages = scanResults
            .OrderBy(
                result => result.QueryResult.Package.Name,
                StringComparer.OrdinalIgnoreCase
            )
            .ThenBy(
                result => result.QueryResult.Package.Name,
                StringComparer.Ordinal
            )
            .ThenBy(
                result => result.QueryResult.Package.Framework,
                StringComparer.OrdinalIgnoreCase
            )
            .ThenBy(
                result => result.QueryResult.Package.Framework,
                StringComparer.Ordinal
            )
            .ThenBy(
                result => result.QueryResult.Package.Version,
                StringComparer.Ordinal
            )
            .ThenBy(result => result.QueryResult.Status)
            .ThenBy(result => result.QueryResult.Declaration?.Kind)
            .ThenBy(
                result => result.QueryResult.Declaration?.Value,
                StringComparer.Ordinal
            )
            .Select(CreatePackageReport)
            .ToArray();

        var report = new LicenseScanReport(
            CurrentSchemaVersion,
            scannedAtUtc.ToUniversalTime(),
            packages
        );

        return JsonSerializer.Serialize(report, JsonOptions);
    }

    public async Task WriteNewAsync(
        string outputPath,
        IEnumerable<LicenseDeclarationReviewResult> scanResults,
        DateTimeOffset scannedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        string fullPath = Path.GetFullPath(outputPath);
        if (File.Exists(fullPath))
        {
            throw new IOException("JSON çıktı dosyası zaten mevcut.");
        }

        string directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Çıktı dosyası yolu geçersiz.", nameof(outputPath));
        string fileName = Path.GetFileName(fullPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Çıktı yolu bir dosya adı içermeli.", nameof(outputPath));
        }

        byte[] reportBytes = Encoding.UTF8.GetBytes(
            Serialize(scanResults, scannedAtUtc)
        );
        string temporaryPath = Path.Combine(
            directory,
            $".{fileName}.{Guid.NewGuid():N}.tmp"
        );

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous))
            {
                await stream.WriteAsync(reportBytes);
                await stream.FlushAsync();
            }

            File.Move(temporaryPath, fullPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static LicenseScanReportPackage CreatePackageReport(
        LicenseDeclarationReviewResult result)
    {
        PackageLicenseDeclarationResult queryResult = result.QueryResult;
        NuGetLicenseDeclaration? declaration = queryResult.Declaration;
        bool isFileDeclaration =
            declaration?.Kind == LicenseDeclarationKind.File;

        return new LicenseScanReportPackage(
            queryResult.Package.Name,
            queryResult.Package.Version,
            queryResult.Package.Type,
            queryResult.Package.Framework,
            declaration?.Kind,
            declaration?.Value,
            queryResult.Status,
            isFileDeclaration ? declaration?.Value : null,
            result.LicenseFileResult?.Status,
            result.Status,
            result.Reason,
            result.EvidenceUrl
        );
    }
}
