using System;
using System.IO;
using System.Security;

namespace TwinPix
{
    /// <summary>
    /// Path comparisons done the way Windows sees paths: case-insensitive,
    /// on full paths, with "C:\Photos" and "C:\Photos\" being one folder -
    /// and "C:\Photos2" not being inside "C:\Photos".
    /// </summary>
    /// <remarks>
    /// Two traps these helpers exist to avoid, both relevant to not losing files:
    /// <list type="bullet">
    /// <item>Path.Combine(a, b) returns b alone when b is itself a full path:
    ///   Path.Combine(@"D:\Quarantine", @"C:\Photos\a.jpg") is "C:\Photos\a.jpg".
    ///   Anything built with it here is checked to still be inside its folder.</item>
    /// <item>Path.GetFullPath() resolves a relative path against the current
    ///   directory, which the user never sees; "C:" alone even means "the
    ///   current directory of drive C". Folders typed by the user must
    ///   therefore be absolute (<see cref="IsAbsolute"/>).</item>
    /// </list>
    /// </remarks>
    public static class PathHelper
    {
        private static readonly char[] Separators = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };

        /// <summary>Most " (n)" suffixes tried by <see cref="FreeFileName"/>.</summary>
        private const int MaxSuffix = 9999;

        /// <summary>
        /// True for the exceptions the file system throws on an ordinary
        /// failure - missing file, access denied, file in use, malformed or too
        /// long path - as opposed to a bug in the program. Used in place of
        /// C# 6 exception filters, which the Windows compiler does not know:
        /// <code>catch (Exception ex) { if (!PathHelper.IsFileSystemError(ex)) throw; ... }</code>
        /// PHP note: "throw;" on its own re-throws the exception being handled,
        /// keeping its original stack trace.
        /// </summary>
        public static bool IsFileSystemError(Exception ex)
        {
            return ex is IOException                    // includes FileNotFound, DirectoryNotFound, PathTooLong
                || ex is UnauthorizedAccessException
                || ex is SecurityException
                || ex is ArgumentException              // illegal characters in a path
                || ex is NotSupportedException;         // "C:\a:b" and other unsupported formats
        }

        /// <summary>
        /// True for a path that does not depend on the current directory:
        /// "C:\..." or a network path "\\server\share\...".
        /// </summary>
        public static bool IsAbsolute(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (Path.DirectorySeparatorChar == '/') return path[0] == '/';   // Mono, for the tests
            if (path.StartsWith(@"\\", StringComparison.Ordinal)) return true;
            return path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':'
                && (path[2] == '\\' || path[2] == '/');
        }

        /// <summary>The full form of a path, or null when it cannot be resolved.</summary>
        public static string FullPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            try { return Path.GetFullPath(path); }
            catch (Exception ex)
            {
                if (!IsFileSystemError(ex)) throw;
                return null;
            }
        }

        /// <summary>The full form of a path, or the path as given when it cannot be resolved.</summary>
        public static string FullPathOrSelf(string path)
        {
            return FullPath(path) ?? (path ?? "");
        }

        /// <summary>
        /// Full path of a folder without its trailing separator - except for a
        /// drive root, whose separator is part of it ("C:\", not "C:").
        /// </summary>
        public static string NormalizeFolder(string path)
        {
            string full = FullPathOrSelf(path);
            string trimmed = full.TrimEnd(Separators);
            if (trimmed.Length == 0) return full;                       // "/" on Mono
            if (trimmed.Length == 2 && trimmed[1] == ':') return trimmed + Path.DirectorySeparatorChar;
            return trimmed;
        }

        /// <summary>True when <paramref name="path"/> is <paramref name="folder"/> itself or anything below it.</summary>
        public static bool IsSameOrUnder(string path, string folder)
        {
            string p = FullPath(path), f = FullPath(folder);
            if (p == null || f == null) return false;
            return WithSeparator(p).StartsWith(WithSeparator(f), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True when <paramref name="path"/> is below <paramref name="folder"/>, not the folder itself.</summary>
        public static bool IsStrictlyUnder(string path, string folder)
        {
            return IsSameOrUnder(path, folder) && !IsSameFolder(path, folder);
        }

        /// <summary>True when both paths name the same folder, whatever their case or trailing separator.</summary>
        public static bool IsSameFolder(string a, string b)
        {
            string fa = FullPath(a), fb = FullPath(b);
            if (fa == null || fb == null) return false;
            return string.Equals(WithSeparator(fa), WithSeparator(fb), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Where <paramref name="path"/> sits inside <paramref name="folder"/>:
        /// "2019\Holidays" for "C:\Photos\2019\Holidays" in "C:\Photos", "" for the
        /// folder itself, null when the path is not inside the folder.
        /// </summary>
        public static string RelativePath(string path, string folder)
        {
            string p = FullPath(path), f = FullPath(folder);
            if (p == null || f == null) return null;
            string fs = WithSeparator(f), ps = WithSeparator(p);
            if (!ps.StartsWith(fs, StringComparison.OrdinalIgnoreCase)) return null;
            return ps.Substring(fs.Length).TrimEnd(Separators);
        }

        /// <summary>
        /// A name not yet taken in <paramref name="folder"/>: the file's own
        /// name, else "name (1).jpg", "name (2).jpg" and so on. Null when even
        /// the 9999th is taken. Nothing is ever overwritten: the caller moves
        /// with File.Move, which fails rather than replace an existing file.
        /// </summary>
        public static string FreeFileName(string folder, string fileName)
        {
            string candidate = Path.Combine(folder, fileName);
            if (!Exists(candidate)) return candidate;

            string name = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            for (int i = 1; i <= MaxSuffix; i++)
            {
                candidate = Path.Combine(folder, name + " (" + i + ")" + ext);
                if (!Exists(candidate)) return candidate;
            }
            return null;
        }

        private static bool Exists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        private static string WithSeparator(string fullPath)
        {
            char last = fullPath[fullPath.Length - 1];
            return last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar
                 ? fullPath : fullPath + Path.DirectorySeparatorChar;
        }
    }
}
