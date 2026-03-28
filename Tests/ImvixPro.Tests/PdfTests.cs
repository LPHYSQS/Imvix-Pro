using ImvixPro.Models;
using ImvixPro.Services;
using ImvixPro.Services.PdfModule;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ImvixPro.Tests;

public sealed class PdfTests
{
    [Fact]
    public void PdfOutputFormat_UsesExpectedExtensionAndTransparencyRules()
    {
        Assert.Equal(".pdf", ImageConversionService.GetFileExtension(OutputImageFormat.Pdf));
        Assert.False(ImageConversionService.OutputFormatSupportsTransparency(OutputImageFormat.Pdf));
    }

    [Fact]
    public void SupportedInputExtensions_IncludePdfSources()
    {
        Assert.Contains(".pdf", ImageConversionService.SupportedInputExtensions);
    }

    [Fact]
    public void PdfImportService_ImportsPlainPdfWithoutLockState()
    {
        var service = new PdfImportService();

        var success = service.TryCreate(GetFixturePath("sample-plain.pdf"), out var item, out var error, generateThumbnail: false);

        try
        {
            Assert.True(success, error);
            Assert.NotNull(item);
            Assert.True(item!.IsPdfDocument);
            Assert.False(item.IsEncrypted);
            Assert.True(item.IsUnlocked);
            Assert.False(item.NeedsPdfUnlock);
            Assert.True(item.PdfPageCount > 0);
        }
        finally
        {
            item?.Dispose();
        }
    }

    [Fact]
    public void PdfImportService_ImportsEncryptedPdfAsLocked()
    {
        var service = new PdfImportService();

        var success = service.TryCreate(GetFixturePath("sample-encrypted.pdf"), out var item, out var error, generateThumbnail: false);

        try
        {
            Assert.True(success, error);
            Assert.NotNull(item);
            Assert.True(item!.IsPdfDocument);
            Assert.True(item.IsEncrypted);
            Assert.False(item.IsUnlocked);
            Assert.True(item.NeedsPdfUnlock);
        }
        finally
        {
            item?.Dispose();
        }
    }

    [Fact]
    public void PdfSecurityService_RejectsWrongPassword()
    {
        var securityService = new PdfSecurityService();

        try
        {
            var success = securityService.TryUnlock(
                GetFixturePath("sample-encrypted.pdf"),
                "wrong-pass",
                out _,
                out var errorCode,
                out var errorMessage);

            Assert.False(success);
            Assert.Equal(PdfSecurityService.InvalidPasswordErrorCode, errorCode);
            Assert.False(string.IsNullOrWhiteSpace(errorMessage));
        }
        finally
        {
            securityService.ClearAllSessions();
        }
    }

    [Fact]
    public void PdfSecurityService_AcceptsCorrectPasswordAndCreatesUnlockedSession()
    {
        var securityService = new PdfSecurityService();
        var sourcePath = GetFixturePath("sample-encrypted.pdf");

        try
        {
            var success = securityService.TryUnlock(
                sourcePath,
                "secret-pass",
                out var result,
                out var errorCode,
                out var errorMessage);

            Assert.True(success, errorMessage);
            Assert.Null(errorCode);
            Assert.True(result.DocumentInfo.PageCount > 0);

            var resolvedPath = securityService.ResolveAccessiblePath(sourcePath);
            Assert.NotEqual(sourcePath, resolvedPath);
            Assert.True(File.Exists(resolvedPath));

            var inspected = securityService.TryInspect(sourcePath, out var snapshot, out var inspectError);
            Assert.True(inspected, inspectError);
            Assert.True(snapshot.IsEncrypted);
            Assert.True(snapshot.IsUnlocked);
            Assert.True(snapshot.DocumentInfo.PageCount > 0);
        }
        finally
        {
            securityService.ClearAllSessions();
        }
    }

    [Fact]
    public async Task ImageConversionService_SkipsLockedPdfDuringMixedBatchConversion()
    {
        var importService = new PdfImportService();
        var conversionService = new ImageConversionService();
        var outputDirectory = Path.Combine(Path.GetTempPath(), "ImvixProTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputDirectory);

        ImageItemViewModel? plainItem = null;
        ImageItemViewModel? lockedItem = null;

        try
        {
            Assert.True(importService.TryCreate(GetFixturePath("sample-plain.pdf"), out plainItem, out var plainError, generateThumbnail: false), plainError);
            Assert.True(importService.TryCreate(GetFixturePath("sample-encrypted.pdf"), out lockedItem, out var lockedError, generateThumbnail: false), lockedError);

            var options = new ConversionOptions
            {
                OutputFormat = OutputImageFormat.Png,
                OutputDirectoryRule = OutputDirectoryRule.SpecificFolder,
                OutputDirectory = outputDirectory,
                AllowOverwrite = true,
                LanguageCode = "en-US",
                MaxDegreeOfParallelism = 1
            };

            var summary = await conversionService.ConvertAsync([plainItem!, lockedItem!], options, progress: null);

            Assert.Equal(2, summary.TotalCount);
            Assert.Equal(2, summary.ProcessedCount);
            Assert.Equal(1, summary.SuccessCount);
            Assert.Single(summary.Failures);
            Assert.Equal("sample-encrypted.pdf", summary.Failures[0].FileName);
            Assert.Equal("PDF is locked and was skipped", summary.Failures[0].Reason);
            Assert.True(Directory.EnumerateFiles(outputDirectory, "*.png", SearchOption.AllDirectories).Any());
        }
        finally
        {
            plainItem?.Dispose();
            lockedItem?.Dispose();

            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static string GetFixturePath(string fileName)
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Fixtures", fileName));
    }
}
