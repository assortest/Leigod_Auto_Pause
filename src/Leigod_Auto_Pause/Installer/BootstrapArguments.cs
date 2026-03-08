namespace Leigod_Auto_Pause.Installer;

public sealed record BootstrapArguments(bool IsInstalledLaunch, string? InstallDirectory)
{
    public const string InstalledLaunchFlag = "--installed-launch";
    public const string PerformInstallFlag = "--perform-install";

    public static BootstrapArguments Parse(string[] args)
    {
        var isInstalledLaunch = args.Any(x => string.Equals(x, InstalledLaunchFlag, StringComparison.OrdinalIgnoreCase));
        string? installDirectory = null;

        for (var i = 0; i < args.Length; i++)
        {
            if (!string.Equals(args[i], PerformInstallFlag, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 < args.Length)
            {
                installDirectory = args[i + 1];
            }

            break;
        }

        return new BootstrapArguments(isInstalledLaunch, installDirectory);
    }
}
