using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace TwinPix
{
    /// <summary>
    /// The few visual constants the code needs at run time. Everything else -
    /// sizes, positions, captions - lives in the .Designer.cs files, where
    /// Visual Studio's form designer can edit it.
    /// </summary>
    public static class UiStyle
    {
        /// <summary>The application font, as set in the designer files: Segoe UI 10.5 pt.</summary>
        public static readonly Font UiFont = new Font("Segoe UI", 10.5f, FontStyle.Regular);
        public static readonly Font UiFontBold = new Font(UiFont, FontStyle.Bold);

        /// <summary>Edges of the splitter bands.</summary>
        public static readonly Color BorderGray = Color.FromArgb(214, 214, 214);

        /// <summary>Fill of the splitter bands.</summary>
        public static readonly Color DividerGray = Color.FromArgb(230, 230, 230);

        /// <summary>
        /// The application icon. build.bat embeds assets\twinpix.ico in the
        /// executable (/resource); if it is missing, the icon Windows
        /// associates with the executable is used; failing that, null.
        /// </summary>
        public static Icon AppIcon()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream st = asm.GetManifestResourceStream("TwinPix.twinpix.ico"))
                    if (st != null) return new Icon(st);
            }
            catch { }       // a damaged icon only costs the default one
            try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }
            return null;
        }
    }
}
