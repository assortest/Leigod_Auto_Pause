using Microsoft.Win32;

namespace Leigod_Auto_Pause.Installer;

public sealed class SystemRegistryReader : IRegistryReader
{
    private static readonly string[] UninstallRoots =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
    ];

    public IEnumerable<string> ReadInstallLocations()
    {
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                RegistryKey? baseKey = null;
                try
                {
                    baseKey = RegistryKey.OpenBaseKey(hive, view);
                }
                catch
                {
                    continue;
                }

                using (baseKey)
                {
                    foreach (var root in UninstallRoots)
                    {
                        using var uninstallKey = baseKey.OpenSubKey(root);
                        if (uninstallKey is null)
                        {
                            continue;
                        }

                        foreach (var subKeyName in uninstallKey.GetSubKeyNames())
                        {
                            using var subKey = uninstallKey.OpenSubKey(subKeyName);
                            if (subKey is null)
                            {
                                continue;
                            }

                            var displayName = subKey.GetValue("DisplayName") as string;
                            if (string.IsNullOrWhiteSpace(displayName) ||
                                displayName.IndexOf("雷神", StringComparison.OrdinalIgnoreCase) < 0 &&
                                displayName.IndexOf("leigod", StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                continue;
                            }

                            var installLocation = subKey.GetValue("InstallLocation") as string;
                            if (!string.IsNullOrWhiteSpace(installLocation))
                            {
                                yield return installLocation;
                            }

                            var displayIcon = subKey.GetValue("DisplayIcon") as string;
                            if (!string.IsNullOrWhiteSpace(displayIcon))
                            {
                                var iconDirectory = Path.GetDirectoryName(displayIcon.Trim('"'));
                                if (!string.IsNullOrWhiteSpace(iconDirectory))
                                {
                                    yield return iconDirectory;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
