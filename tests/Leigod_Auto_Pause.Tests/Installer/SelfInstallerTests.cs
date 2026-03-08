using Leigod_Auto_Pause.Installer;

namespace Leigod_Auto_Pause.Tests.Installer;

public class SelfInstallerTests
{
    [Fact]
    public void Install_CopiesExeAndCreatesShortcut()
    {
        var copied = new List<(string Source, string Target)>();
        var shortcuts = new List<(string ShortcutPath, string TargetPath)>();

        var installer = new SelfInstaller(
            copyFile: (source, target, overwrite) => copied.Add((source, target)),
            ensureDirectory: _ => { },
            shortcutService: new FakeShortcutService((shortcut, target) => shortcuts.Add((shortcut, target))));

        installer.Install(
            sourceExePath: @"C:\Users\me\Downloads\Leigod_Auto_Pause.exe",
            targetDirectory: @"D:\Leigod",
            desktopDirectory: @"C:\Users\me\Desktop");

        Assert.Contains(copied, x => x.Target == @"D:\Leigod\Leigod_Auto_Pause.exe");
        Assert.Contains(shortcuts, x => x.ShortcutPath == @"C:\Users\me\Desktop\雷神自动暂停.lnk");
    }

    private sealed class FakeShortcutService(Action<string, string> onCreate) : IShortcutService
    {
        public void CreateOrUpdate(string shortcutPath, string targetPath, string workingDirectory, string arguments, string iconLocation)
        {
            onCreate(shortcutPath, targetPath);
        }
    }
}
