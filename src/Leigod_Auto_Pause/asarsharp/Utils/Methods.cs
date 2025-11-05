using System;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace AsarSharp.Utils
{
    class Methods
    {
        [Conditional("DEBUG")]
        public static void DebugLog(string text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("[DEBUG]:");
            Console.ResetColor();
            Console.Write($" {text}\n");
        }

        /**
         * <summary>Checks to see if given file is an executable.</summary>
         * <param name="pathToFile">The path to the file.</param>
         * <returns>True if the file is an executable, and false otherwise.</returns>
         */
        public static bool IsFileExecutable(string pathToFile)
        {
            byte[] firstTwoBytes = new byte[2];
            using FileStream fileStream = File.Open(pathToFile, FileMode.Open);
            fileStream.Read(firstTwoBytes, offset: 0, count: 2);

            // As it turns out, the first 2 bytes of an executable in UTF8 encoding
            // represent "MZ". Uh... ok?
            return Encoding.UTF8.GetString(firstTwoBytes) == "MZ";
        }

        // Inspiration for this method: MSDN
        public static void CopyDirectory(string directoryToCopy, string whereToCopyDirectory)
        {
            Directory.CreateDirectory(whereToCopyDirectory);
         
            foreach (string filePath in Directory.EnumerateFiles(directoryToCopy))
            {
                string filePathInNewDirectory = Path.Combine(whereToCopyDirectory, Path.GetFileName(filePath));
                File.Copy(sourceFileName: filePath, destFileName: filePathInNewDirectory, overwrite: true);
            }

            foreach (string directoryPath in Directory.EnumerateDirectories(directoryToCopy))
            {
                string dirPathInNewDirectory = Path.Combine(whereToCopyDirectory, Path.GetFileName(directoryPath));
                CopyDirectory(directoryPath, dirPathInNewDirectory);    
            }
                        
        }
    }
}

