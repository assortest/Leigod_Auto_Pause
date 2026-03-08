# Publish Output Facts

Verification run:

- Command:
  `dotnet publish src/Leigod_Auto_Pause/Leigod_Auto_Pause.csproj /p:PublishProfile=ReleaseSingleFile -o artifacts/publish/single-file`
- Result:
  publish succeeded on 2026-03-08
- Output directory contents:
  - `Leigod_Auto_Pause.exe`
  - `Leigod_Auto_Pause.pdb`
- Main executable size:
  - `68,647,310` bytes

Conclusions:

1. The publish profile now emits a self-contained single-file launcher artifact that matches the official release shape.
2. Installer validation should use this published artifact instead of `bin/Release/net8.0` or any test output directory.
3. The installer's "copy only the exe" model is aligned with official release packaging because the runtime is bundled into the executable.
