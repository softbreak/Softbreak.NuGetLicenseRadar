
namespace Softbreak.LicenseRadar.Cli.Models;

public enum LicenseLookupStatus
{
    ExpressionFound,
    ExpressionNotFound,
    QueryFailed
}

public sealed record PackageLicenseResult(
    PackageDependency Package,
    string? LicenseExpression,
    LicenseLookupStatus Status
);
