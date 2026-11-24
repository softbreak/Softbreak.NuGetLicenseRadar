using System.Globalization;
using System.Runtime.CompilerServices;

namespace Softbreak.LicenseRadar.Tests;

// Preserve existing Turkish assertions independently of the CI host's UI culture.
// Production language selection is unaffected; locale-specific tests override it.
internal static class TestCultureInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
    }
}
