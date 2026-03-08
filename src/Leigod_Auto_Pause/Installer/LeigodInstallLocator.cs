using System.Diagnostics;

namespace Leigod_Auto_Pause.Installer;

public sealed class LeigodInstallLocator
{
    private readonly IRegistryReader _registryReader;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<IEnumerable<string>> _processDirectories;
    private readonly Func<IEnumerable<string>> _commonDirectories;

    public LeigodInstallLocator(
        IRegistryReader registryReader,
        Func<string, bool>? fileExists = null,
        Func<IEnumerable<string>>? processDirectories = null,
        Func<IEnumerable<string>>? commonDirectories = null)
    {
        _registryReader = registryReader;
        _fileExists = fileExists ?? File.Exists;
        _processDirectories = processDirectories ?? GetRunningProcessDirectories;
        _commonDirectories = commonDirectories ?? GetCommonDirectories;
    }

    public LeigodInstallCandidate? LocateBestCandidate()
    {
        return EnumerateScoredDirectories()
            .GroupBy(path => Normalize(path.DirectoryPath), StringComparer.OrdinalIgnoreCase)
            .Select(group => new LeigodInstallCandidate(group.Key, group.Max(x => x.Score)))
            .Where(candidate => IsValidLeigodDirectory(candidate.DirectoryPath))
            .OrderByDescending(candidate => candidate.Score)
            .FirstOrDefault();
    }

    private IEnumerable<LeigodInstallCandidate> EnumerateScoredDirectories()
    {
        foreach (var path in _registryReader.ReadInstallLocations())
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return new LeigodInstallCandidate(path, 100);
            }
        }

        foreach (var path in _processDirectories())
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return new LeigodInstallCandidate(path, 80);
            }
        }

        foreach (var path in _commonDirectories())
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                yield return new LeigodInstallCandidate(path, 60);
            }
        }
    }

    private bool IsValidLeigodDirectory(string directoryPath)
    {
        return _fileExists(Path.Combine(directoryPath, "resources", "app.asar")) &&
               _fileExists(Path.Combine(directoryPath, "leigod_launcher.exe"));
    }

    private static IEnumerable<string> GetRunningProcessDirectories()
    {
        foreach (var processName in new[] { "leigod_launcher", "leigod" })
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(processName);
            }
            catch
            {
                continue;
            }

            foreach (var process in processes)
            {
                using (process)
                {
                    string? directory = null;
                    try
                    {
                        directory = Path.GetDirectoryName(process.MainModule?.FileName);
                    }
                    catch
                    {
                        directory = null;
                    }

                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        yield return directory;
                    }
                }
            }
        }
    }

    private static IEnumerable<string> GetCommonDirectories()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };

        foreach (var root in roots.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var child in new[] { "Leigod", "雷神加速器" })
            {
                yield return Path.Combine(root, child);
            }
        }
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
