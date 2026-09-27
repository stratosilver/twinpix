using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TwinPix
{
    /// <summary>
    /// The folders remembered by each folder field, and a few single values
    /// (the window placement), kept between runs in
    /// %APPDATA%\TwinPix\folders.txt - one "key|value" line each.
    /// </summary>
    public class FolderHistory
    {
        public const int MaxEntries = 12;

        private readonly Dictionary<string, List<string>> _map =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        public static string FilePath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TwinPix");
                return Path.Combine(dir, "folders.txt");
            }
        }

        /// <summary>The list of one field, most recent first; created empty on first use.</summary>
        public List<string> Get(string key)
        {
            List<string> list;
            if (!_map.TryGetValue(key, out list))
            {
                list = new List<string>();
                _map[key] = list;
            }
            return list;
        }

        /// <summary>Puts a folder at the top of its list, without duplicates.</summary>
        public void Add(string key, string path)
        {
            if (path == null) return;
            path = path.Trim().TrimEnd('\\', '/');   // "C:\Photos\" and "C:/Photos" are one entry
            if (path.Length == 0) return;
            // a drive root keeps its separator: "C:" alone is not "C:\"
            if (path.Length == 2 && path[1] == ':') path += Path.DirectorySeparatorChar;

            List<string> list = Get(key);
            list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, path);
            if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        }

        public void Clear(string key) { Get(key).Clear(); }

        /// <summary>
        /// Single-value entries stored in the same file, verbatim: the window
        /// placement uses one. Unlike Add(), nothing is normalized here.
        /// </summary>
        public void SetValue(string key, string value)
        {
            List<string> list = Get(key);
            list.Clear();
            if (!string.IsNullOrEmpty(value)) list.Add(value);
        }

        public string GetValue(string key)
        {
            List<string> list = Get(key);
            return list.Count > 0 ? list[0] : null;
        }

        /// <summary>Reads the file. A missing or unreadable one simply means no history.</summary>
        public void Load()
        {
            _map.Clear();
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int sep = line.IndexOf('|');
                    if (sep <= 0) continue;
                    string key = line.Substring(0, sep);
                    string value = line.Substring(sep + 1).Trim();
                    if (value.Length == 0) continue;
                    List<string> list = Get(key);
                    if (list.Count < MaxEntries) list.Add(value);
                }
            }
            catch (Exception ex)
            {
                if (!PathHelper.IsFileSystemError(ex)) throw;
            }
        }

        /// <summary>Writes the file. Losing the history is not worth an error message.</summary>
        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var sb = new StringBuilder();
                foreach (KeyValuePair<string, List<string>> kv in _map)
                    foreach (string value in kv.Value)
                        sb.AppendLine(kv.Key + "|" + value);
                File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                if (!PathHelper.IsFileSystemError(ex)) throw;
            }
        }
    }
}
