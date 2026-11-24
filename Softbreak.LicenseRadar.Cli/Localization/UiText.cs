using System.Globalization;
using System.Resources;

namespace Softbreak.LicenseRadar.Cli.Localization;

/// <summary>
/// Localized user-facing CLI text. Turkish is selected for tr-* UI cultures;
/// all other UI cultures use English. Structured status values remain invariant.
/// </summary>
public static class UiText
{
    private static readonly ResourceManager Manager = new(
        "Softbreak.LicenseRadar.Cli.Localization.Messages",
        typeof(UiText).Assembly);

    private static CultureInfo SelectedCulture =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr"
            ? CultureInfo.GetCultureInfo("tr")
            : CultureInfo.GetCultureInfo("en");

    public static string Get(string key) =>
        Manager.GetString(key, SelectedCulture)
        ?? throw new MissingManifestResourceException($"Missing UI resource: {key}");

    public static string Format(string key, params object?[] args) =>
        string.Format(SelectedCulture, Get(key), args);
}
