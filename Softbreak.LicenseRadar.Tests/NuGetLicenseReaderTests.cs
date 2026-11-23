using System.Net;
using System.Xml;
using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;

namespace Softbreak.LicenseRadar.Tests;

public class NuGetLicenseReaderTests
{
    [Fact]
    public async Task GetLicenseDeclarationAsync_ExpressionLicense_ReturnsExpression()
    {
        const string xml = """
            <package>
              <metadata>
                <license type="expression">MIT</license>
              </metadata>
            </package>
            """;

        var handler = new FakeHttpMessageHandler(xml);
        using var httpClient = new HttpClient(handler);
        var reader = new NuGetLicenseReader(httpClient);

        NuGetLicenseDeclaration result =
            await reader.GetLicenseDeclarationAsync("Example.Package", "1.0.0");

        Assert.Equal(LicenseDeclarationKind.Expression, result.Kind);
        Assert.Equal("MIT", result.Value);
        Assert.Equal(
            "https://api.nuget.org/v3-flatcontainer/example.package/1.0.0/example.package.nuspec",
            handler.RequestUri?.AbsoluteUri
        );
    }

    [Fact]
    public async Task GetLicenseDeclarationAsync_FileLicense_ReturnsFile()
    {
        const string xml = """
            <package>
              <metadata>
                <license type="file">LICENSE.txt</license>
              </metadata>
            </package>
            """;

        NuGetLicenseDeclaration result = await ParseAsync(xml);

        Assert.Equal(LicenseDeclarationKind.File, result.Kind);
        Assert.Equal("LICENSE.txt", result.Value);
    }

    [Fact]
    public async Task GetLicenseDeclarationAsync_MissingLicense_ReturnsMissing()
    {
        const string xml = """
            <package>
              <metadata />
            </package>
            """;

        NuGetLicenseDeclaration result = await ParseAsync(xml);

        Assert.Equal(LicenseDeclarationKind.Missing, result.Kind);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetLicenseDeclarationAsync_UnsupportedLicense_ReturnsUnsupported()
    {
        const string xml = """
            <package>
              <metadata>
                <license type="url">https://example.test/license</license>
              </metadata>
            </package>
            """;

        NuGetLicenseDeclaration result = await ParseAsync(xml);

        Assert.Equal(LicenseDeclarationKind.Unsupported, result.Kind);
        Assert.Equal("https://example.test/license", result.Value);
    }

    [Fact]
    public async Task GetLicenseDeclarationAsync_InvalidXml_ThrowsXmlException()
    {
        const string xml = "<package><metadata>";

        await Assert.ThrowsAsync<XmlException>(() => ParseAsync(xml));
    }

    [Fact]
    public async Task GetLicenseDeclarationAsync_MissingMetadata_ThrowsFormatException()
    {
        const string xml = """
            <package>
              <other />
            </package>
            """;

        await Assert.ThrowsAsync<FormatException>(() => ParseAsync(xml));
    }

    [Fact]
    public async Task GetLicenseDeclarationResultAsync_ExpressionLicense_ReturnsSuccess()
    {
        const string xml = """
            <package>
              <metadata>
                <license type="expression">MIT</license>
              </metadata>
            </package>
            """;

        PackageLicenseDeclarationResult result = await QueryResultAsync(xml);

        Assert.Equal(LicenseDeclarationQueryStatus.Success, result.Status);
        Assert.Equal(
            LicenseDeclarationKind.Expression,
            result.Declaration?.Kind
        );
        Assert.Equal("MIT", result.Declaration?.Value);
        Assert.Equal(
            new PackageDependency(
                "Example.Package",
                "1.0.0",
                "Direct",
                "net10.0"
            ),
            result.Package
        );
    }

    [Fact]
    public async Task GetLicenseDeclarationResultAsync_FileLicense_ReturnsSuccess()
    {
        const string xml = """
            <package>
              <metadata>
                <license type="file">LICENSE.txt</license>
              </metadata>
            </package>
            """;

        PackageLicenseDeclarationResult result = await QueryResultAsync(xml);

        Assert.Equal(LicenseDeclarationQueryStatus.Success, result.Status);
        Assert.Equal(LicenseDeclarationKind.File, result.Declaration?.Kind);
        Assert.Equal("LICENSE.txt", result.Declaration?.Value);
    }

    [Fact]
    public async Task GetLicenseDeclarationResultAsync_MissingLicense_ReturnsSuccess()
    {
        const string xml = """
            <package>
              <metadata />
            </package>
            """;

        PackageLicenseDeclarationResult result = await QueryResultAsync(xml);

        Assert.Equal(LicenseDeclarationQueryStatus.Success, result.Status);
        Assert.Equal(LicenseDeclarationKind.Missing, result.Declaration?.Kind);
        Assert.Null(result.Declaration?.Value);
    }

    [Fact]
    public async Task GetLicenseDeclarationResultAsync_UnsupportedLicense_ReturnsSuccess()
    {
        const string xml = """
            <package>
              <metadata>
                <license type="url">https://example.test/license</license>
              </metadata>
            </package>
            """;

        PackageLicenseDeclarationResult result = await QueryResultAsync(xml);

        Assert.Equal(LicenseDeclarationQueryStatus.Success, result.Status);
        Assert.Equal(
            LicenseDeclarationKind.Unsupported,
            result.Declaration?.Kind
        );
        Assert.Equal(
            "https://example.test/license",
            result.Declaration?.Value
        );
    }

    [Fact]
    public async Task GetLicenseDeclarationResultAsync_HttpFailure_ReturnsHttpFailure()
    {
        PackageLicenseDeclarationResult result = await QueryResultAsync(
            "<package />",
            HttpStatusCode.ServiceUnavailable
        );

        Assert.Equal(LicenseDeclarationQueryStatus.HttpFailure, result.Status);
        Assert.Null(result.Declaration);
    }

    [Fact]
    public async Task GetLicenseDeclarationResultAsync_Timeout_ReturnsTimeout()
    {
        PackageLicenseDeclarationResult result = await QueryResultAsync(
            "<package />",
            exception: new TaskCanceledException("Simulated timeout.")
        );

        Assert.Equal(LicenseDeclarationQueryStatus.Timeout, result.Status);
        Assert.Null(result.Declaration);
    }

    [Fact]
    public async Task GetLicenseDeclarationResultAsync_InvalidXml_ReturnsInvalidMetadata()
    {
        PackageLicenseDeclarationResult result =
            await QueryResultAsync("<package><metadata>");

        Assert.Equal(
            LicenseDeclarationQueryStatus.InvalidMetadata,
            result.Status
        );
        Assert.Null(result.Declaration);
    }

    [Fact]
    public async Task GetLicenseDeclarationResultAsync_MissingMetadata_ReturnsInvalidMetadata()
    {
        const string xml = """
            <package>
              <other />
            </package>
            """;

        PackageLicenseDeclarationResult result = await QueryResultAsync(xml);

        Assert.Equal(
            LicenseDeclarationQueryStatus.InvalidMetadata,
            result.Status
        );
        Assert.Null(result.Declaration);
    }

    private static async Task<NuGetLicenseDeclaration> ParseAsync(string xml)
    {
        using var httpClient = new HttpClient(new FakeHttpMessageHandler(xml));
        var reader = new NuGetLicenseReader(httpClient);

        return await reader.GetLicenseDeclarationAsync("Example.Package", "1.0.0");
    }

    private static async Task<PackageLicenseDeclarationResult> QueryResultAsync(
        string xml,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        Exception? exception = null)
    {
        using var httpClient = new HttpClient(
            new FakeHttpMessageHandler(xml, statusCode, exception)
        );
        var reader = new NuGetLicenseReader(httpClient);
        var package = new PackageDependency(
            "Example.Package",
            "1.0.0",
            "Direct",
            "net10.0"
        );

        return await reader.GetLicenseDeclarationResultAsync(package);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _xml;
        private readonly HttpStatusCode _statusCode;
        private readonly Exception? _exception;

        public FakeHttpMessageHandler(
            string xml,
            HttpStatusCode statusCode = HttpStatusCode.OK,
            Exception? exception = null)
        {
            _xml = xml;
            _statusCode = statusCode;
            _exception = exception;
        }

        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;

            if (_exception is not null)
            {
                return Task.FromException<HttpResponseMessage>(_exception);
            }

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_xml)
            });
        }
    }
}
