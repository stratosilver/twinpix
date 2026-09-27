// =====================================================================
//  TwinPix - entry point
//
//  Windows starts the program here. Everything else happens in MainForm,
//  the main window; this file only makes sure that an error nobody
//  caught still ends up in front of the user and in a text file.
// =====================================================================

using System;
using System.IO;
using System.Windows.Forms;

namespace TwinPix
{
    /// <summary>The program's entry point and its last-resort error report.</summary>
    public static class Program
    {
        // PHP note: a PHP script starts at its first line; a C# program starts
        // in the one method called Main. [STAThread] is an attribute (the same
        // idea as PHP 8's #[...]): the window, the clipboard and the folder
        // dialogs are COM objects, and COM requires this "single-threaded
        // apartment" mode on the thread that shows them.
        [STAThread]
        public static void Main()
        {
            // Two safety nets for exceptions nobody caught: one for the
            // window's own thread, one for any other thread.
            // PHP note: "+=" on an event registers a callback, much like
            // set_exception_handler(); the lambda "(sender, e) => ..." is the
            // equivalent of PHP's fn($sender, $e) => ...
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                Report(e.ExceptionObject as Exception, "TwinPix - unhandled error");
            Application.ThreadException += (sender, e) =>
                Report(e.Exception, "TwinPix - unexpected error");

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());      // returns when the window closes
            }
            catch (Exception ex)
            {
                Report(ex, "TwinPix could not start");
            }
        }

        /// <summary>
        /// Shows a failure and appends it to %APPDATA%\TwinPix\startup-error.txt,
        /// so a window that never appears still leaves a trace. Deliberately a
        /// plain message box: it must work even when the richer dialog cannot.
        /// </summary>
        private static void Report(Exception ex, string title)
        {
            string text = ex == null ? "Unknown error." : ex.ToString();
            string written = Append("startup-error.txt",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine
                + text + Environment.NewLine + Environment.NewLine);

            string shown = text + Environment.NewLine + Environment.NewLine
                         + (written == null ? "(this report could not be saved to disk)"
                                            : "Saved to: " + written);
            try { MessageBox.Show(shown, title, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            catch { }   // nothing else can be done: the report is the last resort
        }

        /// <summary>
        /// Appends <paramref name="text"/> to the first location that accepts
        /// it: the settings folder, then the folder of the executable, then the
        /// temporary folder. Returns the file written, or null when none worked.
        /// </summary>
        private static string Append(string fileName, string text)
        {
            var candidates = new string[3];
            // Each lookup can fail on a locked-down machine; a failure only
            // means one candidate fewer, so the exceptions are ignored.
            try { candidates[0] = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TwinPix"); }
            catch { }
            try { candidates[1] = Path.GetDirectoryName(Application.ExecutablePath); }
            catch { }
            try { candidates[2] = Path.GetTempPath(); }
            catch { }

            foreach (string folder in candidates)
            {
                if (string.IsNullOrEmpty(folder)) continue;
                try
                {
                    Directory.CreateDirectory(folder);        // does nothing if it exists
                    string path = Path.Combine(folder, fileName);
                    File.AppendAllText(path, text);
                    return path;
                }
                catch { }   // try the next location
            }
            return null;
        }
    }
}
