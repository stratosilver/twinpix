namespace TwinPix
{
    /// <summary>One folder the scan went through, and how many images it holds.</summary>
    public class ScannedFolder
    {
        /// <summary>Full path of the folder.</summary>
        public string Path { get; set; }

        /// <summary>Images found directly in it (not in its subfolders).</summary>
        public int Images { get; set; }
    }
}
