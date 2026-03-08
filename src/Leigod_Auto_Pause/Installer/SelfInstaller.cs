namespace Leigod_Auto_Pause.Installer;

public sealed class SelfInstaller
{
    public const string InstalledFileName = "Leigod_Auto_Pause.exe";
    public const string ShortcutFileName = "雷神自动暂停.lnk";

    private readonly Action<string, string, bool> _copyFile;
    private readonly Action<string> _ensureDirectory;
    private readonly IShortcutService _shortcutService;

    public SelfInstaller(
        Action<string, string, bool>? copyFile = null,
        Action<string>? ensureDirectory = null,
        IShortcutService? shortcutService = null)
    {
        _copyFile = copyFile ?? File.Copy;
        _ensureDirectory = ensureDirectory ?? (path => Directory.CreateDirectory(path));
        _shortcutService = shortcutService ?? new DesktopShortcutService();
    }

    public string Install(string sourceExePath, string targetDirectory, string desktopDirectory)
    {
        _ensureDirectory(targetDirectory);

        var installedExePath = Path.Combine(targetDirectory, InstalledFileName);
        if (!string.Equals(
                Path.GetFullPath(sourceExePath),
                Path.GetFullPath(installedExePath),
                StringComparison.OrdinalIgnoreCase))
        {
            _copyFile(sourceExePath, installedExePath, true);
        }

        var shortcutPath = Path.Combine(desktopDirectory, ShortcutFileName);
        _shortcutService.CreateOrUpdate(shortcutPath, installedExePath, targetDirectory, string.Empty, installedExePath);
        return installedExePath;
    }
}
