namespace Leigod_Auto_Pause.Installer;

public interface IShortcutService
{
    void CreateOrUpdate(string shortcutPath, string targetPath, string workingDirectory, string arguments, string iconLocation);
}
