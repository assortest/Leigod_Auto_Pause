# Single-File Release Bootstrap Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Align the launcher with the original v2.0 distribution model by publishing a true single-file `Leigod_Auto_Pause.exe`, then keep the new auto-install UX but make it copy only that single executable into the Leigod install directory.

**Architecture:** The current bootstrap flow is directionally correct, but it assumes the copied executable can run alone. That assumption is only valid if release output is a real single-file app. The fix is to make release publishing produce one runnable exe, validate that the installed copy can launch independently, and simplify installer logic so it copies exactly one file and relaunches it.

**Tech Stack:** C#/.NET 8 WinExe, `dotnet publish`, Windows desktop shortcut creation via COM, existing Asar patch flow, xUnit.

---

### Task 1: Reproduce the packaging mismatch with an explicit release test

**Files:**
- Create: `tests/Leigod_Auto_Pause.Tests/Packaging/ReleasePackagingFacts.md`
- Modify: `docs/plans/2026-03-08-auto-installer-ux.md`

**Step 1: Write the failing test**

Write a manual packaging fact sheet that captures the bug precisely:

```text
Observed mismatch:
1. Current installer copies only Leigod_Auto_Pause.exe
2. Current build output under bin/Release/net8.0 also requires dll/json sidecar files
3. Therefore copied exe is not equivalent to original v2.0 release behavior
```

**Step 2: Run test to verify it fails**

Run: `Get-ChildItem src/Leigod_Auto_Pause/bin/Release/net8.0`
Expected: Multiple runtime files exist beside `Leigod_Auto_Pause.exe`, so current assumption is invalid.

**Step 3: Write minimal implementation**

Record the conclusion in `tests/Leigod_Auto_Pause.Tests/Packaging/ReleasePackagingFacts.md` and annotate the previous plan file to mark the old copy-only assumption as invalid for framework-dependent output.

**Step 4: Run test to verify it passes**

Run: `Get-Content tests/Leigod_Auto_Pause.Tests/Packaging/ReleasePackagingFacts.md`
Expected: The packaging mismatch is explicitly documented and no longer implicit.

**Step 5: Commit**

```bash
git add tests/Leigod_Auto_Pause.Tests/Packaging/ReleasePackagingFacts.md docs/plans/2026-03-08-auto-installer-ux.md
git commit -m "docs: capture single-file packaging requirement"
```

### Task 2: Convert release publishing to a real single-file executable

**Files:**
- Modify: `src/Leigod_Auto_Pause/Leigod_Auto_Pause.csproj`
- Create: `src/Leigod_Auto_Pause/Properties/PublishProfiles/ReleaseSingleFile.pubxml`
- Create: `tests/Leigod_Auto_Pause.Tests/Packaging/PublishSettingsTests.cs`

**Step 1: Write the failing test**

```csharp
using System.Xml.Linq;
using Xunit;

namespace Leigod_Auto_Pause.Tests.Packaging;

public class PublishSettingsTests
{
    [Fact]
    public void ReleaseProfile_EnablesSingleFileSelfContainedPublish()
    {
        var doc = XDocument.Load(@"src/Leigod_Auto_Pause/Properties/PublishProfiles/ReleaseSingleFile.pubxml");
        var values = doc.Root!.Elements().ToDictionary(x => x.Name.LocalName, x => x.Value);

        Assert.Equal("true", values["PublishSingleFile"]);
        Assert.Equal("true", values["SelfContained"]);
        Assert.Equal("win-x64", values["RuntimeIdentifier"]);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/Leigod_Auto_Pause.Tests/Leigod_Auto_Pause.Tests.csproj --filter PublishSettingsTests -v minimal`
Expected: FAIL because publish profile does not exist yet.

**Step 3: Write minimal implementation**

Add a publish profile with these settings:

```xml
<Project>
  <PropertyGroup>
    <Configuration>Release</Configuration>
    <TargetFramework>net8.0</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>false</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <PublishTrimmed>false</PublishTrimmed>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <EnableCompressionInSingleFile>false</EnableCompressionInSingleFile>
  </PropertyGroup>
</Project>
```

Also add equivalent guarded defaults in the `.csproj` if needed so CLI publish is stable in CI and local usage.

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/Leigod_Auto_Pause.Tests/Leigod_Auto_Pause.Tests.csproj --filter PublishSettingsTests -v minimal`
Expected: PASS

**Step 5: Commit**

```bash
git add src/Leigod_Auto_Pause/Leigod_Auto_Pause.csproj src/Leigod_Auto_Pause/Properties/PublishProfiles/ReleaseSingleFile.pubxml tests/Leigod_Auto_Pause.Tests/Packaging/PublishSettingsTests.cs
git commit -m "build: add single-file release publish profile"
```

### Task 3: Prove the published artifact is a single-file runnable payload

**Files:**
- Create: `tests/Leigod_Auto_Pause.Tests/Packaging/PublishOutputFacts.md`

**Step 1: Write the failing test**

Define the manual acceptance check:

```text
1. Run dotnet publish with ReleaseSingleFile profile
2. Inspect publish folder
3. Expect exactly one app executable payload for the launcher, not a dll/json sidecar set
```

**Step 2: Run test to verify it fails**

Run: `dotnet publish src/Leigod_Auto_Pause/Leigod_Auto_Pause.csproj /p:PublishProfile=ReleaseSingleFile -o artifacts/publish/single-file`
Expected: before config is correct, output is not yet validated as single-file.

**Step 3: Write minimal implementation**

After the publish profile exists, publish and document expected output in `tests/Leigod_Auto_Pause.Tests/Packaging/PublishOutputFacts.md`, including the produced file names and approximate size compared to upstream release expectations.

**Step 4: Run test to verify it passes**

Run: `Get-ChildItem artifacts/publish/single-file | Select-Object Name,Length`
Expected: one main `Leigod_Auto_Pause.exe` launcher payload; any remaining files must be explicitly understood and justified.

**Step 5: Commit**

```bash
git add tests/Leigod_Auto_Pause.Tests/Packaging/PublishOutputFacts.md
git commit -m "test: verify single-file publish output"
```

### Task 4: Simplify installer flow to copy only the published executable

**Files:**
- Modify: `src/Leigod_Auto_Pause/Program.cs`
- Modify: `src/Leigod_Auto_Pause/Installer/SelfInstaller.cs`
- Modify: `src/Leigod_Auto_Pause/Installer/LaunchBootstrap.cs`
- Modify: `tests/Leigod_Auto_Pause.Tests/Installer/SelfInstallerTests.cs`
- Modify: `tests/Leigod_Auto_Pause.Tests/Installer/LaunchBootstrapTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public void Install_WhenRunningFromSingleFilePayload_CopiesOnlyExecutable()
{
    var copied = new List<(string Source, string Target)>();

    var installer = new SelfInstaller(
        copyFile: (source, target, overwrite) => copied.Add((source, target)),
        ensureDirectory: _ => { },
        shortcutService: new FakeShortcutService((_, _) => { }));

    installer.Install(
        sourceExePath: @"C:\Artifacts\Leigod_Auto_Pause.exe",
        targetDirectory: @"D:\Leigod",
        desktopDirectory: @"C:\Users\me\Desktop");

    Assert.Single(copied);
    Assert.Equal(@"D:\Leigod\Leigod_Auto_Pause.exe", copied[0].Target);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/Leigod_Auto_Pause.Tests/Leigod_Auto_Pause.Tests.csproj --filter "SelfInstallerTests|LaunchBootstrapTests" -v minimal`
Expected: FAIL if installer still assumes development build layout or carries redundant bootstrap branches.

**Step 3: Write minimal implementation**

Refactor with these goals:

- Treat published single-file exe as the only artifact that must be copied.
- Remove any logic that assumes sidecar runtime files need to exist after installation.
- Keep recursion prevention via `--installed-launch`.
- Keep target validation requirement:
  - `resources\app.asar`
  - `leigod_launcher.exe`

If possible, collapse installer abstractions:

- merge `LaunchBootstrapResult` into `LaunchBootstrap`
- keep `SelfInstaller` small and imperative
- leave registry probing isolated, but avoid unnecessary DTO churn

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/Leigod_Auto_Pause.Tests/Leigod_Auto_Pause.Tests.csproj --filter "SelfInstallerTests|LaunchBootstrapTests" -v minimal`
Expected: PASS

**Step 5: Commit**

```bash
git add src/Leigod_Auto_Pause/Program.cs src/Leigod_Auto_Pause/Installer/SelfInstaller.cs src/Leigod_Auto_Pause/Installer/LaunchBootstrap.cs tests/Leigod_Auto_Pause.Tests/Installer/SelfInstallerTests.cs tests/Leigod_Auto_Pause.Tests/Installer/LaunchBootstrapTests.cs
git commit -m "refactor: align installer with single-file release model"
```

### Task 5: Validate end-to-end first-run behavior against the original v2.0 expectation

**Files:**
- Modify: `README.md`
- Modify: `dev.md`

**Step 1: Write the failing test**

Write a manual checklist first:

```text
1. Publish single-file release payload
2. Copy only that exe to Downloads
3. Double-click it
4. Confirm exe is copied into Leigod install directory
5. Confirm desktop shortcut is created
6. Confirm installed copy launches and continues patch flow
7. Confirm second launch from shortcut does not loop
```

**Step 2: Run test to verify it fails**

Run: `dotnet publish src/Leigod_Auto_Pause/Leigod_Auto_Pause.csproj /p:PublishProfile=ReleaseSingleFile -o artifacts/publish/single-file`
Expected: publish completes, but manual checklist remains unverified until executed on a real Leigod machine.

**Step 3: Write minimal implementation**

Update docs so they refer to the published single-file release artifact, not `bin/Release/net8.0` development output:

- `README.md`: installation instructions must say users download the release exe and run it directly
- `dev.md`: add the recommended publish command for release verification

Suggested command block:

```powershell
dotnet publish src/Leigod_Auto_Pause/Leigod_Auto_Pause.csproj /p:PublishProfile=ReleaseSingleFile -o artifacts/publish/single-file
```

**Step 4: Run test to verify it passes**

Run: `dotnet test tests/Leigod_Auto_Pause.Tests/Leigod_Auto_Pause.Tests.csproj -v minimal`
Expected: PASS

Run: `dotnet publish src/Leigod_Auto_Pause/Leigod_Auto_Pause.csproj /p:PublishProfile=ReleaseSingleFile -o artifacts/publish/single-file`
Expected: PASS

Run: manual checklist above using the published exe.
Expected: behavior matches original v2.0 expectation: copy into target directory, then launch successfully from installed copy.

**Step 5: Commit**

```bash
git add README.md dev.md
git commit -m "docs: document single-file release workflow"
```

## Engineering constraints

- Do not validate installer behavior against `tests/.../bin/...` output. That is test harness output, not a release artifact.
- Do not validate installer behavior against `src/.../bin/Release/net8.0` unless that path is proven to be equivalent to final distribution. Prefer `dotnet publish`.
- The true acceptance target is: a user downloads one exe from releases, runs it from anywhere, it installs itself into Leigod, and the installed copy behaves like the original v2.0 release flow.
- Keep the existing Asar patch logic unchanged unless single-file publish exposes a concrete bug.
- Prefer reducing abstraction count if it does not reduce clarity.

## Verification checklist

- `dotnet test` passes.
- `dotnet publish` produces the expected single-file release payload.
- Installed copied exe can run without sidecar files.
- Desktop shortcut points at the installed exe.
- First launch and second launch both behave correctly.
- Manual verification is performed using the published artifact, not a test output directory.
