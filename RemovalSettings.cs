namespace TwinPix
{
    /// <summary>Where the duplicates go, as chosen at the bottom of the window.</summary>
    public class RemovalSettings
    {
        /// <summary>Send them to the Recycle Bin rather than to a folder.</summary>
        public bool ToRecycleBin { get; set; }

        /// <summary>Folder mode: the destination, as a full path.</summary>
        public string Destination { get; set; }

        /// <summary>Folder mode: recreate each file's subfolders inside the destination.</summary>
        public bool KeepFolderStructure { get; set; }

        /// <summary>The folder that was scanned, as a full path: the base of the subfolders recreated.</summary>
        public string ScanRoot { get; set; }
    }
}
