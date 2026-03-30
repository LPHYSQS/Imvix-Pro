using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;

namespace ImvixPro.Services
{
    public readonly record struct WindowsContextMenuRegistrationResult(bool Succeeded, string? ErrorMessage = null);

    internal readonly record struct WindowsContextMenuExtensionPlan(
        string Extension,
        string ShellKeyPath,
        string CommandKeyPath,
        string LegacyShellKeyPath,
        string LegacyCommandKeyPath);

    internal readonly record struct WindowsContextMenuRegistrationPlan(
        string MenuText,
        string ExecutablePath,
        string IconPath,
        string CommandText,
        string CommandStoreKeyPath,
        string CommandStoreCommandKeyPath,
        IReadOnlyList<WindowsContextMenuExtensionPlan> ExtensionPlans);

    public sealed class SystemIntegrationService
    {
        private const string ShortcutExtension = ".lnk";
        private const string ContextMenuVerbKey = "ImvixPro";
        private const string ClassesRoot = @"Software\Classes";
        private const string SystemFileAssociationsRoot = @"Software\Classes\SystemFileAssociations";
        private const string CommandStoreShellRoot = @"Software\Microsoft\Windows\CurrentVersion\Explorer\CommandStore\shell";

        internal static IReadOnlyList<string> WindowsContextMenuSupportedExtensions { get; } =
            new ReadOnlyCollection<string>(
            [
                ".png",
                ".jpg",
                ".jpeg",
                ".webp",
                ".bmp",
                ".gif",
                ".tiff",
                ".ico",
                ".svg",
                ".pdf",
                ".psd",
                ".exe",
                ".lnk"
            ]);

        public bool IsWindowsFileContextMenuSupported => OperatingSystem.IsWindows();

        public bool SetRunOnStartup(bool enabled)
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            var shortcutPath = GetStartupShortcutPath();
            if (string.IsNullOrWhiteSpace(shortcutPath))
            {
                return false;
            }

            if (!enabled)
            {
                TryDelete(shortcutPath);
                return true;
            }

            return TryCreateShortcut(shortcutPath);
        }

        public WindowsContextMenuRegistrationResult SetWindowsFileContextMenu(bool enabled, string menuText)
        {
            if (!OperatingSystem.IsWindows())
            {
                return new WindowsContextMenuRegistrationResult(false, "Windows Explorer context menus are only available on Windows.");
            }

            return enabled
                ? RegisterWindowsFileContextMenu(menuText)
                : RemoveWindowsFileContextMenu();
        }

        public bool CreateDesktopShortcut()
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            var shortcutPath = GetDesktopShortcutPath();
            if (string.IsNullOrWhiteSpace(shortcutPath))
            {
                return false;
            }

            return TryCreateShortcut(shortcutPath);
        }

        public bool DesktopShortcutExists()
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            var shortcutPath = GetDesktopShortcutPath();
            if (string.IsNullOrWhiteSpace(shortcutPath))
            {
                return false;
            }

            return File.Exists(shortcutPath);
        }

        internal static WindowsContextMenuRegistrationPlan BuildWindowsFileContextMenuRegistrationPlan(string executablePath, string menuText)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);

            var normalizedExecutablePath = Path.GetFullPath(executablePath);
            var normalizedMenuText = string.IsNullOrWhiteSpace(menuText)
                ? $"Open with {AppIdentity.DisplayName}"
                : menuText.Trim();
            var commandStoreKeyPath = BuildCommandStoreKeyPath();
            var extensionPlans = WindowsContextMenuSupportedExtensions
                .Select(extension => new WindowsContextMenuExtensionPlan(
                    extension,
                    BuildSystemFileAssociationsShellKeyPath(extension),
                    BuildSystemFileAssociationsCommandKeyPath(extension),
                    BuildLegacyExtensionShellKeyPath(extension),
                    BuildLegacyExtensionCommandKeyPath(extension)))
                .ToArray();

            return new WindowsContextMenuRegistrationPlan(
                normalizedMenuText,
                normalizedExecutablePath,
                BuildIconReference(normalizedExecutablePath),
                BuildQuotedOpenCommand(normalizedExecutablePath),
                commandStoreKeyPath,
                $@"{commandStoreKeyPath}\command",
                extensionPlans);
        }

        internal static string BuildQuotedOpenCommand(string executablePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
            return $"\"{Path.GetFullPath(executablePath)}\" \"%1\"";
        }

        internal static string BuildIconReference(string executablePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
            return $"{Path.GetFullPath(executablePath)},0";
        }

        [SupportedOSPlatform("windows")]
        private static WindowsContextMenuRegistrationResult RegisterWindowsFileContextMenu(string menuText)
        {
            if (!TryResolveCurrentExecutablePath(out var executablePath))
            {
                return new WindowsContextMenuRegistrationResult(false, "Could not resolve the current executable path.");
            }

            var plan = BuildWindowsFileContextMenuRegistrationPlan(executablePath, menuText);

            try
            {
                WriteCommandStoreRegistration(plan);
                foreach (var extensionPlan in plan.ExtensionPlans)
                {
                    WriteExtensionRegistration(plan, extensionPlan);
                }

                return ValidateWindowsContextMenuRegistration(plan)
                    ? new WindowsContextMenuRegistrationResult(true)
                    : new WindowsContextMenuRegistrationResult(false, "Registry verification failed after writing the context menu keys.");
            }
            catch (Exception ex)
            {
                AppServices.Logger.LogWarning(nameof(SystemIntegrationService), "Failed to register the Windows Explorer context menu.", ex);
                return new WindowsContextMenuRegistrationResult(false, ex.Message);
            }
        }

        [SupportedOSPlatform("windows")]
        private static WindowsContextMenuRegistrationResult RemoveWindowsFileContextMenu()
        {
            try
            {
                using var currentUser = Registry.CurrentUser;

                foreach (var extension in WindowsContextMenuSupportedExtensions)
                {
                    currentUser.DeleteSubKeyTree(BuildSystemFileAssociationsShellKeyPath(extension), throwOnMissingSubKey: false);
                    currentUser.DeleteSubKeyTree(BuildLegacyExtensionShellKeyPath(extension), throwOnMissingSubKey: false);
                }

                currentUser.DeleteSubKeyTree(BuildCommandStoreKeyPath(), throwOnMissingSubKey: false);

                return ValidateWindowsContextMenuRemoval()
                    ? new WindowsContextMenuRegistrationResult(true)
                    : new WindowsContextMenuRegistrationResult(false, "Registry verification failed after removing the context menu keys.");
            }
            catch (Exception ex)
            {
                AppServices.Logger.LogWarning(nameof(SystemIntegrationService), "Failed to remove the Windows Explorer context menu.", ex);
                return new WindowsContextMenuRegistrationResult(false, ex.Message);
            }
        }

        [SupportedOSPlatform("windows")]
        private static void WriteCommandStoreRegistration(WindowsContextMenuRegistrationPlan plan)
        {
            using var key = Registry.CurrentUser.CreateSubKey(plan.CommandStoreKeyPath, writable: true);
            if (key is null)
            {
                throw new InvalidOperationException("Could not create the CommandStore registry key.");
            }

            key.SetValue(null, plan.MenuText, RegistryValueKind.String);
            key.SetValue("MUIVerb", plan.MenuText, RegistryValueKind.String);
            key.SetValue("Icon", plan.IconPath, RegistryValueKind.String);
            key.SetValue("CommandStateSync", string.Empty, RegistryValueKind.String);

            using var commandKey = key.CreateSubKey("command", writable: true);
            if (commandKey is null)
            {
                throw new InvalidOperationException("Could not create the CommandStore command registry key.");
            }

            commandKey.SetValue(null, plan.CommandText, RegistryValueKind.String);
        }

        [SupportedOSPlatform("windows")]
        private static void WriteExtensionRegistration(WindowsContextMenuRegistrationPlan plan, WindowsContextMenuExtensionPlan extensionPlan)
        {
            using var key = Registry.CurrentUser.CreateSubKey(extensionPlan.ShellKeyPath, writable: true);
            if (key is null)
            {
                throw new InvalidOperationException($"Could not create the shell registry key for '{extensionPlan.Extension}'.");
            }

            key.SetValue(null, plan.MenuText, RegistryValueKind.String);
            key.SetValue("MUIVerb", plan.MenuText, RegistryValueKind.String);
            key.SetValue("Icon", plan.IconPath, RegistryValueKind.String);
            key.SetValue("CommandStateSync", string.Empty, RegistryValueKind.String);

            using var commandKey = key.CreateSubKey("command", writable: true);
            if (commandKey is null)
            {
                throw new InvalidOperationException($"Could not create the command registry key for '{extensionPlan.Extension}'.");
            }

            commandKey.SetValue(null, plan.CommandText, RegistryValueKind.String);

            // Clean up the earlier extension-root registration so old builds do not leave conflicting keys behind.
            Registry.CurrentUser.DeleteSubKeyTree(extensionPlan.LegacyShellKeyPath, throwOnMissingSubKey: false);
        }

        [SupportedOSPlatform("windows")]
        private static bool ValidateWindowsContextMenuRegistration(WindowsContextMenuRegistrationPlan plan)
        {
            using var currentUser = Registry.CurrentUser;

            if (!ValidateShellKey(currentUser, plan.CommandStoreKeyPath, plan.MenuText, plan.IconPath))
            {
                return false;
            }

            if (!ValidateCommandKey(currentUser, plan.CommandStoreCommandKeyPath, plan.CommandText))
            {
                return false;
            }

            foreach (var extensionPlan in plan.ExtensionPlans)
            {
                if (!ValidateShellKey(currentUser, extensionPlan.ShellKeyPath, plan.MenuText, plan.IconPath))
                {
                    return false;
                }

                if (!ValidateCommandKey(currentUser, extensionPlan.CommandKeyPath, plan.CommandText))
                {
                    return false;
                }
            }

            return true;
        }

        [SupportedOSPlatform("windows")]
        private static bool ValidateWindowsContextMenuRemoval()
        {
            using var currentUser = Registry.CurrentUser;
            if (currentUser.OpenSubKey(BuildCommandStoreKeyPath()) is not null)
            {
                return false;
            }

            return WindowsContextMenuSupportedExtensions.All(extension =>
                currentUser.OpenSubKey(BuildSystemFileAssociationsShellKeyPath(extension)) is null &&
                currentUser.OpenSubKey(BuildLegacyExtensionShellKeyPath(extension)) is null);
        }

        [SupportedOSPlatform("windows")]
        private static bool ValidateShellKey(RegistryKey currentUser, string keyPath, string menuText, string iconPath)
        {
            using var key = currentUser.OpenSubKey(keyPath, writable: false);
            if (key is null)
            {
                return false;
            }

            return string.Equals(ReadRegistryString(key, null), menuText, StringComparison.Ordinal) &&
                   string.Equals(ReadRegistryString(key, "MUIVerb"), menuText, StringComparison.Ordinal) &&
                   string.Equals(ReadRegistryString(key, "Icon"), iconPath, StringComparison.Ordinal);
        }

        [SupportedOSPlatform("windows")]
        private static bool ValidateCommandKey(RegistryKey currentUser, string keyPath, string commandText)
        {
            using var key = currentUser.OpenSubKey(keyPath, writable: false);
            if (key is null)
            {
                return false;
            }

            return string.Equals(ReadRegistryString(key, null), commandText, StringComparison.Ordinal);
        }

        [SupportedOSPlatform("windows")]
        private static string ReadRegistryString(RegistryKey key, string? valueName)
        {
            return Convert.ToString(key.GetValue(valueName), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string GetStartupShortcutPath()
        {
            var startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            if (string.IsNullOrWhiteSpace(startup))
            {
                return string.Empty;
            }

            return Path.Combine(startup, $"{AppIdentity.ShortcutName}{ShortcutExtension}");
        }

        private static string GetDesktopShortcutPath()
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrWhiteSpace(desktop))
            {
                return string.Empty;
            }

            return Path.Combine(desktop, $"{AppIdentity.ShortcutName}{ShortcutExtension}");
        }

        private static string BuildCommandStoreKeyPath()
        {
            return $@"{CommandStoreShellRoot}\{ContextMenuVerbKey}";
        }

        private static string BuildSystemFileAssociationsShellKeyPath(string extension)
        {
            return $@"{SystemFileAssociationsRoot}\{NormalizeExtension(extension)}\shell\{ContextMenuVerbKey}";
        }

        private static string BuildSystemFileAssociationsCommandKeyPath(string extension)
        {
            return $@"{BuildSystemFileAssociationsShellKeyPath(extension)}\command";
        }

        private static string BuildLegacyExtensionShellKeyPath(string extension)
        {
            return $@"{ClassesRoot}\{NormalizeExtension(extension)}\shell\{ContextMenuVerbKey}";
        }

        private static string BuildLegacyExtensionCommandKeyPath(string extension)
        {
            return $@"{BuildLegacyExtensionShellKeyPath(extension)}\command";
        }

        private static string NormalizeExtension(string extension)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(extension);
            return extension.StartsWith(".", StringComparison.Ordinal) ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                AppServices.Logger.LogWarning(nameof(SystemIntegrationService), $"Failed to delete shortcut '{path}'.", ex);
            }
        }

        [SupportedOSPlatform("windows")]
        private static bool TryCreateShortcut(string shortcutPath)
        {
            if (!TryResolveAppLaunch(out var targetPath, out var arguments, out var workingDirectory, out var iconPath))
            {
                return false;
            }

            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType is null)
                {
                    return false;
                }

                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;

                if (!string.IsNullOrWhiteSpace(arguments))
                {
                    shortcut.Arguments = arguments;
                }

                if (!string.IsNullOrWhiteSpace(workingDirectory))
                {
                    shortcut.WorkingDirectory = workingDirectory;
                }

                if (!string.IsNullOrWhiteSpace(iconPath))
                {
                    shortcut.IconLocation = iconPath;
                }

                shortcut.Save();
                return true;
            }
            catch (Exception ex)
            {
                AppServices.Logger.LogWarning(nameof(SystemIntegrationService), $"Failed to create shortcut '{shortcutPath}'.", ex);
                return false;
            }
        }

        private static bool TryResolveAppLaunch(
            out string targetPath,
            out string arguments,
            out string workingDirectory,
            out string iconPath)
        {
            targetPath = string.Empty;
            arguments = string.Empty;
            workingDirectory = string.Empty;
            iconPath = string.Empty;

            if (TryResolveCurrentExecutablePath(out var executablePath))
            {
                targetPath = executablePath;
                workingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty;
                iconPath = executablePath;
                return true;
            }

            var processPath = Environment.ProcessPath;
            var entryLocation = Assembly.GetEntryAssembly()?.Location;

            if (!string.IsNullOrWhiteSpace(entryLocation) && File.Exists(entryLocation))
            {
                if (entryLocation.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    var dotnetPath = ResolveDotNetHostPath(processPath);
                    if (!string.IsNullOrWhiteSpace(dotnetPath))
                    {
                        targetPath = dotnetPath;
                        arguments = $"\"{entryLocation}\"";
                        workingDirectory = Path.GetDirectoryName(entryLocation) ?? string.Empty;
                        iconPath = targetPath;
                        return true;
                    }
                }
            }

            return false;
        }

        internal static bool TryResolveCurrentExecutablePath(out string executablePath)
        {
            if (TryResolveExecutableCandidate(Environment.ProcessPath, out executablePath))
            {
                return true;
            }

            var entryLocation = Assembly.GetEntryAssembly()?.Location;
            if (TryResolveExecutableCandidate(entryLocation, out executablePath))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(entryLocation) &&
                entryLocation.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                var exeCandidate = Path.ChangeExtension(entryLocation, ".exe");
                if (TryResolveExecutableCandidate(exeCandidate, out executablePath))
                {
                    return true;
                }
            }

            return TryResolveExecutableCandidate(ResolveProcessModulePath(), out executablePath);
        }

        private static bool TryResolveExecutableCandidate(string? path, out string executablePath)
        {
            executablePath = string.Empty;

            if (string.IsNullOrWhiteSpace(path) ||
                !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                IsDotNetHost(path) ||
                !File.Exists(path))
            {
                return false;
            }

            executablePath = Path.GetFullPath(path);
            return true;
        }

        private static bool IsDotNetHost(string path)
        {
            var name = Path.GetFileName(path);
            return name.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveDotNetHostPath(string? fallback)
        {
            if (!string.IsNullOrWhiteSpace(fallback) && File.Exists(fallback) && IsDotNetHost(fallback))
            {
                return fallback;
            }

            try
            {
                var module = Process.GetCurrentProcess().MainModule;
                if (module is not null && !string.IsNullOrWhiteSpace(module.FileName) && IsDotNetHost(module.FileName))
                {
                    return module.FileName;
                }
            }
            catch (Exception ex)
            {
                AppServices.Logger.LogDebug(nameof(SystemIntegrationService), "Failed to resolve the current dotnet host path from the process module.", ex);
            }

            return string.Empty;
        }

        private static string ResolveProcessModulePath()
        {
            try
            {
                return Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            }
            catch (Exception ex)
            {
                AppServices.Logger.LogDebug(nameof(SystemIntegrationService), "Failed to resolve the current process module path.", ex);
                return string.Empty;
            }
        }
    }
}
