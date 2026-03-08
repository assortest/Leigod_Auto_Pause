namespace Leigod_Auto_Pause.Installer;

public interface IRegistryReader
{
    IEnumerable<string> ReadInstallLocations();
}
