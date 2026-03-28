using Docnet.Core;
using Docnet.Core.Models;
using ImvixPro.Models;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ImvixPro.Services.PdfModule
{
    public sealed class PdfSecurityService
    {
        public const string InvalidPasswordErrorCode = "PdfUnlockInvalidPassword";
        public const string UnlockFailedErrorCode = "PdfUnlockFailed";

        private const int PdfSampleBytes = 4 * 1024 * 1024;

        private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _sessionRoot = Path.Combine(
            Path.GetTempPath(),
            "ImvixPro",
            "PdfUnlock",
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));

        public bool TryInspect(string filePath, out PdfSecuritySnapshot snapshot, out string? error)
        {
            snapshot = default;
            error = null;

            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    error = "File not found.";
                    return false;
                }

                if (TryGetSession(filePath, out var session))
                {
                    snapshot = new PdfSecuritySnapshot(
                        IsEncrypted: true,
                        IsUnlocked: true,
                        DocumentInfo: session.DocumentInfo);
                    return true;
                }

                var sample = ReadPdfSampleText(filePath);
                if (TryReadDocumentInfoCore(filePath, password: null, out var info, out var readError))
                {
                    var isEncrypted = IsEncryptedSample(sample);
                    snapshot = new PdfSecuritySnapshot(
                        IsEncrypted: isEncrypted,
                        IsUnlocked: true,
                        DocumentInfo: info);
                    return true;
                }

                if (IsEncryptedSample(sample) || IsLikelyPasswordError(readError))
                {
                    snapshot = new PdfSecuritySnapshot(
                        IsEncrypted: true,
                        IsUnlocked: false,
                        DocumentInfo: default);
                    return true;
                }

                error = readError;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public bool TryUnlock(string filePath, string password, out PdfUnlockResult result, out string? errorCode, out string? errorMessage)
        {
            result = default;
            errorCode = null;
            errorMessage = null;

            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    errorCode = UnlockFailedErrorCode;
                    errorMessage = "File not found.";
                    return false;
                }

                if (!TryReadDocumentInfoCore(filePath, password, out var info, out var validationError))
                {
                    errorCode = IsLikelyPasswordError(validationError)
                        ? InvalidPasswordErrorCode
                        : UnlockFailedErrorCode;
                    errorMessage = validationError;
                    return false;
                }

                var unlockedBytes = DocLib.Instance.Unlock(filePath, password);
                if (unlockedBytes is null || unlockedBytes.Length == 0)
                {
                    errorCode = UnlockFailedErrorCode;
                    errorMessage = "Unable to unlock PDF document.";
                    return false;
                }

                Directory.CreateDirectory(_sessionRoot);
                var unlockedPath = Path.Combine(
                    _sessionRoot,
                    $"{Guid.NewGuid():N}.pdf");
                File.WriteAllBytes(unlockedPath, unlockedBytes);

                if (!TryReadDocumentInfoCore(unlockedPath, password: null, out var unlockedInfo, out var unlockedError))
                {
                    TryDeleteFile(unlockedPath);
                    errorCode = UnlockFailedErrorCode;
                    errorMessage = unlockedError;
                    return false;
                }

                StoreSession(filePath, unlockedPath, unlockedInfo, password);
                result = new PdfUnlockResult(unlockedInfo);
                return true;
            }
            catch (Exception ex)
            {
                errorCode = IsLikelyPasswordError(ex.Message)
                    ? InvalidPasswordErrorCode
                    : UnlockFailedErrorCode;
                errorMessage = ex.Message;
                return false;
            }
        }

        public string ResolveAccessiblePath(string filePath)
        {
            return TryGetSession(filePath, out var session)
                ? session.UnlockedFilePath
                : filePath;
        }

        public void ClearSession(string filePath)
        {
            if (!_sessions.TryRemove(filePath, out var session))
            {
                return;
            }

            TryDeleteFile(session.UnlockedFilePath);
        }

        public void ClearAllSessions()
        {
            foreach (var entry in _sessions.ToArray())
            {
                if (_sessions.TryRemove(entry.Key, out var session))
                {
                    TryDeleteFile(session.UnlockedFilePath);
                }
            }

            try
            {
                if (Directory.Exists(_sessionRoot) && !Directory.EnumerateFileSystemEntries(_sessionRoot).Any())
                {
                    Directory.Delete(_sessionRoot, recursive: false);
                }
            }
            catch
            {
                // Ignore temp cleanup failures during shutdown.
            }
        }

        private bool TryGetSession(string filePath, out SessionEntry session)
        {
            if (_sessions.TryGetValue(filePath, out session) &&
                File.Exists(session.UnlockedFilePath))
            {
                return true;
            }

            if (session != default)
            {
                _sessions.TryRemove(filePath, out _);
            }

            session = default;
            return false;
        }

        private void StoreSession(string filePath, string unlockedPath, PdfDocumentInfo documentInfo, string password)
        {
            var next = new SessionEntry(unlockedPath, documentInfo, password);
            if (_sessions.TryGetValue(filePath, out var existing))
            {
                if (!existing.UnlockedFilePath.Equals(unlockedPath, StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteFile(existing.UnlockedFilePath);
                }
            }

            _sessions[filePath] = next;
        }

        private static bool TryReadDocumentInfoCore(string filePath, string? password, out PdfDocumentInfo info, out string? error)
        {
            info = default;
            error = null;

            try
            {
                using var docReader = string.IsNullOrWhiteSpace(password)
                    ? DocLib.Instance.GetDocReader(filePath, new PageDimensions(1d))
                    : DocLib.Instance.GetDocReader(filePath, password, new PageDimensions(1d));

                if (docReader is null)
                {
                    error = "Unable to open PDF document.";
                    return false;
                }

                var pageCount = docReader.GetPageCount();
                if (pageCount <= 0)
                {
                    error = "PDF contains no readable pages.";
                    return false;
                }

                using var pageReader = docReader.GetPageReader(0);
                if (pageReader is null)
                {
                    error = "Unable to read the first PDF page.";
                    return false;
                }

                info = new PdfDocumentInfo(
                    pageCount,
                    Math.Max(1, pageReader.GetPageWidth()),
                    Math.Max(1, pageReader.GetPageHeight()));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool IsEncryptedSample(string? sample)
        {
            return !string.IsNullOrWhiteSpace(sample) &&
                   sample.Contains("/Encrypt", StringComparison.Ordinal);
        }

        private static bool IsLikelyPasswordError(string? error)
        {
            if (string.IsNullOrWhiteSpace(error))
            {
                return false;
            }

            return error.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                   error.Contains("encrypted", StringComparison.OrdinalIgnoreCase) ||
                   error.Contains("security", StringComparison.OrdinalIgnoreCase);
        }

        private static string? ReadPdfSampleText(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                using var stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                if (fileInfo.Length <= PdfSampleBytes * 2L)
                {
                    var allBytes = new byte[(int)fileInfo.Length];
                    stream.ReadExactly(allBytes);
                    return Encoding.Latin1.GetString(allBytes);
                }

                var head = new byte[PdfSampleBytes];
                stream.ReadExactly(head);

                stream.Seek(-PdfSampleBytes, SeekOrigin.End);
                var tail = new byte[PdfSampleBytes];
                stream.ReadExactly(tail);

                return Encoding.Latin1.GetString(head) + "\n" + Encoding.Latin1.GetString(tail);
            }
            catch
            {
                return null;
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Ignore temp cleanup failures.
            }
        }

        private readonly record struct SessionEntry(
            string UnlockedFilePath,
            PdfDocumentInfo DocumentInfo,
            string Password);
    }
}
