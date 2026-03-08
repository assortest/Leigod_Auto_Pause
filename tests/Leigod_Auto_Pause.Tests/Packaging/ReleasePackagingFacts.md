# Release Packaging Facts

Observed mismatch before the single-file release fix:

1. The installer copied only `Leigod_Auto_Pause.exe`.
2. The local development output under `src/Leigod_Auto_Pause/bin/Release/net8.0` still depended on sidecar files like:
   - `Leigod_Auto_Pause.dll`
   - `Leigod_Auto_Pause.deps.json`
   - `Leigod_Auto_Pause.runtimeconfig.json`
   - third-party dependency dlls
3. Therefore copying only the development-build exe could not faithfully reproduce the original v2.0 release behavior.

Conclusion:

- Installer validation must use the published release artifact.
- The intended model is a single downloadable exe that can be copied by itself into the Leigod install directory.
