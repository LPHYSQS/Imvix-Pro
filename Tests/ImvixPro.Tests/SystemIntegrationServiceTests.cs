using ImvixPro.Services;

namespace ImvixPro.Tests;

public sealed class SystemIntegrationServiceTests
{
    [Fact]
    public void BuildQuotedOpenCommand_AlwaysQuotesExecutableAndSelectedFilePlaceholder()
    {
        var command = SystemIntegrationService.BuildQuotedOpenCommand(@"C:\Apps\Imvix Pro\Imvix Pro.exe");

        Assert.Equal("\"C:\\Apps\\Imvix Pro\\Imvix Pro.exe\" \"%1\"", command);
    }

    [Fact]
    public void BuildWindowsFileContextMenuRegistrationPlan_UsesOnlySupportedExtensions()
    {
        var plan = SystemIntegrationService.BuildWindowsFileContextMenuRegistrationPlan(
            @"C:\Apps\Imvix Pro\Imvix Pro.exe",
            "Open with Imvix Pro");

        Assert.Equal("\"C:\\Apps\\Imvix Pro\\Imvix Pro.exe\" \"%1\"", plan.CommandText);
        Assert.Equal(@"C:\Apps\Imvix Pro\Imvix Pro.exe,0", plan.IconPath);
        Assert.Equal(SystemIntegrationService.WindowsContextMenuSupportedExtensions.Count, plan.ExtensionPlans.Count);
        Assert.Contains(plan.ExtensionPlans, extensionPlan => extensionPlan.Extension == ".png");
        Assert.Contains(plan.ExtensionPlans, extensionPlan => extensionPlan.Extension == ".tiff");
        Assert.DoesNotContain(plan.ExtensionPlans, extensionPlan => extensionPlan.Extension == ".tif");
        Assert.All(plan.ExtensionPlans, extensionPlan =>
        {
            Assert.True(
                extensionPlan.ShellKeyPath.StartsWith(@"Software\Classes\SystemFileAssociations\", System.StringComparison.OrdinalIgnoreCase),
                $"Unexpected shell key path: {extensionPlan.ShellKeyPath}");
            Assert.True(
                extensionPlan.CommandKeyPath.EndsWith(@"\command", System.StringComparison.OrdinalIgnoreCase),
                $"Unexpected command key path: {extensionPlan.CommandKeyPath}");
            Assert.True(
                extensionPlan.LegacyShellKeyPath.StartsWith(@"Software\Classes\.", System.StringComparison.OrdinalIgnoreCase),
                $"Unexpected legacy shell key path: {extensionPlan.LegacyShellKeyPath}");
        });
    }
}
