using System;
using System.Collections.Generic;
using System.IO;

using Newtonsoft.Json;
using DotNet.Globbing;

using static AsarSharp.Utils.Types;
using static AsarSharp.Utils.Constants;
using static AsarSharp.Utils.Methods;

namespace AsarSharp
{

    /**
     * <summary>
     * Hosts all the needed methods, properties and fields for extracting an archive.
     * </summary>
     */
    public sealed class AsarArchiver : IDisposable
    {
        private readonly string _directoryPath;
        private readonly string _archivePath;
        // Temporary file whose use is to store the contents of the directory that is to be archived.
        // The contents of this temp file will be written to the main .asar file after the header has
        // been written.
        private FileStream _tempArchiveFileStream = null;

        private FileStream _newArchiveFileStream = null;

        // The offset at which every file in the archive is found. It's part of the JSON directory structure.
        // This gets incremented by the size of each file read.
        private ulong _fileOffset = 0;

        // The data structure that will hold the JSON representation of the directory structure.
        // It is initialised with a "files" key pointing to another empty dictionary that is to be filled
        // with the files/directories directly inside the main directory.
        private Dictionary<string, dynamic> _jsonDirectoryStructure =
            new() { ["files"] = new Dictionary<string, dynamic>() };

        // Path of {archiveName.asar.unpacked}.
        private string _unpackedDirectoryPath = null;
        // List of globs for including files in the {archiveName.asar.unpacked} directory.
        private readonly List<Glob> _unpackedGlobs = new(capacity: 2);

        /**
         * <param name="directoryPath">Path of the directory that is to be archived.</param>
         * <param name="archivePath">Path to where the archive will be created.</param>
         * <exception cref="DirectoryNotFoundException">
         * Is thrown when the given path to the directory that holds the data to form the archive, doesn't exist.
         * </exception>
         * <exception cref="FormatException">
         * Is thrown when the given path to the archive is not going to be an .asar file.
         * </exception>
         * 
         */
        public AsarArchiver(string directoryPath, string archivePath)
        {
            if (!archivePath.EndsWith(".asar")) throw new FormatException(AsarExceptions.invalidArchiveFilePath);
            if (!Directory.Exists(directoryPath)) throw new DirectoryNotFoundException(AsarExceptions.archiveDirectoryMissing);

            _directoryPath = directoryPath;
            _archivePath = archivePath;
        }

        /**
         * <summary>
         * Packs the given directory from the constructor into an .asar file. 
         * </summary>
         * <param name="archivingOptions">
         * A list of options that describe some rules for the archiving process.
         * </param>
         */
        public void Archive(ArchivingOptions archivingOptions = null)
        {
            if (archivingOptions != null) HandleArchivingOptions(ref archivingOptions);

            DebugLog(GetStep(ArchivingStep.TemporaryFileCreation));
            _tempArchiveFileStream = CreateArchiveDataTempFile();
            DebugLog($"Temporary file created with name {Path.GetFileName(_tempArchiveFileStream.Name)}");

            DebugLog(GetStep(ArchivingStep.DirectoryStructureCreation));
            // startBranch represents the empty dictionary that will hold the files/directories 
            // directly inside the main directory. Basically, the initial "files": {}
            Dictionary<string, dynamic> startBranch = _jsonDirectoryStructure.GetValueOrDefault("files");
            MakeJsonDirStructureAndWriteToTempFile(_directoryPath, ref startBranch, isFirstDirectory: true);

            DebugLog(GetStep(ArchivingStep.ArchiveCreation));
            _newArchiveFileStream = File.Create(_archivePath);

            DebugLog(GetStep(ArchivingStep.ArchiveHeaderWrite));
            WriteArchiveHeader();

            DebugLog(GetStep(ArchivingStep.ArchiveContentsWrite));
            WriteArchiveContents();

            DebugLog($"Archive created at {_newArchiveFileStream.Name}");
        }

        private void HandleArchivingOptions(ref ArchivingOptions archivingOptions)
        {
            if (archivingOptions.Unpack != null)
            {
                foreach (string unpackedGlob in archivingOptions.Unpack)
                    _unpackedGlobs.Add(Glob.Parse($"{(archivingOptions.MatchBasename ? "**/" : "")}{unpackedGlob}"));

                _unpackedDirectoryPath = _archivePath + ".unpacked";

                DebugLog(GetStep(ArchivingStep.UnpackedDirectoryCreation));
                Directory.CreateDirectory(_unpackedDirectoryPath);
            }

            // TODO: implement --unpack-dir
        }

        /**
         * <summary>
         * Creates a new temporary file used for holding the data of every file inside the directory. 
         * Precisely, it stores the bytes of file 1, then right after, the bytes of file 2 and so on.
         * </summary>
         * <returns>The FileStream of the temporary file.</returns>
         */
        private static FileStream CreateArchiveDataTempFile()
        {
            string pathToTempFile = Path.GetTempFileName();

            FileInfo tempFileInfo = new(pathToTempFile);
            tempFileInfo.Attributes = FileAttributes.Temporary;

            return tempFileInfo.Open(FileMode.Open);
        }

        /**
        * <summary>
        * Recursively creates, in memory, the structure of the files and directories from the main 
        * directory. The structure created follows the same format as the one from asar's docs.
        * </summary>
        * <param name="currentFilePath">The path of the file that is to be added to the archive.</param>
        * <param name="currentBranch">
        * The branch of the directory structure that needs to be populated. It can represent the "files"
        * property, or the file names with their size and offset. 
        * </param>
        * <param name="isFirstDirectory">
        * Is used to treat the edge case of the main directory. The format of the JSON simply starts with
        * the files property, at the root, and doesn't include the name of the main directory. This 
        * parameter, for now, may not be set to anything, when calling the method. Otherwise, the outputted
        * structure will not be correct.
        * </param>
        */
        private void MakeJsonDirStructureAndWriteToTempFile(
          string currentFilePath,
          ref Dictionary<string, dynamic> currentBranch,
          bool isFirstDirectory = false
        )
        {
            // TODO: think about how to not... have to... do this check, lol.
            if (isFirstDirectory)
            {
                foreach (string fileOrDirectoryPath in Directory.EnumerateFileSystemEntries(currentFilePath, searchPattern: "*.*", SearchOption.TopDirectoryOnly))
                    MakeJsonDirStructureAndWriteToTempFile(fileOrDirectoryPath, ref currentBranch);
                return;
            }

            if (Directory.Exists(currentFilePath))
            {
                // Creates a new branch in the directory structure, that corresponds to the format of a 
                // directory, like: "directory name": { "files": {} }
                string directoryName = Path.GetFileName(currentFilePath);
                Dictionary<string, dynamic> filesDataInNewFilesBranch = new();
                Dictionary<string, dynamic> newFilesBranch = new() { ["files"] = filesDataInNewFilesBranch };

                currentBranch.Add(directoryName, newFilesBranch);

                foreach (string fileOrDirectoryPath in Directory.EnumerateFileSystemEntries(currentFilePath, searchPattern: "*.*", SearchOption.TopDirectoryOnly))
                    MakeJsonDirStructureAndWriteToTempFile(fileOrDirectoryPath, ref filesDataInNewFilesBranch);

                return;
            }


            WriteFileBytesToTempFile(ref currentFilePath);
            AddNewFileToBranch(ref currentBranch, currentFilePath);
        }

        /**
         * <summary>Writes the header size and the JSON directory structure to the archive.</summary>
         */
        private void WriteArchiveHeader()
        {
            string headerJsonDirectoryStructure = JsonConvert.SerializeObject(_jsonDirectoryStructure);

            // Serializes the JSON directory structure.
            Pickle headerJsonDirStructurePickle = new();
            headerJsonDirStructurePickle.WriteString(headerJsonDirectoryStructure);
            byte[] headerJsonDirStructureBuffer = headerJsonDirStructurePickle.ToBuffer();

            // Serializes the size of the JSON, which is the length of the JSON string. The header format
            // of asar says that the size should be an unsigned, 32-bit integer.
            Pickle headerSizePickle = new();
            headerSizePickle.WriteUInt32((uint)headerJsonDirStructureBuffer.Length);
            byte[] headerSizeBuffer = headerSizePickle.ToBuffer();

            _newArchiveFileStream.Write(headerSizeBuffer);
            _newArchiveFileStream.Write(headerJsonDirStructureBuffer);
        }

        /**
         * <summary>
         * Concatenates to the archive file the bytes of the files inside the directory that is
         * to be archived.
         * </summary>
         */
        private void WriteArchiveContents()
        {
            _tempArchiveFileStream.Seek(0, SeekOrigin.Begin);
            _tempArchiveFileStream.CopyTo(_newArchiveFileStream);
        }

        private void WriteFileBytesToTempFile(ref string filePath)
        {
            _tempArchiveFileStream.Seek(offset: (long)_fileOffset, SeekOrigin.Begin);
            _tempArchiveFileStream.Write(File.ReadAllBytes(filePath));
        }

        /**
         * <summary>Creates the current file's "size", "offset", and "executable" properties, and computes them.</summary> 
         * <param name="currentBranch">The point inside the directory structure where the current file is located.</param>
         * <param name="currentFilePath">The path of the file whose size and offset are to be computed.</param>
         */
        private void AddNewFileToBranch(
            ref Dictionary<string, dynamic> currentBranch,
            string currentFilePath
        )
        {
            string currentFileName = Path.GetFileName(currentFilePath);
            var (currentFileSize, currentFileOffset) = GetFileSizeAndOffset(currentFilePath);
            Dictionary<string, dynamic> currentFileData = new() { ["size"] = currentFileSize };

            if (!_unpackedGlobs.Exists((glob) => glob.IsMatch(currentFilePath)))
            {
                currentFileData.Add("offset", currentFileOffset);
                _fileOffset += (ulong)currentFileSize;
            }
            else
            {
                currentFileData.Add("unpacked", true);
                AddFileToUnpackedDirectory(currentFilePath);
            }
            if (IsFileExecutable(currentFilePath)) currentFileData.Add("executable", true);

            currentBranch.Add(currentFileName, currentFileData);
        }

        /**
         * <summary>
         * Adds the files excluded by the globs from the archiving options to the {archiveName.asar.unpacked} directory.
         * </summary>
         */
        private void AddFileToUnpackedDirectory(string filePath)
        {
            /*
             _unpackedDirectoryPath = path/to/{archiveName.asar.unpacked}
             _directoryPath = path/to/directoryToArchive
             filePath = {_directoryPath}/path/to/file
            
             filePathInUnpackedDirectory = _unpackedDirectoryPath + (filePath - _directoryPath)
            */
            string filePathInUnpackedDirectory = Path.Join(_unpackedDirectoryPath, filePath.Substring(_directoryPath.Length + 1));
            string fileParentPathInUnpackedDirectory = Directory.GetParent(filePathInUnpackedDirectory).FullName;

            Directory.CreateDirectory(fileParentPathInUnpackedDirectory);
            File.Copy(filePath, filePathInUnpackedDirectory, overwrite: true);
        }

        private (long, string) GetFileSizeAndOffset(string filePath)
        {
            FileInfo fileInfo = new(filePath);

            long fileSize = fileInfo.Length;
            string currentFileOffset = _fileOffset.ToString();

            return (fileSize, currentFileOffset);
        }

        private bool _isDisposed = false;
        /**
         * <summary>Disposes the resources associated with this archiver.</summary>
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
                if (_tempArchiveFileStream != null)
                {
                    _tempArchiveFileStream.Dispose();
                    try
                    {
                        File.Delete(_tempArchiveFileStream.Name);
                        DebugLog($"Deleted temp file {_tempArchiveFileStream.Name}");
                    }
                    catch (Exception error)
                    {
                        DebugLog($"The following error occured whilst trying to delete the temp file: {error}");
                    }

                    _tempArchiveFileStream = null;
                }

                _newArchiveFileStream?.Dispose();
                _newArchiveFileStream = null;
            }

            _unpackedGlobs.Clear();
            _jsonDirectoryStructure = null;
            _isDisposed = true;
        }

        /**
         * <summary> Finalizer invokes Dispose.</summary>
         */
        ~AsarArchiver() => Dispose(isCalledFromDispose: false);
    }
}