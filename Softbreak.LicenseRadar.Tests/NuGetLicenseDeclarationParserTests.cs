using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;

namespace Softbreak.LicenseRadar.Tests;

public class NuGetLicenseDeclarationParserTests
{
    [Fact]
    public void Parse_ExpressionLicense_ReturnsExpressionAndValue()
    {
        const string xml = """
            <package>
              <metadata>
                <license type="expression">MIT</license>
              </metadata>
            </package>
            """;

        var parser = new NuGetLicenseDeclarationParser();

        NuGetLicenseDeclaration result = parser.Parse(xml);

        Assert.Equal(LicenseDeclarationKind.Expression, result.Kind);
        Assert.Equal("MIT", result.Value);
    }

    [Fact]
    public void Parse_FileLicense_ReturnsFileAndPath()
    {
        const string xml = """
            <package>
              <metadata>
                <license type="file">LICENSE.txt</license>
              </metadata>
            </package>
            """;

        var parser = new NuGetLicenseDeclarationParser();

        NuGetLicenseDeclaration result = parser.Parse(xml);

        Assert.Equal(LicenseDeclarationKind.File, result.Kind);
        Assert.Equal("LICENSE.txt", result.Value);
    }

    [Fact]
    public void Parse_MissingLicense_ReturnsMissing()
    {
        const string xml = """
            <package>
              <metadata />
            </package>
            """;

        var parser = new NuGetLicenseDeclarationParser();

        NuGetLicenseDeclaration result = parser.Parse(xml);

        Assert.Equal(LicenseDeclarationKind.Missing, result.Kind);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Parse_UnknownLicenseType_ReturnsUnsupported()
    {
        const string xml = """
            <package>
              <metadata>
                <license type="url">https://example.test/license</license>
              </metadata>
            </package>
            """;

        var parser = new NuGetLicenseDeclarationParser();

        NuGetLicenseDeclaration result = parser.Parse(xml);

        Assert.Equal(LicenseDeclarationKind.Unsupported, result.Kind);
        Assert.Equal("https://example.test/license", result.Value);
    }

    [Fact]
    public void Parse_NamespacedXml_ReturnsExpression()
    {
        const string xml = """
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <license type="expression">MIT</license>
              </metadata>
            </package>
            """;

        var parser = new NuGetLicenseDeclarationParser();

        NuGetLicenseDeclaration result = parser.Parse(xml);

        Assert.Equal(LicenseDeclarationKind.Expression, result.Kind);
        Assert.Equal("MIT", result.Value);
    }

    [Fact]
    public void Parse_MissingMetadata_ThrowsFormatException()
    {
        const string xml = """
            <package>
              <other />
            </package>
            """;

        var parser = new NuGetLicenseDeclarationParser();

        Assert.Throws<FormatException>(() => parser.Parse(xml));
    }
}
