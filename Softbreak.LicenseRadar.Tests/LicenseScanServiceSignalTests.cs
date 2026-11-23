using System.IO.Compression;
using System.Net;
using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public class LicenseScanServiceSignalTests
{
    private const string AutoMapperLicenseText = """
        By accessing code under the [Lucky Penny Software GitHub Organization](https://github.com/LuckyPennySoftware) (Lucky Penny Software) here, you are agreeing to the following licensing terms.
        If you do not agree to these terms, do not access Lucky Penny Software code.

        Your license to Lucky Penny Software source code and/or binaries is governed by the Reciprocal Public License 1.5 (RPL1.5) license as described here: 

        https://opensource.org/license/rpl-1-5/

        If you do not wish to release the source of software you build using Lucky Penny Software source code and/or binaries under the terms above, you may use Lucky Penny Software source code and/or binaries under the License Agreement described here:

        https://luckypennysoftware.com/license
        """;

    private const string FluentAssertionsLicenseText = """
        XCEED SOFTWARE INC. 

        COMMUNITY LICENSE AGREEMENT 

        FLUENT ASSERTIONS 

        (for Non-Commercial Use) 

        DEFINITION OF NON-COMMERCIAL USE 

        Any use outside of these parameters requires a paid Commercial License from Xceed.  
        """;

    [Fact]
    public async Task ScanAsync_AutoMapperLicenseFile_DetectsSignalsAndProvidesReviewReason()
    {
        const string nuspec = """
            <package>
              <metadata>
                <license type="file">LICENSE.md</license>
              </metadata>
            </package>
            """;

        var (result, _, _) = await ScanAsync(
            "AutoMapper",
            "15.0.0",
            nuspec,
            "LICENSE.md",
            AutoMapperLicenseText
        );

        Assert.Equal(LicenseReviewStatus.ReviewRequired, result.Status);
        Assert.Equal(2, result.Signals.Count);
        Assert.Contains("karşılıklı lisans", result.Reason);
        Assert.Contains("alternatif lisans sözleşmesi", result.Reason);
        Assert.False(string.IsNullOrWhiteSpace(result.EvidenceUrl));
        Assert.Equal(LicenseFileReadStatus.Found, result.LicenseFileResult?.Status);
    }

    [Fact]
    public async Task ScanAsync_FluentAssertionsLicenseFile_DetectsPaidCommercialSignal()
    {
        const string nuspec = """
            <package>
              <metadata>
                <license type="file">LICENSE.md</license>
              </metadata>
            </package>
            """;

        var (result, _, _) = await ScanAsync(
            "FluentAssertions",
            "8.0.0",
            nuspec,
            "LICENSE.md",
            FluentAssertionsLicenseText
        );

        Assert.Equal(LicenseReviewStatus.ReviewRequired, result.Status);
        LicenseTextSignal signal = Assert.Single(result.Signals);
        Assert.Equal(LicenseTextSignalKind.PaidCommercialLicenseMention, signal.Kind);
        Assert.Contains("ücretli ticari lisans gereksinimi", result.Reason);
    }

    [Fact]
    public async Task ScanAsync_ExpressionLicense_DoesNotFabricateSignals()
    {
        const string nuspec = """
            <package>
              <metadata>
                <license type="expression">MIT</license>
              </metadata>
            </package>
            """;

        var (result, _, _) = await ScanAsync(
            "Example.Package",
            "1.0.0",
            nuspec,
            "LICENSE.txt",
            "dummy"
        );

        Assert.Equal(LicenseReviewStatus.MetadataOnly, result.Status);
        Assert.Empty(result.Signals);
        Assert.Null(result.LicenseFileResult);
    }

    [Fact]
    public async Task Combiner_AutoMapper15_PreservesOfficialUrlAndWarningWhileRetainingSignals()
    {
        const string nuspec = """
            <package>
              <metadata>
                <license type="file">LICENSE.md</license>
              </metadata>
            </package>
            """;

        var (result, _, _) = await ScanAsync(
            "AutoMapper",
            "15.0.0",
            nuspec,
            "LICENSE.md",
            AutoMapperLicenseText
        );

        var combiner = new LicenseReviewDecisionCombiner(new LicenseReviewEvaluator());
        LicenseDeclarationReviewResult combined = combiner.Combine(result);

        Assert.Equal(LicenseReviewStatus.ReviewRequired, combined.Status);
        Assert.Contains("AutoMapper 15.0 ve sonrasında lisanslama koşulları değişti", combined.Reason);
        Assert.Equal("https://docs.automapper.io/en/stable/15.0-Upgrade-Guide.html", combined.EvidenceUrl);
        Assert.Equal(2, combined.Signals.Count);
        Assert.Equal(2, combined.LicenseFileResult?.Signals.Count);
    }

    private static async Task<(
        LicenseDeclarationReviewResult Result,
        PackageDependency Package,
        FakeHttpMessageHandler Handler)> ScanAsync(
            string packageName,
            string packageVersion,
            string xml,
            string licenseFileName,
            string licenseFileContent)
    {
        byte[] archive = CreateZipArchive(licenseFileName, licenseFileContent);
        var handler = new FakeHttpMessageHandler(xml, archive);
        using var httpClient = new HttpClient(handler);
        var reader = new NuGetLicenseReader(httpClient);
        var evaluator = new LicenseDeclarationReviewEvaluator();
        var service = new LicenseScanService(reader, evaluator);
        var package = new PackageDependency(
            packageName,
            packageVersion,
            "Direct",
            "net10.0"
        );

        LicenseDeclarationReviewResult result = await service.ScanAsync(package);
        return (result, package, handler);
    }

    private static byte[] CreateZipArchive(string entryName, string content)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName);
            using Stream entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream);
            writer.Write(content);
        }
        return stream.ToArray();
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _xml;
        private readonly byte[] _archive;

        public FakeHttpMessageHandler(string xml, byte[] archive)
        {
            _xml = xml;
            _archive = archive;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response;
            if (request.RequestUri?.AbsolutePath.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) == true)
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
}
