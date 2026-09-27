using System;
using System.Collections;
using System.Windows.Forms;

namespace TwinPix
{
    /// <summary>
    /// Sorts the duplicate-group list on the clicked column.
    /// PHP note: implementing IComparer is the C# way of handing a comparison
    /// function to a sort, like the callback of PHP's usort().
    /// </summary>
    public class GroupComparer : IComparer
    {
        // Columns of the group list, in the order of MainForm.Designer.cs.
        public const int ColumnKeptFile = 0;
        public const int ColumnExtension = 1;
        public const int ColumnSize = 2;
        public const int ColumnCopies = 3;
        public const int ColumnReclaimable = 4;
        public const int ColumnKeptIn = 5;

        private readonly int _column;
        private readonly bool _ascending;

        public GroupComparer(int column, bool ascending)
        {
            _column = column;
            _ascending = ascending;
        }

        public int Compare(object x, object y)
        {
            var a = ((ListViewItem)x).Tag as DupGroup;
            var b = ((ListViewItem)y).Tag as DupGroup;
            if (a == null || b == null) return 0;

            int r;
            switch (_column)
            {
                case ColumnExtension: r = string.Compare(a.Extension, b.Extension, StringComparison.OrdinalIgnoreCase); break;
                case ColumnSize: r = a.Size.CompareTo(b.Size); break;
                case ColumnCopies: r = a.Files.Count.CompareTo(b.Files.Count); break;
                case ColumnReclaimable: r = a.Wasted.CompareTo(b.Wasted); break;
                case ColumnKeptIn: r = string.Compare(KeptFolder(a), KeptFolder(b), StringComparison.OrdinalIgnoreCase); break;
                default: r = string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase); break;
            }
            // a stable, predictable order for equal values
            if (r == 0) r = string.Compare(a.Extension, b.Extension, StringComparison.OrdinalIgnoreCase);
            if (r == 0) r = a.Size.CompareTo(b.Size);
            return _ascending ? r : -r;
        }

        private static string KeptFolder(DupGroup g)
        {
            FileEntry k = g.Kept;
            return k == null ? "" : k.DirectoryPath;
        }
    }
}
