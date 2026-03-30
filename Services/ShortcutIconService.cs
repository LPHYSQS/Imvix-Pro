using SkiaSharp;
using Svg.Skia;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ImvixPro.Services
{
    public static class ShortcutIconService
    {
        public readonly record struct ShortcutMetadata(
            string TargetPath,
            string Arguments,
            string WorkingDirectory,
            string IconPath,
            int IconIndex,
            bool TargetExists);

        public static bool IsShortcutIconSource(string filePath)
        {
            return Path.GetExtension(filePath).Equals(".lnk", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryGetShortcutMetadata(string filePath, out ShortcutMetadata metadata)
        {
            metadata = default;

            if (!OperatingSystem.IsWindows() || !IsShortcutIconSource(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            return TryResolveShortcut(filePath, out metadata);
        }

        public static SKBitmap? TryExtractPrimaryIconBitmap(string filePath)
        {
            if (!OperatingSystem.IsWindows() || !IsShortcutIconSource(filePath) || !File.Exists(filePath))
            {
                return null;
            }

            try
            {
                if (!TryResolveShortcut(filePath, out var metadata))
                {
                    return null;
                }

                var bitmap = TryExtractFromIconSource(metadata.IconPath, metadata.IconIndex);
                if (bitmap is not null)
                {
                    return bitmap;
                }

                if (!string.IsNullOrWhiteSpace(metadata.TargetPath) &&
                    !PathsEqual(metadata.TargetPath, metadata.IconPath))
                {
                    return TryExtractFromIconSource(metadata.TargetPath, iconIndex: 0);
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        [SupportedOSPlatform("windows")]
        private static bool TryResolveShortcut(string shortcutPath, out ShortcutMetadata metadata)
        {
            metadata = default;

            object? shell = null;
            object? shortcut = null;

            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType is null)
                {
                    return false;
                }

                shell = Activator.CreateInstance(shellType);
                if (shell is null)
                {
                    return false;
                }

                dynamic shellObject = shell;
                shortcut = shellObject.CreateShortcut(shortcutPath);
                if (shortcut is null)
                {
                    return false;
                }

                dynamic shortcutObject = shortcut;
                string iconLocation = Convert.ToString((object?)shortcutObject.IconLocation) ?? string.Empty;
                var targetPath = NormalizeCandidatePath(Convert.ToString((object?)shortcutObject.TargetPath), shortcutPath);
                var arguments = Convert.ToString((object?)shortcutObject.Arguments) ?? string.Empty;
                var workingDirectory = NormalizeCandidatePath(Convert.ToString((object?)shortcutObject.WorkingDirectory), shortcutPath);

                if (TryParseIconLocation(iconLocation, shortcutPath, out var iconPath, out var iconIndex))
                {
                    metadata = CreateShortcutMetadata(targetPath, arguments, workingDirectory, iconPath, iconIndex);
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(targetPath))
                {
                    metadata = CreateShortcutMetadata(targetPath, arguments, workingDirectory, targetPath, 0);
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
            finally
            {
                TryReleaseComObject(shortcut);
                TryReleaseComObject(shell);
            }
        }

        private static ShortcutMetadata CreateShortcutMetadata(
            string targetPath,
            string arguments,
            string workingDirectory,
            string iconPath,
            int iconIndex)
        {
            var targetExists = !string.IsNullOrWhiteSpace(targetPath) &&
                               (File.Exists(targetPath) || Directory.Exists(targetPath));

            return new ShortcutMetadata(
                targetPath,
                arguments.Trim(),
                workingDirectory,
                iconPath,
                iconIndex,
                targetExists);
        }

        private static bool TryParseIconLocation(string iconLocation, string shortcutPath, out string iconPath, out int iconIndex)
        {
            iconPath = string.Empty;
            iconIndex = 0;

            if (string.IsNullOrWhiteSpace(iconLocation))
            {
                return false;
            }

            var expanded = Environment.ExpandEnvironmentVariables(iconLocation.Trim());
            var commaIndex = expanded.LastIndexOf(',');
            var rawPath = expanded;

            if (commaIndex >= 0 && commaIndex < expanded.Length - 1)
            {
                var suffix = expanded[(commaIndex + 1)..].Trim();
                if (int.TryParse(suffix, out var parsedIndex))
                {
                    iconIndex = parsedIndex;
                    rawPath = expanded[..commaIndex];
                }
            }

            iconPath = NormalizeCandidatePath(rawPath, shortcutPath);
            return !string.IsNullOrWhiteSpace(iconPath);
        }

        private static SKBitmap? TryExtractFromIconSource(string? sourcePath, int iconIndex)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                return null;
            }

            if (ExecutableIconService.IsPortableExecutableIconSource(sourcePath))
            {
                var bitmap = ExecutableIconService.TryExtractPrimaryIconBitmap(sourcePath, iconIndex);
                return bitmap ?? (iconIndex == 0 ? null : ExecutableIconService.TryExtractPrimaryIconBitmap(sourcePath, 0));
            }

            var extension = Path.GetExtension(sourcePath);
            if (extension.Equals(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return TryDecodeSvg(sourcePath);
            }

            return SKBitmap.Decode(sourcePath);
        }

        private static SKBitmap? TryDecodeSvg(string filePath)
        {
            try
            {
                var svg = new SKSvg();
                var picture = svg.Load(filePath);
                if (picture is null)
                {
                    return null;
                }

                var bounds = picture.CullRect;
                var width = Math.Max(1, (int)Math.Ceiling(bounds.Width));
                var height = Math.Max(1, (int)Math.Ceiling(bounds.Height));
                var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
                using var canvas = new SKCanvas(bitmap);
                canvas.Clear(SKColors.Transparent);
                var matrix = SKMatrix.CreateTranslation(-bounds.Left, -bounds.Top);
                canvas.DrawPicture(picture, ref matrix);
                canvas.Flush();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private static string NormalizeCandidatePath(string? candidate, string shortcutPath)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return string.Empty;
            }

            var normalized = Environment.ExpandEnvironmentVariables(candidate.Trim().Trim('"'));
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            try
            {
                if (!Path.IsPathRooted(normalized))
                {
                    var baseDirectory = Path.GetDirectoryName(shortcutPath) ?? string.Empty;
                    normalized = Path.Combine(baseDirectory, normalized);
                }

                return Path.GetFullPath(normalized);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool PathsEqual(string? firstPath, string? secondPath)
        {
            if (string.IsNullOrWhiteSpace(firstPath) || string.IsNullOrWhiteSpace(secondPath))
            {
                return false;
            }

            return string.Equals(firstPath, secondPath, StringComparison.OrdinalIgnoreCase);
        }

        [SupportedOSPlatform("windows")]
        private static void TryReleaseComObject(object? instance)
        {
            if (instance is not null && Marshal.IsComObject(instance))
            {
                try
                {
                    Marshal.FinalReleaseComObject(instance);
                }
                catch
                {
                    // Ignore COM release failures for shortcut inspection.
                }
            }
        }
    }
}
