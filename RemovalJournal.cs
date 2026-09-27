using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace TwinPix
{
    /// <summary>
    /// A CSV log of every file TwinPix moves or sends to the Recycle Bin:
    /// when, where it was, where it went, and which copy was kept in its
    /// place - so that any move can be traced and undone by hand.
    /// </summary>
    /// <remarks>
    /// Folder mode writes it into the destination folder, next to the files it
    /// describes; Recycle Bin mode into %APPDATA%\TwinPix. Each run appends to
    /// the file, one line per file, flushed at once: a crash in the middle of
    /// a batch still leaves every move made so far on record.
    /// Semicolons separate the fields and the file starts with a UTF-8 mark,
    /// which is what Excel expects in French and Belgian settings.
    /// PHP note: IDisposable + Dispose() is how C# closes a resource
    /// deterministically, like fclose(); "using (var j = ...) { }" calls it
    /// automatically.
    /// </remarks>
    public sealed class RemovalJournal : IDisposable
    {
        public const string FileName = "TwinPix-journal.csv";

        private const string Header = "Date;Action;File;Now at;Kept copy;Why they match";

        private StreamWriter _writer;

        private RemovalJournal(string path, StreamWriter writer)
        {
            Path = path;
            _writer = writer;
        }

        /// <summary>Full path of the journal file.</summary>
        public string Path { get; private set; }

        /// <summary>The journal of the Recycle Bin mode, in the settings folder.</summary>
        public static string RecycleBinJournalPath
        {
            get
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TwinPix");
                return System.IO.Path.Combine(dir, FileName);
            }
        }

        /// <summary>
        /// Opens (or creates) the journal for appending. Throws when it cannot
        /// be written - and then nothing must be moved: a move that leaves no
        /// trace is exactly what the journal is there to prevent.
        /// </summary>
        public static RemovalJournal Open(string path)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            bool isNew = !File.Exists(path) || new FileInfo(path).Length == 0;
            var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            var writer = new StreamWriter(stream, new UTF8Encoding(true));   // mark written on a new file only
            writer.AutoFlush = true;
            if (isNew) writer.WriteLine(Header);
            return new RemovalJournal(path, writer);
        }

        /// <summary>Appends one line. <paramref name="nowAt"/> is the new path, or "Recycle Bin".</summary>
        public void Record(string action, string file, string nowAt, string keptCopy, string evidence)
        {
            if (_writer == null) throw new ObjectDisposedException("RemovalJournal");
            _writer.WriteLine(string.Join(";", new[]
            {
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Csv(action), Csv(file), Csv(nowAt), Csv(keptCopy), Csv(evidence)
            }));
        }

        public void Dispose()
        {
            if (_writer == null) return;
            _writer.Dispose();
            _writer = null;
        }

        /// <summary>Quotes a field when it holds a separator, a quote or a line break.</summary>
        private static string Csv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOfAny(new[] { ';', '"', '\r', '\n' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
    }
}
