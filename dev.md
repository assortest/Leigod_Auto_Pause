# Dev Notes

## Formatting

Use the repository `.editorconfig` as the single formatting contract for `dotnet format`, Visual Studio, and Rider.

- Repository text files use `utf-8` with `CRLF`.
- C# uses 4-space indentation and braces on new lines.
- `main.js` keeps 2-space indentation.
- Markdown keeps trailing spaces when they are meaningful for layout.

Recommended formatting command:

```powershell
dotnet format src/Leigod_Auto_Pause.sln --no-restore
```

## Release Build

Do not validate installer behavior from `bin/Release/net8.0` or any test output directory.
Installer validation must use the published self-contained single-file artifact so the executable shape matches the official release.

Recommended publish command:

```powershell
dotnet publish src/Leigod_Auto_Pause/Leigod_Auto_Pause.csproj /p:PublishProfile=ReleaseSingleFile -o artifacts/publish/single-file
```
