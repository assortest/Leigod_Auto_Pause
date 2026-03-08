namespace Leigod_Auto_Pause.Installer;

public enum BootstrapAction
{
    RunInPlace,
    InstallAndRelaunch,
    Abort
}

public sealed record LaunchBootstrapResult(
    BootstrapAction Action,
    string? TargetDirectory,
    string? InstalledExecutablePath,
    string? ErrorMessage);
