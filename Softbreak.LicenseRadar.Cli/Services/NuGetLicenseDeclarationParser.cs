
using System.Xml.Linq;
using Softbreak.LicenseRadar.Cli.Models;

namespace Softbreak.LicenseRadar.Cli.Services;

public sealed class NuGetLicenseDeclarationParser
{
    public NuGetLicenseDeclaration Parse(string xml)
    {
        ArgumentGuards.ThrowIfNullOrWhiteSpace(xml);

        XDocument document = XDocument.Parse(xml);

        // NuGet metadata bölümünü bul
        XElement? metadata = document.Root?
            .Elements()
            .FirstOrDefault(x => x.Name.LocalName == "metadata");

        if (metadata is null)
        {
            throw new FormatException(
                "NuGet metadata bölümü bulunamadı.");
        }

        // Lisans bildirimini bul
        XElement? license = metadata
            .Elements()
            .FirstOrDefault(x => x.Name.LocalName == "license");

        if (license is null)
        {
            return new NuGetLicenseDeclaration(
                LicenseDeclarationKind.Missing,
                null
            );
        }

        string type = license.Attribute("type")?.Value ?? "";
        string value = license.Value.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            return new NuGetLicenseDeclaration(
                LicenseDeclarationKind.Unsupported,
                null
            );
        }

        if (type.Equals(
            "expression",
            StringComparison.OrdinalIgnoreCase))
        {
            return new NuGetLicenseDeclaration(
                LicenseDeclarationKind.Expression,
                value
            );
        }

        if (type.Equals(
            "file",
            StringComparison.OrdinalIgnoreCase))
        {
            return new NuGetLicenseDeclaration(
                LicenseDeclarationKind.File,
                value
            );
        }

        return new NuGetLicenseDeclaration(
            LicenseDeclarationKind.Unsupported,
            value
        );
    }
}
