using L = Softbreak.LicenseRadar.Cli.Localization.UiText;
using Softbreak.LicenseRadar.Cli.Models;

namespace Softbreak.LicenseRadar.Cli.Services;

public sealed class LicenseChangeDetector
{
    private static readonly PackageFrameworkKeyComparer KeyComparer = new();

    public IReadOnlyList<LicenseChangeResult> DetectChanges(
        IEnumerable<PackageLicenseDeclarationResult> previousScan,
        IEnumerable<PackageLicenseDeclarationResult> currentScan)
    {
        ArgumentNullException.ThrowIfNull(previousScan);
        ArgumentNullException.ThrowIfNull(currentScan);

        Dictionary<PackageFrameworkKey, List<PackageLicenseDeclarationResult>>
            previous = GroupByPackageAndFramework(previousScan);
        Dictionary<PackageFrameworkKey, List<PackageLicenseDeclarationResult>>
            current = GroupByPackageAndFramework(currentScan);
        var keys = new HashSet<PackageFrameworkKey>(previous.Keys, KeyComparer);
        keys.UnionWith(current.Keys);
        var changes = new List<LicenseChangeResult>();

        foreach (PackageFrameworkKey key in keys
            .OrderBy(key => key.PackageId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(key => key.Framework, StringComparer.OrdinalIgnoreCase))
        {
            previous.TryGetValue(key, out List<PackageLicenseDeclarationResult>? oldRecords);
            current.TryGetValue(key, out List<PackageLicenseDeclarationResult>? newRecords);

            if (oldRecords is null)
            {
                foreach (PackageLicenseDeclarationResult record in newRecords!)
                {
                    changes.Add(CreateChange(
                        null,
                        record,
                        LicenseChangeType.PackageAdded,
                        GetPackagePresenceReason(record, wasAdded: true)
                    ));
                }

                continue;
            }

            if (newRecords is null)
            {
                foreach (PackageLicenseDeclarationResult record in oldRecords)
                {
                    changes.Add(CreateChange(
                        record,
                        null,
                        LicenseChangeType.PackageRemoved,
                        GetPackagePresenceReason(record, wasAdded: false)
                    ));
                }

                continue;
            }

            if (oldRecords.Count != 1 || newRecords.Count != 1)
            {
                string reason = L.Get("Change.Duplicate");

                foreach (PackageLicenseDeclarationResult record in oldRecords)
                {
                    changes.Add(CreateChange(
                        record,
                        null,
                        LicenseChangeType.NotComparable,
                        reason
                    ));
                }

                foreach (PackageLicenseDeclarationResult record in newRecords)
                {
                    changes.Add(CreateChange(
                        null,
                        record,
                        LicenseChangeType.NotComparable,
                        reason
                    ));
                }

                continue;
            }

            CompareMatchedRecords(
                oldRecords[0],
                newRecords[0],
                changes
            );
        }

        return changes;
    }

    private static Dictionary<PackageFrameworkKey, List<PackageLicenseDeclarationResult>>
        GroupByPackageAndFramework(
            IEnumerable<PackageLicenseDeclarationResult> scan)
    {
        var groups = new Dictionary<
            PackageFrameworkKey,
            List<PackageLicenseDeclarationResult>
        >(KeyComparer);

        foreach (PackageLicenseDeclarationResult result in scan)
        {
            var key = new PackageFrameworkKey(
                result.Package.Name,
                result.Package.Framework
            );

            if (!groups.TryGetValue(key, out List<PackageLicenseDeclarationResult>? records))
            {
                records = [];
                groups.Add(key, records);
            }

            records.Add(result);
        }

        return groups;
    }

    private static void CompareMatchedRecords(
        PackageLicenseDeclarationResult previous,
        PackageLicenseDeclarationResult current,
        List<LicenseChangeResult> changes)
    {
        if (!string.Equals(
            previous.Package.Version,
            current.Package.Version,
            StringComparison.Ordinal))
        {
            changes.Add(CreateChange(
                previous,
                current,
                LicenseChangeType.PackageVersionChanged,
                L.Get("Change.Version")
            ));
        }

        if (previous.Status != LicenseDeclarationQueryStatus.Success ||
            current.Status != LicenseDeclarationQueryStatus.Success ||
            previous.Declaration is null ||
            current.Declaration is null)
        {
            changes.Add(CreateChange(
                previous,
                current,
                LicenseChangeType.NotComparable,
                GetNotComparableReason(previous, current)
            ));
            return;
        }

        NuGetLicenseDeclaration oldDeclaration = previous.Declaration;
        NuGetLicenseDeclaration newDeclaration = current.Declaration;

        if (oldDeclaration.Kind == LicenseDeclarationKind.Missing &&
            newDeclaration.Kind != LicenseDeclarationKind.Missing)
        {
            changes.Add(CreateChange(
                previous,
                current,
                LicenseChangeType.LicenseInformationAdded,
                L.Get("Change.InfoAdded")
            ));

            return;
        }

        if (oldDeclaration.Kind != LicenseDeclarationKind.Missing &&
            newDeclaration.Kind == LicenseDeclarationKind.Missing)
        {
            changes.Add(CreateChange(
                previous,
                current,
                LicenseChangeType.LicenseInformationRemoved,
                L.Get("Change.InfoRemoved")
            ));

            return;
        }

        if (oldDeclaration.Kind != newDeclaration.Kind)
        {
            changes.Add(CreateChange(
                previous,
                current,
                LicenseChangeType.DeclarationTypeChanged,
                L.Get("Change.Type")
            ));
        }

        if (!string.Equals(
            oldDeclaration.Value,
            newDeclaration.Value,
            StringComparison.Ordinal))
        {
            changes.Add(CreateChange(
                previous,
                current,
                LicenseChangeType.DeclarationValueChanged,
                L.Get("Change.Value")
            ));
        }
    }

    private static string GetPackagePresenceReason(
        PackageLicenseDeclarationResult record,
        bool wasAdded)
    {
        string presence = wasAdded
            ? L.Get("Change.Added")
            : L.Get("Change.Removed");

        return record.Status == LicenseDeclarationQueryStatus.Success &&
            record.Declaration is not null
                ? presence
                : $"{presence} {L.Get("Change.Unavailable")}";
    }

    private static string GetNotComparableReason(
        PackageLicenseDeclarationResult previous,
        PackageLicenseDeclarationResult current)
    {
        var reasons = new List<string>();

        if (previous.Status != LicenseDeclarationQueryStatus.Success)
        {
            reasons.Add(L.Format("Change.PreviousStatus", previous.Status));
        }
        else if (previous.Declaration is null)
        {
            reasons.Add(L.Get("Change.PreviousMissing"));
        }

        if (current.Status != LicenseDeclarationQueryStatus.Success)
        {
            reasons.Add(L.Format("Change.CurrentStatus", current.Status));
        }
        else if (current.Declaration is null)
        {
            reasons.Add(L.Get("Change.CurrentMissing"));
        }

        reasons.Add(L.Get("Change.NotComparable"));
        return string.Join(" ", reasons);
    }

    private static LicenseChangeResult CreateChange(
        PackageLicenseDeclarationResult? previous,
        PackageLicenseDeclarationResult? current,
        LicenseChangeType changeType,
        string reason)
    {
        return new LicenseChangeResult(
            previous?.Package,
            current?.Package,
            GetComparableDeclaration(previous),
            GetComparableDeclaration(current),
            previous?.Status,
            current?.Status,
            changeType,
            reason
        );
    }

    private static NuGetLicenseDeclaration? GetComparableDeclaration(
        PackageLicenseDeclarationResult? result)
    {
        return result?.Status == LicenseDeclarationQueryStatus.Success
            ? result.Declaration
            : null;
    }

    private readonly record struct PackageFrameworkKey(
        string PackageId,
        string Framework
    );

    private sealed class PackageFrameworkKeyComparer
        : IEqualityComparer<PackageFrameworkKey>
    {
        public bool Equals(
            PackageFrameworkKey left,
            PackageFrameworkKey right)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(
                    left.PackageId,
                    right.PackageId) &&
                StringComparer.OrdinalIgnoreCase.Equals(
                    left.Framework,
                    right.Framework);
        }

        public int GetHashCode(PackageFrameworkKey key)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(key.PackageId),
                StringComparer.OrdinalIgnoreCase.GetHashCode(key.Framework)
            );
        }
    }
}
