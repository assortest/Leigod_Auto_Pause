using Leigod_Auto_Pause.Installer;

namespace Leigod_Auto_Pause.Tests.Installer;

public class BootstrapArgumentsTests
{
    [Fact]
    public void Parse_WhenInstalledLaunchFlagPresent_ReturnsInstalledLaunchTrue()
    {
        var parsed = BootstrapArguments.Parse([BootstrapArguments.InstalledLaunchFlag]);
        Assert.True(parsed.IsInstalledLaunch);
    }

    [Fact]
    public void Parse_WhenPerformInstallFlagPresent_ReturnsInstallDirectory()
    {
        var parsed = BootstrapArguments.Parse([BootstrapArguments.PerformInstallFlag, @"D:\Leigod"]);
        Assert.Equal(@"D:\Leigod", parsed.InstallDirectory);
    }
}
