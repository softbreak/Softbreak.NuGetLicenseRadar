using System.IO.Compression;
using System.Net;
using System.Text;
using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public class LicenseScanServiceFileIntegrationTests
{
    [Fact]
    public async Task ScanAsync_Expression_OnlyRequestsMetadata()
    {
        var scan = await ScanAsync(CreateNuspec("expression", "MIT"));

        Assert.Equal(LicenseReviewStatus.MetadataOnly, scan.Result.Status);
        Assert.Equal(LicenseDeclarationQueryStatus.Success, scan.Result.QueryResult.Status);
        Assert.Null(scan.Result.LicenseFileResult);
        Assert.Single(scan.Handler.RequestUris);
        Assert.EndsWith(".nuspec", scan.Handler.RequestUris[0].AbsolutePath);
        AssertExpectedNuGetAddresses(scan.Handler.RequestUris);
    }

    [Fact]
    public async Task ScanAsync_FileAndValidLicense_RequestsMetadataAndArchive()
    {
        var scan = await ScanAsync(CreateNuspec("file", "LICENSE.txt"));

        Assert.Equal(LicenseDeclarationQueryStatus.Success, scan.Result.QueryResult.Status);
        Assert.Equal(LicenseFileReadStatus.Found, scan.Result.LicenseFileResult?.Status);
        Assert.Equal("synthetic license text", scan.Result.LicenseFileResult?.Content);
        Assert.Equal(LicenseReviewStatus.ReviewRequired, scan.Result.Status);
        Assert.Equal(2, scan.Handler.RequestUris.Count);
        Assert.EndsWith(".nuspec", scan.Handler.RequestUris[0].AbsolutePath);
        Assert.EndsWith(".nupkg", scan.Handler.RequestUris[1].AbsolutePath);
        AssertExpectedNuGetAddresses(scan.Handler.RequestUris);
    }

    [Fact]
    public async Task ScanAsync_FileMissingFromArchive_ReturnsExplicitFailure()
    {
        var scan = await ScanAsync(
            CreateNuspec("file", "LICENSE.txt"),
            _ => CreateOkResponse(
                new ByteArrayContent(CreateArchive(("README.md", "readme")))
            )
        );

        Assert.Equal(LicenseDeclarationQueryStatus.Success, scan.Result.QueryResult.Status);
        Assert.Equal(
            LicenseFileReadStatus.NotFound,
            scan.Result.LicenseFileResult?.Status
        );
        Assert.False(
            string.IsNullOrWhiteSpace(scan.Result.LicenseFileResult?.FailureReason)
        );
        Assert.Equal(LicenseReviewStatus.ReviewRequired, scan.Result.Status);
        Assert.Equal(2, scan.Handler.RequestUris.Count);
    }

    [Theory]
    [InlineData(
        "<package><metadata /></package>",
        LicenseDeclarationKind.Missing
    )]
    [InlineData(
        "<package><metadata><license type=\"url\">x</license></metadata></package>",
        LicenseDeclarationKind.Unsupported
    )]
    public async Task ScanAsync_NonFileDeclaration_DoesNotRequestArchive(
        string nuspec,
        LicenseDeclarationKind expectedKind)
    {
        var scan = await ScanAsync(nuspec);

        Assert.Equal(expectedKind, scan.Result.QueryResult.Declaration?.Kind);
        Assert.Null(scan.Result.LicenseFileResult);
        Assert.Single(scan.Handler.RequestUris);
        Assert.EndsWith(".nuspec", scan.Handler.RequestUris[0].AbsolutePath);
    }

    [Fact]
    public async Task ScanAsync_ArchiveHttpFailure_PreservesMetadataSuccess()
    {
        var scan = await ScanAsync(
            CreateNuspec("file", "LICENSE.txt"),
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        );

        Assert.Equal(LicenseDeclarationQueryStatus.Success, scan.Result.QueryResult.Status);
        Assert.Equal(
            LicenseFileReadStatus.DownloadFailure,
            scan.Result.LicenseFileResult?.Status
        );
        Assert.Contains(
            "503",
            scan.Result.LicenseFileResult?.FailureReason
        );
        Assert.Equal(LicenseReviewStatus.ReviewRequired, scan.Result.Status);
        Assert.Equal(2, scan.Handler.RequestUris.Count);
    }

    [Fact]
    public async Task ScanAsync_ArchiveContentLengthOverLimit_StopsBeforeReading()
    {
        var stream = new GeneratedReadStream(
            NuGetLicenseReader.MaximumPackageArchiveSizeBytes + 1L
        );

        var scan = await ScanAsync(
            CreateNuspec("file", "LICENSE.txt"),
            _ =>
            {
                var content = new StreamContent(stream);
                content.Headers.ContentLength =
                    NuGetLicenseReader.MaximumPackageArchiveSizeBytes + 1L;
                return CreateOkResponse(content);
            }
        );

        Assert.Equal(
            LicenseFileReadStatus.ArchiveTooLarge,
            scan.Result.LicenseFileResult?.Status
        );
        Assert.Equal(0, stream.BytesRead);
        Assert.Equal(LicenseDeclarationQueryStatus.Success, scan.Result.QueryResult.Status);
        Assert.Equal(2, scan.Handler.RequestUris.Count);
    }

    [Fact]
    public async Task ScanAsync_ArchiveStreamOverLimit_StopsAtLimitPlusOne()
    {
        var stream = new GeneratedReadStream(
            NuGetLicenseReader.MaximumPackageArchiveSizeBytes + 1L
        );

        var scan = await ScanAsync(
            CreateNuspec("file", "LICENSE.txt"),
            _ => CreateOkResponse(new StreamContent(stream))
        );

        Assert.Equal(
            LicenseFileReadStatus.ArchiveTooLarge,
            scan.Result.LicenseFileResult?.Status
        );
        Assert.Equal(
            NuGetLicenseReader.MaximumPackageArchiveSizeBytes + 1L,
            stream.BytesRead
        );
        Assert.Equal(LicenseDeclarationQueryStatus.Success, scan.Result.QueryResult.Status);
        Assert.Equal(2, scan.Handler.RequestUris.Count);
    }

    [Fact]
    public async Task ScanAsync_CorruptArchive_ReturnsInvalidArchive()
    {
        var scan = await ScanAsync(
            CreateNuspec("file", "LICENSE.txt"),
            _ => CreateOkResponse(
                new ByteArrayContent(Encoding.UTF8.GetBytes("not a zip"))
            )
        );

        Assert.Equal(LicenseDeclarationQueryStatus.Success, scan.Result.QueryResult.Status);
        Assert.Equal(
            LicenseFileReadStatus.InvalidArchive,
            scan.Result.LicenseFileResult?.Status
        );
        Assert.Equal(LicenseReviewStatus.ReviewRequired, scan.Result.Status);
        Assert.Equal(2, scan.Handler.RequestUris.Count);
    }

    [Theory]
    [InlineData("../Example.Package", "1.0.0")]
    [InlineData("Example.Package", "1.0.0/other")]
    public async Task ScanAsync_InvalidPackageIdentity_DoesNotSendRequest(
        string packageId,
        string version)
    {
        var package = new PackageDependency(
            packageId,
            version,
            "Direct",
            "net10.0"
        );
        var handler = new FakeHttpMessageHandler(
            CreateNuspec("file", "LICENSE.txt"),
            _ => CreateOkResponse(
                new ByteArrayContent(CreateArchive(("LICENSE.txt", "license")))
            )
        );
        using var httpClient = new HttpClient(handler);
        var service = new LicenseScanService(
            new NuGetLicenseReader(httpClient),
            new LicenseDeclarationReviewEvaluator()
        );

        LicenseDeclarationReviewResult result = await service.ScanAsync(package);

        Assert.Equal(
            LicenseDeclarationQueryStatus.InvalidPackageIdentity,
            result.QueryResult.Status
        );
        Assert.Equal(LicenseReviewStatus.Unknown, result.Status);
        Assert.Empty(handler.RequestUris);
    }

    [Fact]
    public async Task ScanAsync_RedirectResponse_IsReportedWithoutFollowingIt()
    {
        var scan = await ScanAsync(
            CreateNuspec("file", "LICENSE.txt"),
            _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Redirect);
                response.Headers.Location = new Uri("https://elsewhere.example/archive");
                return response;
            }
        );

        Assert.Equal(
            LicenseFileReadStatus.DownloadFailure,
            scan.Result.LicenseFileResult?.Status
        );
        Assert.Equal(2, scan.Handler.RequestUris.Count);
        Assert.All(
            scan.Handler.RequestUris,
            uri => Assert.Equal("api.nuget.org", uri.Host)
        );
    }

    private static async Task<(
        LicenseDeclarationReviewResult Result,
        FakeHttpMessageHandler Handler)> ScanAsync(
            string nuspec,
            Func<HttpRequestMessage, HttpResponseMessage>? archiveResponse = null)
    {
        var handler = new FakeHttpMessageHandler(
            nuspec,
            archiveResponse ?? new Func<HttpRequestMessage, HttpResponseMessage>(
                _ => CreateOkResponse(
                    new ByteArrayContent(
                        CreateArchive(("LICENSE.txt", "synthetic license text"))
                    )
                )
            )
        );
        using var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        var service = new LicenseScanService(
            new NuGetLicenseReader(httpClient),
            new LicenseDeclarationReviewEvaluator()
        );
        var package = new PackageDependency(
            "Example.Package",
            "1.0.0",
            "Direct",
            "net10.0"
        );

        LicenseDeclarationReviewResult result = await service.ScanAsync(package);
        return (result, handler);
    }

    private static string CreateNuspec(string type, string value)
    {
        return $"""
            <package>
              <metadata>
                <license type="{type}">{value}</license>
              </metadata>
            </package>
            """;
    }

    private static byte[] CreateArchive(params (string Path, string Content)[] files)
    {
        using var stream = new MemoryStream();

        using (var archive = new ZipArchive(
            stream,
            ZipArchiveMode.Create,
            leaveOpen: true))
        {
            foreach ((string path, string content) in files)
            {
                ZipArchiveEntry entry = archive.CreateEntry(path);
                using Stream entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream);
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }

    private static HttpResponseMessage CreateOkResponse(HttpContent content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content
        };
    }

    private static void AssertExpectedNuGetAddresses(IEnumerable<Uri> requestUris)
    {
        Assert.All(requestUris, uri =>
        {
            Assert.Equal("https", uri.Scheme);
            Assert.Equal("api.nuget.org", uri.Host);
            Assert.StartsWith("/v3-flatcontainer/", uri.AbsolutePath);
        });
    }

    private sealed class FakeHttpMessageHandler(
        string nuspec,
        Func<HttpRequestMessage, HttpResponseMessage> archiveResponse)
        : HttpMessageHandler
    {
        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Uri requestUri = request.RequestUri
                ?? throw new InvalidOperationException("Request URI is missing.");
            RequestUris.Add(requestUri);

            HttpResponseMessage response = requestUri.AbsolutePath.EndsWith(
                ".nuspec",
                StringComparison.OrdinalIgnoreCase
            )
                ? CreateOkResponse(new StringContent(nuspec))
                : archiveResponse(request);

            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }

    private sealed class GeneratedReadStream(long length) : Stream
    {
        private long _position;

        public long BytesRead => _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = (int)Math.Min(count, length - _position);
            Array.Clear(buffer, offset, bytesRead);
            _position += bytesRead;
            return bytesRead;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            int bytesRead = (int)Math.Min(buffer.Length, length - _position);
            buffer.Span[..bytesRead].Clear();
            _position += bytesRead;
            return ValueTask.FromResult(bytesRead);
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Read(buffer, offset, count));
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
    }
}
