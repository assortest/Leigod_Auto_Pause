namespace Leigod_Auto_Pause.Installer;

public interface IShortcutShell
{
    IShortcutFile CreateShortcut(string shortcutPath);
}

public interface IShortcutFile
{
    string TargetPath { get; set; }
    string WorkingDirectory { get; set; }
    string Arguments { get; set; }
    string IconLocation { get; set; }
    string Description { get; set; }
    void Save();
}

public sealed class DesktopShortcutService : IShortcutService
{
    private readonly Func<IShortcutShell> _shellFactory;

    public DesktopShortcutService(Func<IShortcutShell>? shellFactory = null)
    {
        _shellFactory = shellFactory ?? CreateShell;
    }

    public void CreateOrUpdate(string shortcutPath, string targetPath, string workingDirectory, string arguments, string iconLocation)
    {
        var shell = _shellFactory();
        var shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = workingDirectory;
        shortcut.Arguments = arguments;
        shortcut.IconLocation = iconLocation;
        shortcut.Description = "启动雷神自动暂停插件";
        shortcut.Save();
    }

    private static IShortcutShell CreateShell()
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ??
                        throw new InvalidOperationException("无法创建快捷方式服务。");

        var shellInstance = Activator.CreateInstance(shellType) ??
                            throw new InvalidOperationException("无法创建快捷方式服务。");

        return new ComShortcutShell(shellInstance);
    }

    private sealed class ComShortcutShell(object shell) : IShortcutShell
    {
        private readonly dynamic _shell = shell;

        public IShortcutFile CreateShortcut(string shortcutPath)
        {
            return new ComShortcutFile(_shell.CreateShortcut(shortcutPath));
        }
    }

    private sealed class ComShortcutFile(object shortcut) : IShortcutFile
    {
        private readonly dynamic _shortcut = shortcut;

        public string TargetPath
        {
            get => _shortcut.TargetPath;
            set => _shortcut.TargetPath = value;
        }

        public string WorkingDirectory
        {
            get => _shortcut.WorkingDirectory;
            set => _shortcut.WorkingDirectory = value;
        }

        public string Arguments
        {
            get => _shortcut.Arguments;
            set => _shortcut.Arguments = value;
        }

        public string IconLocation
        {
            get => _shortcut.IconLocation;
            set => _shortcut.IconLocation = value;
        }

        public string Description
        {
            get => _shortcut.Description;
            set => _shortcut.Description = value;
        }

        public void Save()
        {
            _shortcut.Save();
        }
    }
}
