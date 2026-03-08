# Auto Installer UX Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make first-run installation fully automatic so the user can open the downloaded `Leigod_Auto_Pause.exe` from anywhere, have it copy itself into the Leigod install directory, create a desktop shortcut, and then continue the normal patch-and-launch flow from the installed location.

**Architecture:** Split startup into two phases: a bootstrap phase that decides whether the current executable is already installed, and the existing patch phase that only runs once the launcher is executing beside Leigod. The bootstrap phase should auto-discover the Leigod install path, copy the launcher into that directory under a stable filename, create/update a desktop shortcut, and relaunch the installed copy with a marker argument to avoid recursion.

**Tech Stack:** C#/.NET 8 WinExe, existing `AsarSharp`, Windows registry/process/path discovery, Windows desktop shortcut creation via COM (`WScript.Shell`) behind a testable abstraction, xUnit test project.

> Note after investigation: this UX only works correctly when the distributed launcher is a true single-file release artifact. Validating copy-only behavior against `bin/Release/net8.0` or test output is invalid because those directories contain sidecar runtime files.

---

### Task 1: Create a testable bootstrap model

**Files:**
- Modify: `src/Leigod_Auto_Pause/Leigod_Auto_Pause.csproj`
- Modify: `src/Leigod_Auto_Pause/Program.cs`
- Modify: `src/Leigod_Auto_Pause.sln`
- Create: `src/Leigod_Auto_Pause/Installer/LaunchBootstrap.cs`
- Create: `src/Leigod_Auto_Pause/Installer/LaunchBootstrapResult.cs`
- Create: `src/Leigod_Auto_Pause/Installer/LeigodInstallCandidate.cs`
- Create: `tests/Leigod_Auto_Pause.Tests/Leigod_Auto_Pause.Tests.csproj`
- Create: `tests/Leigod_Auto_Pause.Tests/Installer/LaunchBootstrapTests.cs`

**Step 1: Write the failing test**

```csharp
using Xunit;

namespace Leigod_Auto_Pause.Tests.Installer;

public class LaunchBootstrapTests
{
    [Fact]
    public void Decide_WhenCurrentDirectoryAlreadyContainsLeigodFiles_ReturnsAlreadyInstalled()
    {
        var bootstrap = new LaunchBootstrap();
        var result = bootstrap.Decide(
            executablePath: @"C:\Leigod\Leigod_Auto_Pause.exe",
            currentDirectory: @"C:\Leigod",
            candidates: []);

        Assert.Equal(BootstrapAction.RunInPlace, result.Action);
        Assert.Equal(@"C:\Leigod", result.TargetDirectory);
    }

    [Fact]
    public void Decide_WhenOutsideLeigodDirectory_ReturnsInstallAndRelaunch()
    {
        var bootstrap = new LaunchBootstrap();
        var result = bootstrap.Decide(
            executablePath: @"C:\Users\me\Downloads\Leigod_Auto_Pause.exe",
            currentDirectory: @"C:\Users\me\Downloads",
            candidates:
            [
                new LeigodInstallCandidate(@"D:\Leigod", 100)
            ]);

        Assert.Equal(BootstrapAction.InstallAndRelaunch, result.Action);
        Assert.Equal(@"D:\Leigod\Leigod_Auto_Pause.exe", result.InstalledExecutablePath);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/Leigod_Auto_Pause.Tests/Leigod_Auto_Pause.Tests.csproj --filter LaunchBootstrapTests -v minimal`
Expected: FAIL with missing test project or missing `LaunchBootstrap` types.

**Step 3: Write minimal implementation**

```csharp
namespace Leigod_Auto_Pause.Installer;

public enum BootstrapAction
{
    RunInPlace,
    InstallAndRelaunch,
    Abort
}

public sealed record LeigodInstallCandidate(string DirectoryPath, int Score);

public sealed record LaunchBootstrapResult(
    BootstrapAction Action,
    string? TargetDirectory,
    string? InstalledExecutablePath,
    string? ErrorMessage);

public sealed class LaunchBootstrap
{
    public LaunchBootstrapResult Decide(
        string executablePath,
        string currentDirectory,
        IReadOnlyList<LeigodInstallCandidate> candidates)
    {
        if (File.Exists(Path.Combine(currentDirectory, "resources", "app.asar")) &&
            File.Exists(Path.Combine(currentDirectory, "leigod_launcher.exe")))
        {
            return new LaunchBootstrapResult(BootstrapAction.RunInPlace, currentDirectory, executablePath, null);
        }

        var bestCandidate = candidates.OrderByDescending(x => x.Score).FirstOrDefault();
        if (bestCandidate is null)
        {
            return new LaunchBootstrapResult(BootstrapAction.Abort, null, null, "未找到雷神安装目录。");
        }

        var installedExe = Path.Combine(bestCandidate.DirectoryPath, "Leigod_Auto_Pause.exe");
        return new LaunchBootstrapResult(BootstrapAction.InstallAndRelaunch, bestCandidate.DirectoryPath, installedExe, null);
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/Leigod_Auto_Pause.Tests/Leigod_Auto_Pause.Tests.csproj --filter LaunchBootstrapTests -v minimal`
Expected: PASS

**Step 5: Commit**

```bash
git add src/Leigod_Auto_Pause/Leigod_Auto_Pause.csproj src/Leigod_Auto_Pause/Program.cs src/Leigod_Auto_Pause.sln src/Leigod_Auto_Pause/Installer/LaunchBootstrap.cs src/Leigod_Auto_Pause/Installer/LaunchBootstrapResult.cs src/Leigod_Auto_Pause/Installer/LeigodInstallCandidate.cs tests/Leigod_Auto_Pause.Tests/Leigod_Auto_Pause.Tests.csproj tests/Leigod_Auto_Pause.Tests/Installer/LaunchBootstrapTests.cs
git commit -m "test: add bootstrap decision coverage"
```

### Task 2: Auto-discover the Leigod install directory

**Files:**
- Modify: `src/Leigod_Auto_Pause/Program.cs`
- Create: `src/Leigod_Auto_Pause/Installer/LeigodInstallLocator.cs`
- Create: `src/Leigod_Auto_Pause/Installer/IRegistryReader.cs`
- Create: `src/Leigod_Auto_Pause/Installer/SystemRegistryReader.cs`
- Create: `tests/Leigod_Auto_Pause.Tests/Installer/LeigodInstallLocatorTests.cs`

**Step 1: Write the failing test**

```csharp
using Xunit;

namespace Leigod_Auto_Pause.Tests.Installer;

public class LeigodInstallLocatorTests
{
    [Fact]
    public void Locate_PrefersRegistryInstallLocation_WhenItContainsRequiredFiles()
    {
        var registry = new FakeRegistryReader(new[]
        {
            @"C:\Program Files\Leigod"
        });

        var locator = new LeigodInstallLocator(registry, path => path switch
        {
            @"C:\Program Files\Leigod\resources\app.asar" => true,
            @"C:\Program Files\Leigod\leigod_launcher.exe" => true,
            _ => false
        });

        var result = locator.LocateBestCandidate();

        Assert.NotNull(result);
        Assert.Equal(@"C:\Program Files\Leigod", result!.DirectoryPath);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/Leigod_Auto_Pause.Tests/Leigod_Auto_Pause.Tests.csproj --filter LeigodInstallLocatorTests -v minimal`
Expected: FAIL with missing `LeigodInstallLocator` or `FakeRegistryReader`.

**Step 3: Write minimal implementation**

```csharp
public interface IRegistryReader
{
    IEnumerable<string> ReadInstallLocations();
}

public sealed class LeigodInstallLocator
{
    private readonly IRegistryReader _registryReader;
    private readonly Func<string, bool> _fileExists;

    public LeigodInstallLocator(IRegistryReader registryReader, Func<string, bool>? fileExists = null)
    {
        _registryReader = registryReader;
        _fileExists = fileExists ?? File.Exists;
    }

    public LeigodInstallCandidate? LocateBestCandidate()
    {
        var candidates = _registryReader.ReadInstallLocations()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(IsValidLeigodDirectory)
            .Select(path => new LeigodInstallCandidate(path, 100))
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        return candidates;
    }

    private bool IsValidLeigodDirectory(string directoryPath)
    {
        return _fileExists(Path.Combine(directoryPath, "resources", "app.asar")) &&
               _fileExists(Path.Combine(directoryPath, "leigod_launcher.exe"));
    }
}
```

Implementation notes to follow immediately after the first green test:

- Extend `SystemRegistryReader` to read both `HKLM` and `HKCU` uninstall keys in 32-bit and 64-bit views.
- Add fallback candidates from:
  - running `leigod_launcher` / `leigod` process paths
  - `%ProgramFiles%`
  - `%ProgramFiles(x86)%`
  - `%LocalAppData%`
- Score registry hits highest, running-process paths second, common-directory matches third.

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/Leigod_Auto_Pause.Tests/Leigod_Auto_Pause.Tests.csproj --filter LeigodInstallLocatorTests -v minimal`
Expected: PASS

**Step 5: Commit**

```bash
git add src/Leigod_Auto_Pause/Program.cs src/Leigod_Auto_Pause/Installer/LeigodInstallLocator.cs src/Leigod_Auto_Pause/Installer/IRegistryReader.cs src/Leigod_Auto_Pause/Installer/SystemRegistryReader.cs tests/Leigod_Auto_Pause.Tests/Installer/LeigodInstallLocatorTests.cs
git commit -m "feat: auto-detect leigod install directory"
```

### Task 3: Implement self-copy and desktop shortcut creation

**Files:**
- Modify: `src/Leigod_Auto_Pause/Program.cs`
- Create: `src/Leigod_Auto_Pause/Installer/SelfInstaller.cs`
- Create: `src/Leigod_Auto_Pause/Installer/DesktopShortcutService.cs`
- Create: `src/Leigod_Auto_Pause/Installer/IShortcutService.cs`
- Create: `tests/Leigod_Auto_Pause.Tests/Installer/SelfInstallerTests.cs`
- Create: `tests/Leigod_Auto_Pause.Tests/Installer/DesktopShortcutServiceTests.cs`

**Step 1: Write the failing test**

```csharp
using Xunit;

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
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/Leigod_Auto_Pause.Tests/Leigod_Auto_Pause.Tests.csproj --filter SelfInstallerTests -v minimal`
Expected: FAIL with missing `SelfInstaller` or `IShortcutService`.

**Step 3: Write minimal implementation**

```csharp
public interface IShortcutService
{
    void CreateOrUpdate(string shortcutPath, string targetPath, string workingDirectory, string arguments, string iconLocation);
}

public sealed class SelfInstaller
{
    private const string InstalledFileName = "Leigod_Auto_Pause.exe";
    private readonly Action<string, string, bool> _copyFile;
    private readonly Action<string> _ensureDirectory;
    private readonly IShortcutService _shortcutService;

    public SelfInstaller(
        Action<string, string, bool>? copyFile = null,
        Action<string>? ensureDirectory = null,
        IShortcutService? shortcutService = null)
    {
        _copyFile = copyFile ?? File.Copy;
        _ensureDirectory = ensureDirectory ?? Directory.CreateDirectory;
        _shortcutService = shortcutService ?? new DesktopShortcutService();
    }

    public string Install(string sourceExePath, string targetDirectory, string desktopDirectory)
    {
        _ensureDirectory(targetDirectory);
        var installedExePath = Path.Combine(targetDirectory, InstalledFileName);
        _copyFile(sourceExePath, installedExePath, true);

        var shortcutPath = Path.Combine(desktopDirectory, "雷神自动暂停.lnk");
        _shortcutService.CreateOrUpdate(shortcutPath, installedExePath, targetDirectory, "", installedExePath);
        return installedExePath;
    }
}
```

Implementation notes to follow immediately after the first green test:

- `DesktopShortcutService` should use `Type.GetTypeFromProgID("WScript.Shell")` and `CreateShortcut`.
- Set:
  - `TargetPath = installedExePath`
  - `WorkingDirectory = targetDirectory`
  - `IconLocation = installedExePath`
  - `Description = "启动雷神自动暂停插件"`
- If the shortcut already exists, overwrite it in place.
- Preserve the stable shortcut name `雷神自动暂停.lnk` so README and support docs remain simple.

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/Leigod_Auto_Pause.Tests/Leigod_Auto_Pause.Tests.csproj --filter "SelfInstallerTests|DesktopShortcutServiceTests" -v minimal`
Expected: PASS

**Step 5: Commit**

```bash
git add src/Leigod_Auto_Pause/Program.cs src/Leigod_Auto_Pause/Installer/SelfInstaller.cs src/Leigod_Auto_Pause/Installer/DesktopShortcutService.cs src/Leigod_Auto_Pause/Installer/IShortcutService.cs tests/Leigod_Auto_Pause.Tests/Installer/SelfInstallerTests.cs tests/Leigod_Auto_Pause.Tests/Installer/DesktopShortcutServiceTests.cs
git commit -m "feat: self-install launcher and create desktop shortcut"
```

### Task 4: Integrate bootstrap flow into startup and prevent recursion

**Files:**
- Modify: `src/Leigod_Auto_Pause/Program.cs`
- Create: `src/Leigod_Auto_Pause/Installer/BootstrapArguments.cs`
- Create: `tests/Leigod_Auto_Pause.Tests/Installer/BootstrapArgumentsTests.cs`
- Create: `tests/Leigod_Auto_Pause.Tests/Installer/ProgramBootstrapFlowTests.cs`

**Step 1: Write the failing test**

```csharp
using Xunit;

namespace Leigod_Auto_Pause.Tests.Installer;

public class BootstrapArgumentsTests
{
    [Fact]
    public void Parse_WhenInstalledLaunchFlagPresent_ReturnsInstalledLaunchTrue()
    {
        var parsed = BootstrapArguments.Parse(new[] { "--installed-launch" });
        Assert.True(parsed.IsInstalledLaunch);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/Leigod_Auto_Pause.Tests/Leigod_Auto_Pause.Tests.csproj --filter "BootstrapArgumentsTests|ProgramBootstrapFlowTests" -v minimal`
Expected: FAIL with missing parsing/bootstrap integration types.

**Step 3: Write minimal implementation**

```csharp
public sealed record BootstrapArguments(bool IsInstalledLaunch)
{
    public static BootstrapArguments Parse(string[] args)
        => new(args.Any(x => string.Equals(x, "--installed-launch", StringComparison.OrdinalIgnoreCase)));
}
```

Integration behavior to implement in `Program.Main`:

```csharp
var bootstrapArgs = BootstrapArguments.Parse(args);
var currentExePath = Environment.ProcessPath ?? throw new InvalidOperationException("Missing process path.");
var currentDirectory = AppContext.BaseDirectory;

if (!bootstrapArgs.IsInstalledLaunch)
{
    var candidate = locator.LocateBestCandidate();
    var decision = bootstrap.Decide(currentExePath, currentDirectory, candidate is null ? [] : [candidate]);

    if (decision.Action == BootstrapAction.InstallAndRelaunch)
    {
        var installedExe = installer.Install(currentExePath, decision.TargetDirectory!, desktopDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = installedExe,
            Arguments = "--installed-launch",
            UseShellExecute = true,
            Verb = "runas"
        });
        return;
    }

    if (decision.Action == BootstrapAction.Abort)
    {
        MessageBox(IntPtr.Zero, decision.ErrorMessage!, "安装失败", 0x10);
        return;
    }
}
```

Critical integration rules:

- Move the admin-elevation check after bootstrap decision.
- Only require `runas` when launching the installed copy or when patching the Leigod directory.
- If the current executable is already the installed copy, skip self-copy and continue the existing patch flow.
- Keep the existing `NeedUpdate -> applyPatch -> LaunchLeigod` flow intact once running in the target directory.

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/Leigod_Auto_Pause.Tests/Leigod_Auto_Pause.Tests.csproj --filter "BootstrapArgumentsTests|ProgramBootstrapFlowTests" -v minimal`
Expected: PASS

**Step 5: Commit**

```bash
git add src/Leigod_Auto_Pause/Program.cs src/Leigod_Auto_Pause/Installer/BootstrapArguments.cs tests/Leigod_Auto_Pause.Tests/Installer/BootstrapArgumentsTests.cs tests/Leigod_Auto_Pause.Tests/Installer/ProgramBootstrapFlowTests.cs
git commit -m "feat: bootstrap auto-install flow on first launch"
```

### Task 5: Update user docs and verify the end-to-end UX

**Files:**
- Modify: `README.md`

**Step 1: Write the failing test**

No code test here. Write a manual verification checklist first:

```text
1. 删除桌面旧快捷方式
2. 把发布好的 Leigod_Auto_Pause.exe 放到 Downloads
3. 双击 Downloads 中的 exe
4. 验证目标目录生成 Leigod_Auto_Pause.exe
5. 验证桌面生成“雷神自动暂停.lnk”
6. 验证雷神正常启动并能继续补丁流程
```

**Step 2: Run test to verify it fails**

Run: `dotnet build src/Leigod_Auto_Pause.sln -c Release`
Expected: PASS build, but manual checklist not yet satisfied until implementation is complete.

**Step 3: Write minimal implementation**

Update `README.md` installation section so it no longer tells users to manually copy the file or hand-create a shortcut. Replace it with:

```markdown
1. 下载 `Leigod_Auto_Pause.exe`
2. 双击运行
3. 程序会自动定位雷神安装目录并完成部署
4. 桌面会自动创建“雷神自动暂停”快捷方式
5. 以后只需从该快捷方式启动
```

Also add a troubleshooting note:

```markdown
如果程序未能自动定位雷神安装目录，会弹出错误提示；此时需要检查雷神是否已正确安装，或是否被移动到非常规目录。
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/Leigod_Auto_Pause.Tests/Leigod_Auto_Pause.Tests.csproj -v minimal`
Expected: PASS

Run: `dotnet build src/Leigod_Auto_Pause.sln -c Release`
Expected: PASS

Run: Manual checklist above on a machine with Leigod installed.
Expected: launcher self-copies, desktop shortcut is created, and the installed copy starts Leigod successfully.

**Step 5: Commit**

```bash
git add README.md
git commit -m "docs: describe automatic installer ux"
```

## Implementation notes

- Keep the installed launcher filename stable as `Leigod_Auto_Pause.exe`; do not derive it from the downloaded source filename.
- Treat the target directory as valid only if both `resources\app.asar` and `leigod_launcher.exe` exist.
- Avoid infinite relaunch loops by using the `--installed-launch` flag and by short-circuiting when `AppContext.BaseDirectory` already equals the chosen target directory.
- Preserve the existing patcher behavior after bootstrap. This change should add a preflight stage, not rewrite patch logic.
- Prefer pure functions and injected delegates for bootstrap logic so discovery and file operations are testable without touching the real machine.

## Verification checklist

- Fresh download run from `Downloads` installs to the detected Leigod directory.
- Desktop shortcut is created or updated automatically.
- Second launch from the desktop shortcut does not trigger another self-copy loop.
- Existing in-place install still works.
- `NeedUpdate` and `applyPatch` behavior remains unchanged once running beside Leigod.
- Build and test suite both pass.
