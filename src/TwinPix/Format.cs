using System.Globalization;

namespace TwinPix
{
    /// <summary>Numbers as the user reads them.</summary>
    public static class Format
    {
        private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB" };

        /// <summary>"512 B", "1.5 MB", "2.34 GB" - in the user's own decimal separator.</summary>
        public static string FileSize(long bytes)
        {
            double b = bytes;
            int i = 0;
            while (b >= 1024 && i < Units.Length - 1) { b /= 1024; i++; }
            return (i == 0 ? b.ToString("0", CultureInfo.CurrentCulture)
                           : b.ToString("0.##", CultureInfo.CurrentCulture)) + " " + Units[i];
        }
    }
}
