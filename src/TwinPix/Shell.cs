using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace TwinPix
{
    /// <summary>Hands a file over to Windows: open it, or show it in Explorer.</summary>
    public static class Shell
    {
        /// <summary>Opens the file with the program Windows associates with it.</summary>
        public static void Open(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex)        // no associated program, file gone, access denied...
            {
                Native.Show(null, "TwinPix", "The file could not be opened", ex.Message,
                            MessageBoxButtons.OK, Native.DialogIcon.Error);
            }
        }

        /// <summary>Opens an Explorer window on the file's folder, with the file selected.</summary>
        public static void ShowInExplorer(string path)
        {
            try { Process.Start("explorer.exe", "/select,\"" + path + "\""); }
            catch (Exception ex)
            {
                Native.Show(null, "TwinPix", "Explorer could not be opened", ex.Message,
                            MessageBoxButtons.OK, Native.DialogIcon.Error);
            }
        }
    }
}
