using Leigod_Auto_Pause.Installer;

namespace Leigod_Auto_Pause.Tests.Installer;

public class LeigodInstallLocatorTests
{
    [Fact]
    public void Locate_PrefersRegistryInstallLocation_WhenItContainsRequiredFiles()
    {
        var registry = new FakeRegistryReader([
            @"C:\Program Files\Leigod"
        ]);

        var locator = new LeigodInstallLocator(
            registry,
            path => path switch
            {
                @"C:\Program Files\Leigod\resources\app.asar" => true,
                @"C:\Program Files\Leigod\leigod_launcher.exe" => true,
                _ => false
            },
            processDirectories: () => [],
            commonDirectories: () => []);

        var result = locator.LocateBestCandidate();

        Assert.NotNull(result);
        Assert.Equal(@"C:\Program Files\Leigod", result!.DirectoryPath);
    }

    private sealed class FakeRegistryReader(IEnumerable<string> installLocations) : IRegistryReader
    {
        public IEnumerable<string> ReadInstallLocations() => installLocations;
    }
}
