using System.IO.Compression;
using System.Net;
using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public class LicenseScanServiceTests
{
    [Fact]
    public async Task ScanAsync_Expression_ReturnsMetadataOnlyAndQueriesOnce()
    {
        const string xml = """
            <package>
              <metadata>
                <license type="expression">MIT</license>
              </metadata>
            </package>
            """;

        var scan = await ScanAsync(xml);

        Assert.Equal(LicenseDeclarationQueryStatus.Success, scan.Result.QueryResult.Status);
        Assert.Equal(LicenseDeclarationKind.Expression, scan.Result.QueryResult.Declaration?.Kind);
        Assert.Equal(LicenseReviewStatus.MetadataOnly, scan.Result.Status);
        Assert.Same(scan.Package, scan.Result.QueryResult.Package);
        Assert.Equal(1, scan.Handler.RequestCount);
    }

    [Fact]
    public async Task ScanAsync_File_ReturnsReviewRequiredAfterReadingArchive()
    {
        const string xml = """
            <package>
              <metadata>
                <license type="file">LICENSE.txt</license>
              </metadata>
            </package>
            """;

        var scan = await ScanAsync(xml);

        Assert.Equal(LicenseDeclarationQueryStatus.Success, scan.Result.QueryResult.Status);
        Assert.Equal(LicenseDeclarationKind.File, scan.Result.QueryResult.Declaration?.Kind);
        Assert.Equal(LicenseReviewStatus.ReviewRequired, scan.Result.Status);
        Assert.Equal(
            LicenseFileReadStatus.Found,
            scan.Result.LicenseFileResult?.Status
        );
        Assert.Same(scan.Package, scan.Result.QueryResult.Package);
        Assert.Equal(2, scan.Handler.RequestCount);
    }

    [Fact]
    public async Task ScanAsync_MissingLicense_ReturnsUnknownAndQueriesOnce()
    {
        const string xml = """
            <package>
              <metadata />
            </package>
            """;

        var scan = await ScanAsync(xml);

        Assert.Equal(LicenseDeclarationQueryStatus.Success, scan.Result.QueryResult.Status);
        Assert.Equal(LicenseDeclarationKind.Missing, scan.Result.QueryResult.Declaration?.Kind);
        Assert.Equal(LicenseReviewStatus.Unknown, scan.Result.Status);
        Assert.Same(scan.Package, scan.Result.QueryResult.Package);
        Assert.Equal(1, scan.Handler.RequestCount);
    }

    [Fact]
    public async Task ScanAsync_HttpFailure_ReturnsUnknownAndQueriesOnce()
    {
        var scan = await ScanAsync(
            "<package />",
            HttpStatusCode.ServiceUnavailable
        );

        Assert.Equal(
            LicenseDeclarationQueryStatus.HttpFailure,
            scan.Result.QueryResult.Status
        );
        Assert.Null(scan.Result.QueryResult.Declaration);
        Assert.Equal(LicenseReviewStatus.Unknown, scan.Result.Status);
        Assert.Same(scan.Package, scan.Result.QueryResult.Package);
        Assert.Equal(1, scan.Handler.RequestCount);
    }

    private static async Task<(
        LicenseDeclarationReviewResult Result,
        PackageDependency Package,
        FakeHttpMessageHandler Handler)> ScanAsync(
            string xml,
            HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new FakeHttpMessageHandler(
            xml,
            CreateLicenseArchive(),
            statusCode
        );
        using var httpClient = new HttpClient(handler);
        var reader = new NuGetLicenseReader(httpClient);
        var evaluator = new LicenseDeclarationReviewEvaluator();
        var service = new LicenseScanService(reader, evaluator);
        var package = new PackageDependency(
            "Example.Package",
            "1.0.0",
            "Direct",
            "net10.0"
        );

        LicenseDeclarationReviewResult result = await service.ScanAsync(package);

        return (result, package, handler);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _xml;
        private readonly byte[] _archive;
        private readonly HttpStatusCode _statusCode;

        public FakeHttpMessageHandler(
            string xml,
            byte[] archive,
            HttpStatusCode statusCode)
        {
            _xml = xml;
            _archive = archive;
            _statusCode = statusCode;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;

            HttpResponseMessage response;

            if (_statusCode != HttpStatusCode.OK)
            {
                response = new HttpResponseMessage(_statusCode);
            }
            else if (request.RequestUri?.AbsolutePath.EndsWith(
                ".nuspec",
                StringComparison.OrdinalIgnoreCase) == true)
            {
                response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_xml)
                };
            }
            else
            {
                response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(_archive)
                };
            }

            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }

    private static byte[] CreateLicenseArchive()
    {
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(
            stream,
            ZipArchiveMode.Create,
            leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry("LICENSE.txt");
            using Stream entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream);
            writer.Write("synthetic license text");
        }

        return stream.ToArray();
    }
}
