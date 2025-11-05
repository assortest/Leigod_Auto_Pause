
namespace AsarSharp.Utils
{
    class Constants
    {
        public const ushort HeaderSizePickleObjectSize = 8;
        public enum ArchivingStep
        {
            UnpackedDirectoryCreation = 0,
            TemporaryFileCreation = 1,
            DirectoryStructureCreation = 2,
            ArchiveCreation = 3,
            ArchiveHeaderWrite = 4,
            ArchiveContentsWrite = 5,
        }

        public enum ExtractionStep
        {
            ArchiveHeaderFetch = 0,
            DirectoryStructureFetch = 1,
            ExtractionDataFetchSuccess = 2,
            ActualExtraction = 3,
            SymlinkHandling = 4,
            ExtractionSuccess = 5
        }

        private static readonly string[] _archivingSteps = {
            "Creating the {archiveName.asar.unpacked} directory...",
            "Creating temporary file for holding the archive data...",
            "Creating the directory structure and writing the file contents to the temporary file (may take a while)...",
            "Creating archive...",
            "Writing the archive header...",
            "Writing the archive contents (may take a while)...",
        };

        private static readonly string[] _extractionSteps =
        {
            "Getting archive data...",
            "Getting folder structure...",
            "All data needed for extraction has been obtained.",
            "Extracting...",
            "Handling symlinks...",
            "The archive has been successfully extracted!",
        };

        public static string GetStep(ArchivingStep step) => _archivingSteps[(ushort)step];
        public static string GetStep(ExtractionStep step) => _extractionSteps[(ushort)step];

        public class AsarExceptions
        {
            public const string insufficientHeaderBytes = "The header of this archive doesn't have the required amount of bytes.";
            public const string invalidArchiveFilePath = "The path to the archive is not an .asar file. It must end in .asar";
            public const string failedTypeConversion = "Could not convert the file offset from string to uint.";
            public const string archiveDirectoryMissing = "The given directory path from which to create the archive doesn't exist.";
            public const string archiveFileMissing = "The archive file from which to extract the files doesn't exist.";
            public const string symlinkMissing = "There's a symbolic link in this archive that points to a file or directory that doesn't exist.";
            public const string unpackedDirectoryMissing = "This archive has been generated with a few external files that haven't been packed for performance, security or other issues' sake. These files live in a directory right outside the archive, which follows the {archiveName.asar.unpacked} name scheme, and this directory doesn't exist.";
        }
    }
}
