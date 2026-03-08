using Leigod_Auto_Pause.Installer;

namespace Leigod_Auto_Pause.Tests.Installer;

public class LaunchBootstrapTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "LeigodAutoPauseTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Decide_WhenCurrentDirectoryAlreadyContainsLeigodFiles_ReturnsAlreadyInstalled()
    {
        var installDir = Path.Combine(_tempRoot, "Leigod");
        Directory.CreateDirectory(Path.Combine(installDir, "resources"));
        File.WriteAllText(Path.Combine(installDir, "resources", "app.asar"), "asar");
        File.WriteAllText(Path.Combine(installDir, "leigod_launcher.exe"), "launcher");

        var bootstrap = new LaunchBootstrap();
        var result = bootstrap.Decide(
            executablePath: Path.Combine(installDir, "Leigod_Auto_Pause.exe"),
            currentDirectory: installDir,
            candidates: []);

        Assert.Equal(BootstrapAction.RunInPlace, result.Action);
        Assert.Equal(installDir, result.TargetDirectory);
    }

    [Fact]
    public void Decide_WhenOutsideLeigodDirectory_ReturnsInstallAndRelaunch()
    {
        var bootstrap = new LaunchBootstrap();
        var result = bootstrap.Decide(
            executablePath: @"C:\Users\me\Downloads\Leigod_Auto_Pause.exe",
            currentDirectory: @"C:\Users\me\Downloads",
            candidates:
            [
                new LeigodInstallCandidate(@"D:\Leigod", 100)
            ]);

        Assert.Equal(BootstrapAction.InstallAndRelaunch, result.Action);
        Assert.Equal(@"D:\Leigod\Leigod_Auto_Pause.exe", result.InstalledExecutablePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, true);
        }
    }
}
