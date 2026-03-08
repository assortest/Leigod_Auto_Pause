namespace Leigod_Auto_Pause.Installer;

public sealed class LaunchBootstrap
{
    public LaunchBootstrapResult Decide(
        string executablePath,
        string currentDirectory,
        IReadOnlyList<LeigodInstallCandidate> candidates)
    {
        if (IsInstalledDirectory(currentDirectory))
        {
            return new LaunchBootstrapResult(BootstrapAction.RunInPlace, Normalize(currentDirectory), executablePath, null);
        }

        var bestCandidate = candidates
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (bestCandidate is null)
        {
            return new LaunchBootstrapResult(BootstrapAction.Abort, null, null, "未找到雷神安装目录。");
        }

        var targetDirectory = Normalize(bestCandidate.DirectoryPath);
        var installedExecutablePath = Path.Combine(targetDirectory, SelfInstaller.InstalledFileName);
        return new LaunchBootstrapResult(BootstrapAction.InstallAndRelaunch, targetDirectory, installedExecutablePath, null);
    }

    private static bool IsInstalledDirectory(string directoryPath)
    {
        return File.Exists(Path.Combine(directoryPath, "resources", "app.asar")) &&
               File.Exists(Path.Combine(directoryPath, "leigod_launcher.exe"));
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
