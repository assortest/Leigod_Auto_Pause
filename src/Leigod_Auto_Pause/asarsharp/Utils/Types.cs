namespace AsarSharp.Utils
{
    /**
     * Various types used within the library.
     */
    public static class Types
    {
        /**
         * Options to be passed to the archive method.
         */
        public class ArchivingOptions
        {
            /**
             * <summary>Match against the file name of the path.</summary>
             */
            public bool MatchBasename { get; init; } = false;

            /**
             * <summary>List of globs for file names to exclude from packing.</summary>
             */
            public string[] Unpack { get; init; } = null;
    
            // public string[] UnpackDir { get; init; } = null;
        }
    }
}
