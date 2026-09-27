using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace TwinPix
{
    /// <summary>
    /// Computes the <see cref="Fingerprint"/> of an image, and compares two.
    /// Nothing here touches the user interface, so it runs on any thread -
    /// the scanner calls it on every processor core at once.
    /// </summary>
    public static class ImageHash
    {
        /// <summary>
        /// Bumped whenever the way a fingerprint is computed or stored changes,
        /// so an old cache is dropped instead of being compared with new values.
        /// 3: frame count added, cache keyed on UTC modification times.
        /// </summary>
        public const int Version = 3;

        /// <summary>Working resolution: every image is first reduced to 32 x 32 grey levels.</summary>
        private const int Side = 32;

        /// <summary>Side of the finer comparison grid: 11 x 11 = 121 points.</summary>
        public const int FineSide = 11;
        public const int FineLength = FineSide * FineSide;

        /// <summary>Side of the coarse comparison grid: 8 x 8 = 64 points.</summary>
        public const int CoarseLength = 8 * 8;

        /// <summary>Below this standard deviation of the grey levels, an image is too plain to compare.</summary>
        private const double FlatThreshold = 6.0;

        /// <summary>
        /// The fingerprint of one image, or null when it cannot be read.
        /// </summary>
        public static Fingerprint Compute(string path)
        {
            int width, height, frames;
            byte[] grey = Decode(path, out width, out height, out frames);
            if (grey == null) return null;

            var fp = new Fingerprint();
            fp.PixelWidth = width;
            fp.PixelHeight = height;
            fp.Frames = frames;
            fp.Grid = Resample(grey, Side, Side, 8, 8);
            fp.FineGrid = Resample(grey, Side, Side, FineSide, FineSide);
            fp.DHash = DHash(Resample(grey, Side, Side, 9, 8));
            fp.PHash = PHash(grey);
            fp.Flat = StdDev(grey) < FlatThreshold;
            return fp;
        }

        // ---------------- decoding ------------------------------------

        /// <summary>
        /// A 32x32 grey thumbnail, the real pixel size of the image and the
        /// number of pictures in the file. WIC (when compiled in) asks the JPEG
        /// decoder for a scaled-down image, which stops at 1/8 resolution in
        /// the DCT domain instead of unpacking millions of pixels. GDI+ has no
        /// such thing and decodes everything, so it is only the fallback.
        /// </summary>
        /// <remarks>
        /// PHP note: "out" parameters are extra return values - the caller
        /// writes "out width" and gets the value back, like a PHP &amp;$width
        /// reference that the method must assign.
        /// Decoders throw every kind of exception on a damaged or unusual file
        /// (GDI+ even reports an unknown format as OutOfMemoryException), so a
        /// failure here simply means "cannot be fingerprinted".
        /// </remarks>
        private static byte[] Decode(string path, out int width, out int height, out int frames)
        {
            width = height = 0;
            frames = 1;
#if WIC
            try
            {
                byte[] viaWic = DecodeWic(path, out width, out height, out frames);
                if (viaWic != null) return viaWic;
            }
            catch { }       // fall back to GDI+
#endif
            try { return DecodeGdi(path, out width, out height, out frames); }
            catch { return null; }
        }

#if WIC
        /// <summary>Decoded width asked of WIC: a JPEG then stops at 1/8.</summary>
        private const int WicWidth = 128;

        /// <summary>
        /// Scaled decoding through the Windows Imaging Component. The header is
        /// read first, for the image's real size - which the keep rules need,
        /// and which the scaled-down bitmap no longer knows - then the pixels
        /// are decoded straight from the same open file. A stream is used
        /// rather than a URI so that a '#' or a '%' in a file name cannot be
        /// mistaken for URI syntax.
        /// </summary>
        private static byte[] DecodeWic(string path, out int width, out int height, out int frames)
        {
            width = height = 0;
            frames = 1;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536))
            {
                var dec = System.Windows.Media.Imaging.BitmapDecoder.Create(fs,
                    System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation
                    | System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreColorProfile,
                    System.Windows.Media.Imaging.BitmapCacheOption.None);
                if (dec.Frames.Count == 0) return null;
                frames = dec.Frames.Count;
                width = dec.Frames[0].PixelWidth;
                height = dec.Frames[0].PixelHeight;
                if (width <= 0 || height <= 0) return null;

                fs.Position = 0;
                var src = new System.Windows.Media.Imaging.BitmapImage();
                src.BeginInit();
                src.StreamSource = fs;
                src.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                src.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreColorProfile;
                if (width > WicWidth) src.DecodePixelWidth = WicWidth;
                src.EndInit();
                src.Freeze();

                var grey = new System.Windows.Media.Imaging.FormatConvertedBitmap();
                grey.BeginInit();
                grey.Source = src;
                grey.DestinationFormat = System.Windows.Media.PixelFormats.Gray8;
                grey.EndInit();
                grey.Freeze();

                int gw = grey.PixelWidth, gh = grey.PixelHeight;
                if (gw <= 0 || gh <= 0) return null;
                var buffer = new byte[gw * gh];
                grey.CopyPixels(buffer, gw, 0);
                return Resample(buffer, gw, gh, Side, Side);
            }
        }
#endif

        /// <summary>Full decoding through GDI+: correct everywhere, slower.</summary>
        private static byte[] DecodeGdi(string path, out int width, out int height, out int frames)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536))
            using (var img = Image.FromStream(fs, false, false))
            {
                width = img.Width;
                height = img.Height;
                frames = CountFrames(img);
                if (width <= 0 || height <= 0) return null;

                using (var small = new Bitmap(Side, Side, PixelFormat.Format24bppRgb))
                {
                    using (var g = Graphics.FromImage(small))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.DrawImage(img, 0, 0, Side, Side);
                    }

                    BitmapData data = small.LockBits(new Rectangle(0, 0, Side, Side),
                                                     ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                    try
                    {
                        var grey = new byte[Side * Side];
                        var row = new byte[data.Stride];
                        for (int y = 0; y < Side; y++)
                        {
                            Marshal.Copy((IntPtr)(data.Scan0.ToInt64() + (long)y * data.Stride),
                                         row, 0, data.Stride);
                            for (int x = 0; x < Side; x++)
                            {
                                int i = x * 3;              // bytes are in B, G, R order
                                // Rec. 601 luma, in integers
                                grey[y * Side + x] = (byte)((row[i + 2] * 77
                                                           + row[i + 1] * 150
                                                           + row[i] * 29) >> 8);
                            }
                        }
                        return grey;
                    }
                    finally { small.UnlockBits(data); }
                }
            }
        }

        /// <summary>Pictures in the file: pages of a TIFF, frames of a GIF.</summary>
        private static int CountFrames(Image img)
        {
            int frames = 1;
            try
            {
                foreach (Guid dimension in img.FrameDimensionsList)
                {
                    int n = img.GetFrameCount(new FrameDimension(dimension));
                    if (n > frames) frames = n;
                }
            }
            catch { }       // a decoder that cannot count holds one picture
            return frames;
        }

        // ---------------- signal ---------------------------------------

        /// <summary>
        /// Box resampling to any size, fractional edges included. Both hashes
        /// and the comparison grids go through it, so every path ends up
        /// comparing grids built exactly the same way.
        /// </summary>
        public static byte[] Resample(byte[] src, int sw, int sh, int dw, int dh)
        {
            var dst = new byte[dw * dh];
            double fx = (double)sw / dw, fy = (double)sh / dh;
            for (int y = 0; y < dh; y++)
            {
                int y0 = (int)(y * fy), y1 = (int)Math.Ceiling((y + 1) * fy);
                if (y1 > sh) y1 = sh;
                if (y1 <= y0) y1 = y0 + 1;
                for (int x = 0; x < dw; x++)
                {
                    int x0 = (int)(x * fx), x1 = (int)Math.Ceiling((x + 1) * fx);
                    if (x1 > sw) x1 = sw;
                    if (x1 <= x0) x1 = x0 + 1;

                    int sum = 0, n = 0;
                    for (int yy = y0; yy < y1; yy++)
                    {
                        int rowBase = yy * sw;
                        for (int xx = x0; xx < x1; xx++) { sum += src[rowBase + xx]; n++; }
                    }
                    dst[y * dw + x] = (byte)(n > 0 ? sum / n : 0);
                }
            }
            return dst;
        }

        /// <summary>One bit per pair of horizontal neighbours on a 9x8 grid.</summary>
        private static ulong DHash(byte[] g)
        {
            ulong bits = 0;
            int bit = 0;
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++, bit++)
                    if (g[y * 9 + x] > g[y * 9 + x + 1]) bits |= 1UL << bit;
            return bits;
        }

        /// <summary>Cosine table of the 32-point DCT-II, only the 8 coefficients kept.</summary>
        private static readonly double[] Cos = BuildCos();

        private static double[] BuildCos()
        {
            var t = new double[8 * Side];
            for (int u = 0; u < 8; u++)
                for (int x = 0; x < Side; x++)
                    t[u * Side + x] = Math.Cos((2 * x + 1) * u * Math.PI / (2.0 * Side));
            return t;
        }

        /// <summary>
        /// The 8x8 low-frequency corner of a 32x32 DCT, thresholded at its
        /// median. Only the coefficients that are kept are worked out - the
        /// separable transform costs about ten thousand operations, nothing
        /// next to decoding the image.
        /// </summary>
        private static ulong PHash(byte[] g)
        {
            var rows = new double[Side * 8];          // DCT along x, 8 columns kept
            for (int y = 0; y < Side; y++)
            {
                int b = y * Side;
                for (int u = 0; u < 8; u++)
                {
                    double s = 0;
                    int c = u * Side;
                    for (int x = 0; x < Side; x++) s += g[b + x] * Cos[c + x];
                    rows[y * 8 + u] = s;
                }
            }

            var block = new double[64];
            for (int u = 0; u < 8; u++)
            {
                for (int v = 0; v < 8; v++)
                {
                    double s = 0;
                    int c = v * Side;
                    for (int y = 0; y < Side; y++) s += rows[y * 8 + u] * Cos[c + y];
                    block[v * 8 + u] = s;
                }
            }

            // The DC term carries the average brightness, which says nothing
            // about the picture, so it is left out of the median.
            var sorted = new double[63];
            Array.Copy(block, 1, sorted, 0, 63);
            Array.Sort(sorted);
            double median = (sorted[30] + sorted[31]) / 2.0;

            ulong bits = 0;
            for (int i = 0; i < 64; i++)
                if (block[i] > median) bits |= 1UL << i;
            return bits;
        }

        private static double StdDev(byte[] g)
        {
            double sum = 0, sum2 = 0;
            for (int i = 0; i < g.Length; i++) { sum += g[i]; sum2 += (double)g[i] * g[i]; }
            double mean = sum / g.Length;
            double variance = sum2 / g.Length - mean * mean;
            return variance > 0 ? Math.Sqrt(variance) : 0;
        }

        // ---------------- comparing -------------------------------------

        /// <summary>Number of differing bits, the .NET 4 way (no POPCNT intrinsic).</summary>
        public static int Distance(ulong a, ulong b)
        {
            ulong v = a ^ b;
            v = v - ((v >> 1) & 0x5555555555555555UL);
            v = (v & 0x3333333333333333UL) + ((v >> 2) & 0x3333333333333333UL);
            v = (v + (v >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((v * 0x0101010101010101UL) >> 56);
        }

        /// <summary>
        /// Mean absolute difference between two grids, each shifted to its own
        /// average first, so that the same picture exported darker or lighter
        /// still matches while two different flat images do not.
        /// </summary>
        public static double GridDifference(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length || a.Length == 0) return 255;
            int sa = 0, sb = 0;
            for (int i = 0; i < a.Length; i++) { sa += a[i]; sb += b[i]; }
            double ma = (double)sa / a.Length, mb = (double)sb / b.Length;
            double sum = 0;
            for (int i = 0; i < a.Length; i++) sum += Math.Abs((a[i] - ma) - (b[i] - mb));
            return sum / a.Length;
        }
    }
}
