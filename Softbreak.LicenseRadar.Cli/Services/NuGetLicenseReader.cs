using L = Softbreak.LicenseRadar.Cli.Localization.UiText;
﻿
using System.Xml;
using System.Xml.Linq;
using NuGet.Versioning;
using Softbreak.LicenseRadar.Cli.Models;

namespace Softbreak.LicenseRadar.Cli.Services;

public sealed class NuGetLicenseReader
{
    public const int MaximumPackageArchiveSizeBytes = 50 * 1024 * 1024;

    private const string FlatContainerBaseUrl =
        "https://api.nuget.org/v3-flatcontainer";

    private readonly HttpClient _httpClient;
    private readonly NuGetLicenseDeclarationParser _licenseDeclarationParser;
    private readonly NuGetLicenseFileReader _licenseFileReader;

    public NuGetLicenseReader(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _licenseDeclarationParser = new NuGetLicenseDeclarationParser();
        _licenseFileReader = new NuGetLicenseFileReader();
    }

    // NuGet üzerinden lisans ifadesini okur
    public async Task<string?> GetLicenseExpressionAsync(
        string packageId,
        string version)
    {
        if (!TryCreatePackageUri(
            packageId,
            version,
            packageArchive: false,
            out Uri? uri) ||
            uri is null)
        {
            throw new ArgumentException(
                L.Get("NuGet.InvalidIdentity")
            );
        }

        string xml = await _httpClient.GetStringAsync(uri);

        XDocument document = XDocument.Parse(xml);

        XElement? license = document
            .Descendants()
            .FirstOrDefault(x => x.Name.LocalName == "license");

        if (license?.Attribute("type")?.Value != "expression")
            return null;

        return license.Value.Trim();
    }

    public async Task<NuGetLicenseDeclaration> GetLicenseDeclarationAsync(
        string packageId,
        string version)
    {
        if (!TryCreatePackageUri(
            packageId,
            version,
            packageArchive: false,
            out Uri? uri) ||
            uri is null)
        {
            throw new ArgumentException(
                L.Get("NuGet.InvalidIdentity")
            );
        }

        string xml = await _httpClient.GetStringAsync(uri);

        return _licenseDeclarationParser.Parse(xml);
    }

    public async Task<PackageLicenseDeclarationResult>
        GetLicenseDeclarationResultAsync(PackageDependency package)
    {
        if (!TryCreatePackageUri(
            package.Name,
            package.Version,
            packageArchive: false,
            out _))
        {
            return new PackageLicenseDeclarationResult(
                package,
                null,
                LicenseDeclarationQueryStatus.InvalidPackageIdentity
            );
        }

        try
        {
            NuGetLicenseDeclaration declaration =
                await GetLicenseDeclarationAsync(
                    package.Name,
                    package.Version
                );

            return new PackageLicenseDeclarationResult(
                package,
                declaration,
                LicenseDeclarationQueryStatus.Success
            );
        }
        catch (HttpRequestException)
        {
            return new PackageLicenseDeclarationResult(
                package,
                null,
                LicenseDeclarationQueryStatus.HttpFailure
            );
        }
        catch (OperationCanceledException)
        {
            return new PackageLicenseDeclarationResult(
                package,
                null,
                LicenseDeclarationQueryStatus.Timeout
            );
        }
        catch (TimeoutException)
        {
            return new PackageLicenseDeclarationResult(
                package,
                null,
                LicenseDeclarationQueryStatus.Timeout
            );
        }
        catch (XmlException)
        {
            return new PackageLicenseDeclarationResult(
                package,
                null,
                LicenseDeclarationQueryStatus.InvalidMetadata
            );
        }
        catch (FormatException)
        {
            return new PackageLicenseDeclarationResult(
                package,
                null,
                LicenseDeclarationQueryStatus.InvalidMetadata
            );
        }
    }

    public async Task<LicenseFileReadResult> ReadLicenseFileAsync(
        PackageDependency package,
        string licenseFilePath)
    {
        if (!TryCreatePackageUri(
            package.Name,
            package.Version,
            packageArchive: true,
            out Uri? requestUri) ||
            requestUri is null)
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.InvalidPackageIdentity,
                null,
                L.Get("NuGet.InvalidArchiveIdentity")
            );
        }

        using var timeoutSource = new CancellationTokenSource();
        if (_httpClient.Timeout != Timeout.InfiniteTimeSpan)
        {
            timeoutSource.CancelAfter(_httpClient.Timeout);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token
            );

            if (response.RequestMessage?.RequestUri != requestUri)
            {
                return new LicenseFileReadResult(
                    LicenseFileReadStatus.DownloadFailure,
                    null,
                    L.Get("NuGet.UnexpectedUri")
                );
            }

            if (!response.IsSuccessStatusCode)
            {
                return new LicenseFileReadResult(
                    LicenseFileReadStatus.DownloadFailure,
                    null,
                    L.Format("NuGet.HttpFailure", (int)response.StatusCode, response.StatusCode)
                );
            }

            long? contentLength = response.Content.Headers.ContentLength;
            if (contentLength > MaximumPackageArchiveSizeBytes)
            {
                return new LicenseFileReadResult(
                    LicenseFileReadStatus.ArchiveTooLarge,
                    null,
                    L.Get("NuGet.TooLarge")
                );
            }

            using Stream responseStream =
                await response.Content.ReadAsStreamAsync(timeoutSource.Token);
            using var archiveStream = new MemoryStream();
            byte[] buffer = new byte[81920];
            long totalRead = 0;

            while (true)
            {
                int bytesToRead = (int)Math.Min(
                    buffer.Length,
                    MaximumPackageArchiveSizeBytes + 1L - totalRead
                );
                int bytesRead = await responseStream.ReadAsync(
                    buffer.AsMemory(0, bytesToRead),
                    timeoutSource.Token
                );

                if (bytesRead == 0)
                {
                    break;
                }

                totalRead += bytesRead;
                if (totalRead > MaximumPackageArchiveSizeBytes)
                {
                    return new LicenseFileReadResult(
                        LicenseFileReadStatus.ArchiveTooLarge,
                        null,
                        L.Get("NuGet.TooLarge")
                    );
                }

                archiveStream.Write(buffer, 0, bytesRead);
            }

            if (contentLength.HasValue && contentLength.Value != totalRead)
            {
                return new LicenseFileReadResult(
                    LicenseFileReadStatus.DownloadFailure,
                    null,
                    L.Get("NuGet.LengthMismatch")
                );
            }

            archiveStream.Position = 0;
            return await _licenseFileReader.ReadAsync(
                archiveStream,
                licenseFilePath
            );
        }
        catch (OperationCanceledException)
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.DownloadTimeout,
                null,
                L.Get("NuGet.Timeout")
            );
        }
        catch (TimeoutException)
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.DownloadTimeout,
                null,
                L.Get("NuGet.Timeout")
            );
        }
        catch (HttpRequestException exception)
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.DownloadFailure,
                null,
                L.Format("NuGet.DownloadFailure", exception.Message)
            );
        }
        catch (IOException exception)
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.DownloadFailure,
                null,
                L.Format("NuGet.StreamFailure", exception.Message)
            );
        }
    }

    // Paket, lisans ifadesi ve sorgu durumunu birlikte döndürür
    public async Task<PackageLicenseResult> GetLicenseResultAsync(
        PackageDependency package)
    {
        try
        {
            string? expression = await GetLicenseExpressionAsync(
                package.Name,
                package.Version
            );

            LicenseLookupStatus status =
                string.IsNullOrWhiteSpace(expression)
                    ? LicenseLookupStatus.ExpressionNotFound
                    : LicenseLookupStatus.ExpressionFound;

            return new PackageLicenseResult(
                package,
                expression,
                status
            );
        }
        catch (HttpRequestException)
        {
            return CreateFailedResult(package);
        }
        catch (TaskCanceledException)
        {
            return CreateFailedResult(package);
        }
        catch (XmlException)
        {
            return CreateFailedResult(package);
        }
    }

    private static PackageLicenseResult CreateFailedResult(
        PackageDependency package)
    {
        return new PackageLicenseResult(
            package,
            null,
            LicenseLookupStatus.QueryFailed
        );
    }

    private static bool TryCreatePackageUri(
        string packageId,
        string version,
        bool packageArchive,
        out Uri? uri)
    {
        uri = null;

        if (!IsValidPackageId(packageId) || !IsValidVersion(version))
        {
            return false;
        }

        string escapedId = Uri.EscapeDataString(packageId.ToLowerInvariant());
        string escapedVersion = Uri.EscapeDataString(version.ToLowerInvariant());
        string fileName = packageArchive
            ? $"{escapedId}.{escapedVersion}.nupkg"
            : $"{escapedId}.nuspec";
        string address =
            $"{FlatContainerBaseUrl}/{escapedId}/{escapedVersion}/{fileName}";

        return Uri.TryCreate(address, UriKind.Absolute, out uri) &&
            uri is not null &&
            uri.Scheme == Uri.UriSchemeHttps &&
            uri.Host.Equals("api.nuget.org", StringComparison.OrdinalIgnoreCase) &&
            uri.Port == 443;
    }

    private static bool IsValidPackageId(string packageId)
    {
        return !string.IsNullOrEmpty(packageId) &&
            packageId.Length <= 100 &&
            IsAsciiLetterOrDigit(packageId[0]) &&
            IsAsciiLetterOrDigit(packageId[^1]) &&
            packageId.All(character =>
                IsAsciiLetterOrDigit(character) ||
                character is '.' or '-' or '_');
    }

    private static bool IsValidVersion(string version)
    {
        return !string.IsNullOrEmpty(version) &&
            version.Length <= 256 &&
            NuGetVersion.TryParse(version, out _) &&
            version.All(character =>
                IsAsciiLetterOrDigit(character) ||
                character is '.' or '-' or '+');
    }
    // Char.IsAsciiLetterOrDigit was added in .NET 7; preserve exact ASCII-only
    // package identity and version validation on .NET 6.
    private static bool IsAsciiLetterOrDigit(char value) =>
        (value >= 'A' && value <= 'Z') ||
        (value >= 'a' && value <= 'z') ||
        (value >= '0' && value <= '9');

}
