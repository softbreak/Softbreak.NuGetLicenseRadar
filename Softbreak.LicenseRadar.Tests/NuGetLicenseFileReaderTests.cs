using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Softbreak.LicenseRadar.Cli.Models;
using Softbreak.LicenseRadar.Cli.Services;
using Xunit;

namespace Softbreak.LicenseRadar.Tests;

public class NuGetLicenseFileReaderTests
{
    [Fact]
    public async Task ReadAsync_RootLicenseFile_ReturnsContentAndLeavesStreamOpen()
    {
        using MemoryStream archive = CreateArchive(
            ("LICENSE.txt", Encoding.UTF8.GetBytes("license text"))
        );

        LicenseFileReadResult result =
            await new NuGetLicenseFileReader().ReadAsync(archive, "LICENSE.txt");

        Assert.Equal(LicenseFileReadStatus.Found, result.Status);
        Assert.Equal("license text", result.Content);
        Assert.True(archive.CanRead);
    }

    [Fact]
    public async Task ReadAsync_NestedLicenseFile_AcceptsBackslashPath()
    {
        using MemoryStream archive = CreateArchive(
            ("docs/LICENSE.md", Encoding.UTF8.GetBytes("nested license"))
        );

        LicenseFileReadResult result =
            await new NuGetLicenseFileReader().ReadAsync(
                archive,
                "docs\\LICENSE.md"
            );

        Assert.Equal(LicenseFileReadStatus.Found, result.Status);
        Assert.Equal("nested license", result.Content);
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ReturnsNotFound()
    {
        using MemoryStream archive = CreateArchive(
            ("LICENSE.txt", Encoding.UTF8.GetBytes("license text"))
        );

        LicenseFileReadResult result =
            await new NuGetLicenseFileReader().ReadAsync(archive, "NOTICE.txt");

        Assert.Equal(LicenseFileReadStatus.NotFound, result.Status);
        Assert.Null(result.Content);
    }

    [Theory]
    [InlineData("../LICENSE.txt")]
    [InlineData("/LICENSE.txt")]
    [InlineData("C:\\LICENSE.txt")]
    [InlineData("docs/../LICENSE.txt")]
    public async Task ReadAsync_InvalidLicensePath_ReturnsInvalidPath(string path)
    {
        using MemoryStream archive = CreateArchive(
            ("LICENSE.txt", Encoding.UTF8.GetBytes("license text"))
        );

        LicenseFileReadResult result =
            await new NuGetLicenseFileReader().ReadAsync(archive, path);

        Assert.Equal(LicenseFileReadStatus.InvalidPath, result.Status);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task ReadAsync_EntryMetadataExceedsLimit_ReturnsTooLarge()
    {
        byte[] content = new byte[
            NuGetLicenseFileReader.MaximumLicenseFileSizeBytes + 1
        ];
        using MemoryStream archive = CreateArchive(("LICENSE.txt", content));

        LicenseFileReadResult result =
            await new NuGetLicenseFileReader().ReadAsync(archive, "LICENSE.txt");

        Assert.Equal(LicenseFileReadStatus.TooLarge, result.Status);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task ReadAsync_FileAtMaximumSize_ReturnsContent()
    {
        byte[] content = Enumerable.Repeat(
            (byte)'a',
            NuGetLicenseFileReader.MaximumLicenseFileSizeBytes
        ).ToArray();
        using MemoryStream archive = CreateArchive(("LICENSE.txt", content));

        LicenseFileReadResult result =
            await new NuGetLicenseFileReader().ReadAsync(archive, "LICENSE.txt");

        Assert.Equal(LicenseFileReadStatus.Found, result.Status);
        Assert.Equal(
            NuGetLicenseFileReader.MaximumLicenseFileSizeBytes,
            result.Content?.Length
        );
    }

    [Fact]
    public async Task ReadAsync_ActualContentExceedsLimit_ReturnsTooLarge()
    {
        byte[] content = new byte[
            NuGetLicenseFileReader.MaximumLicenseFileSizeBytes + 1
        ];
        using MemoryStream validArchive = CreateArchive(("LICENSE.txt", content));
        byte[] archiveBytes = validArchive.ToArray();
        SetDeclaredEntryLength(archiveBytes, 1);
        using var archive = new MemoryStream(archiveBytes);

        LicenseFileReadResult result =
            await new NuGetLicenseFileReader().ReadAsync(archive, "LICENSE.txt");

        Assert.Equal(LicenseFileReadStatus.TooLarge, result.Status);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task ReadAsync_InvalidArchive_ReturnsInvalidArchiveAndLeavesStreamOpen()
    {
        using var archive = new MemoryStream(Encoding.UTF8.GetBytes("not a zip"));

        LicenseFileReadResult result =
            await new NuGetLicenseFileReader().ReadAsync(archive, "LICENSE.txt");

        Assert.Equal(LicenseFileReadStatus.InvalidArchive, result.Status);
        Assert.Null(result.Content);
        Assert.True(archive.CanRead);
    }

    [Fact]
    public async Task ReadAsync_Utf8Bom_ReturnsTextWithoutBom()
    {
        byte[] content = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("license with BOM"))
            .ToArray();
        using MemoryStream archive = CreateArchive(("LICENSE.txt", content));

        LicenseFileReadResult result =
            await new NuGetLicenseFileReader().ReadAsync(archive, "LICENSE.txt");

        Assert.Equal(LicenseFileReadStatus.Found, result.Status);
        Assert.Equal("license with BOM", result.Content);
    }

    [Fact]
    public async Task ReadAsync_InvalidUtf8_ReturnsInvalidText()
    {
        using MemoryStream archive = CreateArchive(
            ("LICENSE.txt", [0xc3, 0x28])
        );

        LicenseFileReadResult result =
            await new NuGetLicenseFileReader().ReadAsync(archive, "LICENSE.txt");

        Assert.Equal(LicenseFileReadStatus.InvalidText, result.Status);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task ReadAsync_ArchiveEntryWithTraversalPath_ReturnsInvalidArchive()
    {
        using MemoryStream archive = CreateArchive(
            ("../LICENSE.txt", Encoding.UTF8.GetBytes("license text"))
        );

        LicenseFileReadResult result =
            await new NuGetLicenseFileReader().ReadAsync(archive, "LICENSE.txt");

        Assert.Equal(LicenseFileReadStatus.InvalidArchive, result.Status);
        Assert.Null(result.Content);
    }

    private static MemoryStream CreateArchive(
        params (string Path, byte[] Content)[] files)
    {
        var archiveStream = new MemoryStream();

        using (var archive = new ZipArchive(
            archiveStream,
            ZipArchiveMode.Create,
            leaveOpen: true))
        {
            foreach ((string path, byte[] content) in files)
            {
                ZipArchiveEntry entry = archive.CreateEntry(
                    path,
                    CompressionLevel.NoCompression
                );
                using Stream entryStream = entry.Open();
                entryStream.Write(content);
            }
        }

        archiveStream.Position = 0;
        return archiveStream;
    }

    private static void SetDeclaredEntryLength(byte[] archive, uint length)
    {
        int centralDirectoryOffset = FindSignature(
            archive,
            [0x50, 0x4b, 0x01, 0x02]
        );
        int localHeaderOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(
            archive.AsSpan(centralDirectoryOffset + 42, sizeof(uint))
        );

        BinaryPrimitives.WriteUInt32LittleEndian(
            archive.AsSpan(centralDirectoryOffset + 24, sizeof(uint)),
            length
        );
        BinaryPrimitives.WriteUInt32LittleEndian(
            archive.AsSpan(localHeaderOffset + 22, sizeof(uint)),
            length
        );
    }

    private static int FindSignature(byte[] data, byte[] signature)
    {
        for (int offset = 0; offset <= data.Length - signature.Length; offset++)
        {
            if (data.AsSpan(offset, signature.Length).SequenceEqual(signature))
            {
                return offset;
            }
        }

        throw new InvalidOperationException("ZIP signature was not found.");
    }
}
