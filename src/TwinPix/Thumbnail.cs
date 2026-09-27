using System;
using System.Drawing;
using System.IO;

namespace TwinPix
{
    /// <summary>The previews shown on the cards.</summary>
    public static class Thumbnail
    {
        /// <summary>
        /// Decodes an image into a new bitmap no larger than
        /// <paramref name="maxWidth"/> x <paramref name="maxHeight"/>. The file is
        /// read and closed at once, so a preview never keeps it locked - a
        /// locked file could not be moved.
        /// </summary>
        /// <remarks>
        /// The caller owns the bitmap and must Dispose() it.
        /// PHP note: memory is freed by a garbage collector at a time of its
        /// choosing, so objects holding Windows resources (files, bitmaps,
        /// fonts) are released explicitly with Dispose() - or with a using
        /// block, which calls Dispose() at its closing brace, even on an
        /// exception.
        /// Throws when the file cannot be read or decoded.
        /// </remarks>
        public static Image Load(string path, int maxWidth, int maxHeight)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536))
            using (var img = Image.FromStream(fs, false, false))
            {
                double rw = (double)maxWidth / img.Width;
                double rh = (double)maxHeight / img.Height;
                double r = Math.Min(Math.Min(rw, rh), 1.0);         // never enlarged
                int nw = Math.Max(1, (int)(img.Width * r));
                int nh = Math.Max(1, (int)(img.Height * r));
                var bmp = new Bitmap(nw, nh);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(img, 0, 0, nw, nh);
                }
                return bmp;
            }
        }
    }
}
