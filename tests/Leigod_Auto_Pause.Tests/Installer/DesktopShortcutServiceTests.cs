using Leigod_Auto_Pause.Installer;

namespace Leigod_Auto_Pause.Tests.Installer;

public class DesktopShortcutServiceTests
{
    [Fact]
    public void CreateOrUpdate_PopulatesShortcutFields()
    {
        var shell = new FakeShell();
        var service = new DesktopShortcutService(() => shell);

        service.CreateOrUpdate(
            @"C:\Users\me\Desktop\雷神自动暂停.lnk",
            @"D:\Leigod\Leigod_Auto_Pause.exe",
            @"D:\Leigod",
            "",
            @"D:\Leigod\Leigod_Auto_Pause.exe");

        Assert.NotNull(shell.Shortcut);
        Assert.Equal(@"D:\Leigod\Leigod_Auto_Pause.exe", shell.Shortcut!.TargetPath);
        Assert.Equal(@"D:\Leigod", shell.Shortcut.WorkingDirectory);
        Assert.True(shell.Shortcut.Saved);
    }

    private sealed class FakeShell : IShortcutShell
    {
        public FakeShortcut? Shortcut { get; private set; }

        public IShortcutFile CreateShortcut(string shortcutPath)
        {
            Shortcut = new FakeShortcut { ShortcutPath = shortcutPath };
            return Shortcut;
        }
    }

    private sealed class FakeShortcut : IShortcutFile
    {
        public string? ShortcutPath { get; set; }
        public string TargetPath { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string IconLocation { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool Saved { get; private set; }

        public void Save()
        {
            Saved = true;
        }
    }
}
