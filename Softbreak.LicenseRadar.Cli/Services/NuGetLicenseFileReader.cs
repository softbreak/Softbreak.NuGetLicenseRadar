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
                "Lisans dosyası yolu geçersiz."
            );
        }

        if (!nupkgStream.CanRead)
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.InvalidArchive,
                null,
                "Arşiv akışı okunabilir değil."
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
                        "Arşiv geçersiz bir ZIP giriş yolu içeriyor."
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
                        "Arşivde aynı lisans yolu birden fazla kez bulunuyor."
                    );
                }

                matchingEntry = entry;
            }

            if (matchingEntry is null)
            {
                return new LicenseFileReadResult(
                    LicenseFileReadStatus.NotFound,
                    null,
                    "Bildirilen lisans dosyası arşivde bulunamadı."
                );
            }

            return await ReadEntryAsync(matchingEntry);
        }
        catch (InvalidDataException)
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.InvalidArchive,
                null,
                "Nupkg içeriği geçerli bir ZIP arşivi değil."
            );
        }
        catch (IOException)
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.InvalidArchive,
                null,
                "ZIP arşivi okunurken giriş/çıkış hatası oluştu."
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
                "Lisans girdisinin boyut metadata'sı geçersiz."
            );
        }

        if (entry.Length > MaximumLicenseFileSizeBytes)
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.TooLarge,
                null,
                "Lisans dosyası 1 MiB sınırını aşıyor."
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
                    "Lisans dosyası okuma sırasında 1 MiB sınırını aşıyor."
                );
            }

            content.Write(buffer, 0, bytesRead);
        }

        if (totalRead != entry.Length)
        {
            return new LicenseFileReadResult(
                LicenseFileReadStatus.InvalidArchive,
                null,
                "Lisans girdisinin okunan boyutu metadata ile uyuşmuyor."
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
                "Lisans dosyası geçerli UTF-8 metni değil."
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
             char.IsAsciiLetter(slashPath[0]) &&
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
