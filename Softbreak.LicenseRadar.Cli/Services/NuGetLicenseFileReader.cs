using L = Softbreak.LicenseRadar.Cli.Localization.UiText;
using System.IO.Compression;
using System.Text;
using Softbreak.LicenseRadar.Cli.Models;

namespace Softbreak.LicenseRadar.Cli.Services;

public sealed class NuGetLicenseFileReader
{
    public const int MaximumLicenseFileSizeBytes = 1024 * 1024;

    private static readonly char[] InvalidPathCharacters =
        ['<', '>', ':', '"', '|', '?', '*'];

    /// <summary>
    /// Reads the declared license file without closing or taking ownership of the stream.
    /// </summary>
    public async Task<LicenseFileReadResult> ReadAsync(
        Stream nupkgStream,
        string licenseFilePath)
    {
        ArgumentNullException.ThrowIfNull(nupkgStream);

        if (!TryNormalizeLicensePath(licenseFilePath, out string normalizedPath))
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.InvalidPath,
                null,
                L.Get("Archive.InvalidPath")
            );
        }

        if (!nupkgStream.CanRead)
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.InvalidArchive,
                null,
                L.Get("Archive.UnreadableStream")
            );
        }

        try
        {
            using var archive = new ZipArchive(
                nupkgStream,
                ZipArchiveMode.Read,
                leaveOpen: true
            );

            ZipArchiveEntry? matchingEntry = null;

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!TryNormalizeArchiveEntryPath(
                    entry.FullName,
                    out string entryPath,
                    out bool isDirectory))
                {
                    return new LicenseFileReadResult(
                        LicenseFileReadStatus.InvalidArchive,
                        null,
                        L.Get("Archive.InvalidEntry")
                    );
                }

                if (isDirectory ||
                    !entryPath.Equals(normalizedPath, StringComparison.Ordinal))
                {
                    continue;
                }

                if (matchingEntry is not null)
                {
                    return new LicenseFileReadResult(
                        LicenseFileReadStatus.InvalidArchive,
                        null,
                        L.Get("Archive.DuplicatePath")
                    );
                }

                matchingEntry = entry;
            }

            if (matchingEntry is null)
            {
                return new LicenseFileReadResult(
                    LicenseFileReadStatus.NotFound,
                    null,
                    L.Get("Archive.FileNotFound")
                );
            }

            return await ReadEntryAsync(matchingEntry);
        }
        catch (InvalidDataException)
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.InvalidArchive,
                null,
                L.Get("Archive.InvalidZip")
            );
        }
        catch (IOException)
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.InvalidArchive,
                null,
                L.Get("Archive.IoFailure")
            );
        }
    }

    private static async Task<LicenseFileReadResult> ReadEntryAsync(
        ZipArchiveEntry entry)
    {
        if (entry.Length < 0)
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.InvalidArchive,
                null,
                L.Get("Archive.InvalidLength")
            );
        }

        if (entry.Length > MaximumLicenseFileSizeBytes)
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.TooLarge,
                null,
                L.Get("Archive.TooLarge")
            );
        }

        using Stream entryStream = entry.Open();
        using var content = new MemoryStream((int)entry.Length);
        byte[] buffer = new byte[81920];
        long totalRead = 0;

        while (true)
        {
            int bytesToRead = (int)Math.Min(
                buffer.Length,
                MaximumLicenseFileSizeBytes + 1L - totalRead
            );
            int bytesRead = await entryStream.ReadAsync(
                buffer.AsMemory(0, bytesToRead)
            );

            if (bytesRead == 0)
            {
                break;
            }

            totalRead += bytesRead;
            if (totalRead > MaximumLicenseFileSizeBytes)
            {
                return new LicenseFileReadResult(
                    LicenseFileReadStatus.TooLarge,
                    null,
                    L.Get("Archive.ExceededReading")
                );
            }

            content.Write(buffer, 0, bytesRead);
        }

        if (totalRead != entry.Length)
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.InvalidArchive,
                null,
                L.Get("Archive.LengthMismatch")
            );
        }

        byte[] bytes = content.ToArray();
        int bomLength = bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble())
            ? Encoding.UTF8.GetPreamble().Length
            : 0;

        try
        {
            string text = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true
            ).GetString(bytes, bomLength, bytes.Length - bomLength);

            return new LicenseFileReadResult(
                LicenseFileReadStatus.Found,
                text
            );
        }
        catch (DecoderFallbackException)
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.InvalidText,
                null,
                L.Get("Archive.InvalidUtf8")
            );
        }
    }

    private static bool TryNormalizeLicensePath(
        string path,
        out string normalizedPath)
    {
        return TryNormalizePath(path, allowDirectory: false, out normalizedPath, out _);
    }

    private static bool TryNormalizeArchiveEntryPath(
        string path,
        out string normalizedPath,
        out bool isDirectory)
    {
        return TryNormalizePath(
            path,
            allowDirectory: true,
            out normalizedPath,
            out isDirectory
        );
    }

    private static bool TryNormalizePath(
        string path,
        bool allowDirectory,
        out string normalizedPath,
        out bool isDirectory)
    {
        normalizedPath = "";
        isDirectory = false;

        if (string.IsNullOrWhiteSpace(path) ||
            path.Any(char.IsControl) ||
            path.IndexOfAny(InvalidPathCharacters) >= 0)
        {
            return false;
        }

        string slashPath = path.Replace('\\', '/');
        isDirectory = allowDirectory &&
            slashPath.EndsWith("/", StringComparison.Ordinal);

        if (isDirectory)
        {
            slashPath = slashPath[..^1];
        }

        if (slashPath.Length == 0 ||
            slashPath.StartsWith("/", StringComparison.Ordinal) ||
            Path.IsPathRooted(path) ||
            (slashPath.Length >= 2 &&
             ((slashPath[0] >= 'A' && slashPath[0] <= 'Z') ||
              (slashPath[0] >= 'a' && slashPath[0] <= 'z')) &&
             slashPath[1] == ':'))
        {
            return false;
        }

        string[] segments = slashPath.Split('/');
        if (segments.Any(segment =>
            segment.Length == 0 ||
            segment is "." or ".."))
        {
            return false;
        }

        normalizedPath = string.Join("/", segments);
        return true;
    }
}
