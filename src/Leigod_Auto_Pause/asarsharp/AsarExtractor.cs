using System;
using System.IO;
using System.Collections.Generic;

using Newtonsoft.Json;

using static AsarSharp.Utils.Constants;
using static AsarSharp.Utils.Methods;


namespace AsarSharp
{
    /**
     * <summary>
     * Hosts all the needed methods, properties and fields for extracting an archive.
     * </summary>
     */
    public sealed class AsarExtractor : IDisposable
    {
        private readonly string _archiveFilePath;
        private readonly string _pathToExtractDirectory;
        private uint _headerSize;
        private dynamic _archiveData;
        private FileStream _archiveFileStream;

        private string _unpackedDirectoryPath = null;

        // Used for queueing symlinks in order to let the files associated with them be created first.
        private readonly Queue<(string symlinkPath, string symlinkTargetPath)> _symlinks = new();

        /**
         *  <param name="archiveFilePath">The ABSOLUTE path of the .asar archive.</param>
         *  <param name="extractInto">
         *  The ABSOLUTE directory path in which all the files will be extracted. If a directory
         *  doesn't exist at this path, a new one is created.
         *  </param>
         */
        public AsarExtractor(string archiveFilePath, string extractInto)
        {
            if (!archiveFilePath.EndsWith(".asar")) throw new FormatException(AsarExceptions.invalidArchiveFilePath);
            if (!File.Exists(archiveFilePath)) throw new FileNotFoundException(AsarExceptions.archiveFileMissing);

            _archiveFilePath = archiveFilePath;
            _pathToExtractDirectory = extractInto;
        }

        /**
         * <summary>
         * Extracts the given archive from the constructor to the given directory from the constructor. 
         * </summary>
         */
        public void Extract()
        {
            PrepareArchiveData();

            Directory.CreateDirectory(_pathToExtractDirectory);

            UnpackArchive(currentBranch: ref _archiveData, currentPath: _pathToExtractDirectory);

            DebugLog(GetStep(ExtractionStep.SymlinkHandling));
            DebugLog($"Symlinks found: {_symlinks.Count}");
            HandleSymlinks();

            DebugLog(GetStep(ExtractionStep.ExtractionSuccess));
        }

        /**
         * <summary>
         * Deserializes the JSON directory structure read from the header.
         * </summary>
         * */
        private void PrepareArchiveData()
        {

            DebugLog(GetStep(ExtractionStep.ArchiveHeaderFetch));
            var (headerJsonString, headerJsonStringSize) = GetArchiveHeaderData();

            DebugLog(GetStep(ExtractionStep.DirectoryStructureFetch));
            dynamic archiveDataDeserialized = JsonConvert.DeserializeObject(headerJsonString);

            DebugLog(GetStep(ExtractionStep.ExtractionDataFetchSuccess));
            DebugLog(GetStep(ExtractionStep.ActualExtraction));

            _headerSize = headerJsonStringSize;
            _archiveData = archiveDataDeserialized;
        }



        /**
         * <summary>
         * Recursively follows the JSON directory structure from the header of the asar archive, and 
         * creates the files and subdirectories that are within the archive.
         * </summary>
         * <param name="currentBranch"> The position in the JSON directory structure, which holds data about 
         * the following subdirectories or files that need to be extracted.</param>
         * <param name="currentPath"> The path for each subdirectory and file within the directory
         * that holds the extracted files.</param>
         * <exception cref="FormatException">The current file offset could not be converted from string to uint.</exception>
         */
        private void UnpackArchive(ref dynamic currentBranch, string currentPath = "")
        {
            bool isSymlink = currentBranch?.link != null;
            if (isSymlink)
            {
                string symlinkTargetPath = Path.Combine(_pathToExtractDirectory, (string)currentBranch.link);
                _symlinks.Enqueue(
                    (symlinkPath: currentPath, symlinkTargetPath)
                );

                return;
            }

            bool isDirectory = currentBranch?.files != null;
            if (!isDirectory)
            {
                CreateFileFromArchive(ref currentBranch, currentPath);
                return;
            }

            Directory.CreateDirectory(currentPath);

            // Lists all the files within the current directory/subdirectory.
            var directoryContents = currentBranch.files.Properties();

            foreach (var fileOrDirectory in directoryContents)
                UnpackArchive(currentBranch: ref fileOrDirectory.Value, currentPath: Path.Combine(currentPath, fileOrDirectory.Name));
        }

        /**
          * <summary>
          * Symlinks require elevated privileges in order to be created. It's not suitable for this library
          * to ask for such privileges in order to execute its code. So, we replace every symlink with the 
          * file or directory they point to.
          * </summary>
          */
        private void HandleSymlinks()
        {
            while (_symlinks.Count > 0)
            {
                var (symlinkPath, symlinkTargetPath) = _symlinks.Dequeue();
                bool isSymlinkTargetADirectory = File.GetAttributes(symlinkTargetPath).HasFlag(FileAttributes.Directory);

                if (!isSymlinkTargetADirectory)
                    File.Copy(symlinkTargetPath, symlinkPath, overwrite: true);
                else
                    CopyDirectory(directoryToCopy: symlinkTargetPath, whereToCopyDirectory: symlinkPath);
            }
        }

        /**
         * <summary>Creates a file that the archive contains.</summary>
         * <param name="currentBranch"> The position in the JSON directory structure, which holds data about 
         * the following subdirectories or files that need to be extracted.</param>
         * <param name="currentPath"> The path for each subdirectory and file within the directory
         * that holds the extracted files.</param>
         */
        private void CreateFileFromArchive(ref dynamic currentBranch, string currentPath)
        {
            if (currentBranch?.unpacked != null && currentBranch?.unpacked.Value == true)
            {
                if (_unpackedDirectoryPath == null)
                {
                    string unpackedDirectoryPath = Path.Join(Path.GetDirectoryName(_archiveFilePath), Path.GetFileName(_archiveFilePath) + ".unpacked");
                    if (!Directory.Exists(unpackedDirectoryPath))
                        throw new DirectoryNotFoundException(AsarExceptions.unpackedDirectoryMissing);
                    else
                        _unpackedDirectoryPath = unpackedDirectoryPath;
                }

                // The {archiveName.asar.unpacked} directory contains files with parallel paths to the files 
                // that are being extracted:
                // archiveExtractionTargetDirectory/path/to/file
                // {archiveName.asar.unpacked}/path/to/file
                // So we just replace, in the path, the first parent directory with the second.
                string filePathInUnpackedDirectory = Path.Join(_unpackedDirectoryPath, currentPath.Substring(_pathToExtractDirectory.Length + 1));
                File.Copy(filePathInUnpackedDirectory, currentPath, overwrite: true);

                return;
            }

            uint currentFileOffset;
            // We can't write to buffers of size 0, though there may be empty files.
            int currentFileSize = currentBranch.size == 0 ? 1 : currentBranch.size;

            using (FileStream fileStream = File.Create(currentPath, bufferSize: currentFileSize))
            {
                if (currentBranch.size == 0) return;

                byte[] fileDataBuffer = new byte[currentFileSize];

                bool hasConvertedFileOffset = uint.TryParse(currentBranch.offset.ToString(), out currentFileOffset);
                if (!hasConvertedFileOffset) throw new FormatException(AsarExceptions.failedTypeConversion);

                uint bytesToSkipForFileRead = HeaderSizePickleObjectSize + _headerSize + currentFileOffset;

                _archiveFileStream.Seek(offset: bytesToSkipForFileRead, SeekOrigin.Begin);
                _archiveFileStream.Read(fileDataBuffer, offset: 0, count: currentFileSize);

                fileStream.Write(fileDataBuffer);
            };
        }

        /**
         * <summary> 
         * Reads the archive's header size, and the JSON string representing the directory structure, using
         * the Pickle class. 
         * </summary>
         * <returns>
         * A tuple representing the JSON string for the directory structure, and the header size.
         * </returns>
         * <exception cref="InvalidDataException">When the header is improper.</exception>
        */
        private (string, uint) GetArchiveHeaderData()
        {
            // headerJsonStringSize is not the length of the JSON string,
            // but the amount of bytes it takes.
            uint headerJsonStringSize;
            string headerJsonString;
            _archiveFileStream = File.Open(_archiveFilePath, FileMode.Open);

            byte[] headerSizeBuffer = new byte[8];
            {
                int firstBytesReadCount = _archiveFileStream.Read(headerSizeBuffer, offset: 0, count: 8);
                if (firstBytesReadCount != 8) throw new InvalidDataException(AsarExceptions.insufficientHeaderBytes);
            }


            headerJsonStringSize = new Pickle(buffer: headerSizeBuffer).CreateIterator().ReadUInt32();

            byte[] headerJsonStringBuffer = new byte[headerJsonStringSize];
            {

                int bytesOfHeader = _archiveFileStream.Read(headerJsonStringBuffer, offset: 0, count: (int)headerJsonStringSize);
                if (bytesOfHeader != headerJsonStringSize) throw new InvalidDataException(AsarExceptions.insufficientHeaderBytes);
            }

            headerJsonString = new Pickle(buffer: headerJsonStringBuffer).CreateIterator().ReadString();

            return (headerJsonString, headerJsonStringSize);
        }

        private bool _isDisposed = false;
        /**
         * <summary>Disposes the resources associated with this extractor.</summary>
         */
        public void Dispose()
        {
            Dispose(isCalledFromDispose: true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool isCalledFromDispose)
        {
            if (_isDisposed) return;

            if (isCalledFromDispose)
            {
                _archiveFileStream?.Dispose();
                _archiveFileStream = null;
            }

            _archiveData = null;
            _isDisposed = true;
        }

        /**
         * <summary>Finalizer invokes Dispose.</summary>
         */
        ~AsarExtractor() => Dispose(isCalledFromDispose: false);
    }
}
