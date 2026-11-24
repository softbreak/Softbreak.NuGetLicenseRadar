using System.Runtime.CompilerServices;

namespace Softbreak.LicenseRadar.Cli.Services;

/// <summary>
/// Backport of the .NET 8 ArgumentException guard for the net6.0 target.
/// </summary>
internal static class ArgumentGuards
{
    public static void ThrowIfNullOrWhiteSpace(
        string? argument,
        [CallerArgumentExpression(nameof(argument))] string? paramName = null)
    {
        if (argument is null)
        {
            throw new ArgumentNullException(paramName);
        }

        if (string.IsNullOrWhiteSpace(argument))
        {
            throw new ArgumentException("Value cannot be empty or whitespace.", paramName);
        }
    }
}
