# AsarSharp
A simple library that lets you archive and extract asar files, in C#.

# Usage
This library exposes 2 disposable classes: `AsarArchiver` and `AsarExtractor`.

#### 📚 Archiving a directory/file 
```
using AsarArchiver archiver = new(pathToDirectoryOrFile, pathToArchive);
archiver.Archive(options);
```

#### 💥 Extracting an archive 
```
using AsarExtractor extractor = new(pathToArchive, pathToWhereToExtract)
extractor.Extract();
```

# Options
### Archiving


##### `ArchivingOptions` in namespace `AsarSharp.Utils.Types`
| Option          | Type       | Default value | What it does                                                                                                                                                          |
|-----------------|------------|---------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `MatchBasename` | `bool`     | `false`       | 	Match against the filename of the path. So basically, if this is set to `true`, for `path/to/file.txt`, `*.txt` will match. This may only be used with the `Unpack` option. |
| `Unpack`        | `string[]` | `null`        | List of globs to match against every file archived. It is like the argument received from the `--unpack` flag from the asar CLI.                                   |




### Remarks
- This library logs additional data when built in debug mode.
- Because symlinks require admin privileges in order to be created, it's not suitable for this library to ask for such
privileges in order to run. Therefore, every symlink will be replaced with the file or directory it points to.

## Contact
If you want to contact the original developer of this library, here's their Discord server: https://discord.gg/Bd2JnFB