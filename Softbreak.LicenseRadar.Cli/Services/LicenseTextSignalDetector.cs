using System.Text.RegularExpressions;
using Softbreak.LicenseRadar.Cli.Models;

namespace Softbreak.LicenseRadar.Cli.Services;

public sealed partial class LicenseTextSignalDetector
{
    private static readonly Regex ReciprocalRegex = new(
        @"\b(Reciprocal\s+Public\s+License(\s*1\.5)?|RPL[\s-]?1\.5|reciprocal\s+(public\s+)?license)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex AlternativeAgreementRegex = new(
        @"\b(license\s+agreement\s+described\s+here|alternative\s+(license\s+)?agreement|alternative\s+license|separate\s+(commercial\s+)?license(\s+agreement)?|commercial\s+license\s+agreement|dual[- ]licens(e|ing))\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex PaidCommercialRegex = new(
        @"\b((require[s]?|must\s+acquire|must\s+purchase)\s+(a\s+)?paid\s+commercial\s+license|paid\s+commercial\s+license|commercial\s+use\s+requires\s+(a\s+)?(paid\s+)?license|commercial\s+license\s+is\s+required|commercial\s+license\s+must\s+be\s+purchased|fee\s+for\s+commercial\s+use)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex NegationRegex = new(
        @"\b(?:no|without|never)\s+(?:a\s+)?(?:paid\s+)?commercial\s+license\b|" +
        @"\b(?:paid\s+)?commercial\s+license\s+(?:is|are|was|were)\s+(?:not\s+required|optional|not\s+necessary)\b|" +
        @"\bcommercial\s+use\s+does\s+not\s+require\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex UrlRegex = new(
        @"https?://[^\s()<>""']+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private static readonly Regex MarkdownLinkRegex = new(
        @"\[([^\]]+)\]\((https?://[^\s)]+)\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    public IReadOnlyList<LicenseTextSignal> Detect(string? licenseText)
    {
        if (string.IsNullOrWhiteSpace(licenseText))
        {
            return Array.Empty<LicenseTextSignal>();
        }

        string[] lines = licenseText.Split(
            new[] { "\r\n", "\r", "\n" },
            StringSplitOptions.None
        );

        var detected = new Dictionary<LicenseTextSignalKind, LicenseTextSignal>();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // 1. Reciprocal license mention
            if (!detected.ContainsKey(LicenseTextSignalKind.ReciprocalLicenseMention) &&
                ReciprocalRegex.IsMatch(line))
            {
                string evidence = CleanEvidenceText(line);
                string? url = ExtractUrl(lines, i);
                detected[LicenseTextSignalKind.ReciprocalLicenseMention] =
                    new LicenseTextSignal(
                        LicenseTextSignalKind.ReciprocalLicenseMention,
                        evidence,
                        url
                    );
            }

            // 2. Alternative license agreement
            if (!detected.ContainsKey(LicenseTextSignalKind.AlternativeLicenseAgreement) &&
                AlternativeAgreementRegex.IsMatch(line))
            {
                string evidence = CleanEvidenceText(line);
                string? url = ExtractUrl(lines, i);
                detected[LicenseTextSignalKind.AlternativeLicenseAgreement] =
                    new LicenseTextSignal(
                        LicenseTextSignalKind.AlternativeLicenseAgreement,
                        evidence,
                        url
                    );
            }

            // 3. Paid commercial license mention
            if (!detected.ContainsKey(LicenseTextSignalKind.PaidCommercialLicenseMention) &&
                PaidCommercialRegex.IsMatch(line) &&
                !NegationRegex.IsMatch(line))
            {
                string evidence = CleanEvidenceText(line);
                string? url = ExtractUrl(lines, i);
                detected[LicenseTextSignalKind.PaidCommercialLicenseMention] =
                    new LicenseTextSignal(
                        LicenseTextSignalKind.PaidCommercialLicenseMention,
                        evidence,
                        url
                    );
            }
        }

        return detected.Values.ToArray();
    }

    private static string CleanEvidenceText(string text)
    {
        string trimmed = text.Trim();
        if (trimmed.Length <= 300)
        {
            return trimmed;
        }

        return trimmed.Substring(0, 297).TrimEnd() + "...";
    }

    private static string? ExtractUrl(string[] lines, int currentIndex)
    {
        // First check current line for markdown link or direct url
        string currentLine = lines[currentIndex];
        var mdMatch = MarkdownLinkRegex.Match(currentLine);
        if (mdMatch.Success)
        {
            return NormalizeUrl(mdMatch.Groups[2].Value);
        }

        var urlMatch = UrlRegex.Match(currentLine);
        if (urlMatch.Success)
        {
            return NormalizeUrl(urlMatch.Value);
        }

        // Look at subsequent 1-2 lines for a standalone url
        for (int offset = 1; offset <= 2 && (currentIndex + offset) < lines.Length; offset++)
        {
            string nextLine = lines[currentIndex + offset].Trim();
            if (string.IsNullOrWhiteSpace(nextLine))
            {
                continue;
            }

            var nextUrlMatch = UrlRegex.Match(nextLine);
            if (nextUrlMatch.Success)
            {
                return NormalizeUrl(nextUrlMatch.Value);
            }

            // If next line contains other prose, stop searching
            break;
        }

        return null;
    }

    private static string NormalizeUrl(string url)
    {
        return url.TrimEnd('.', ',', ';', ')', ']', '"', '\'');
    }
}
