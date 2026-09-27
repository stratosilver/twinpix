using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TwinPix
{
    /// <summary>
    /// Thin wrappers over the Win32 features WinForms does not expose: the
    /// Explorer look of the lists, the sort arrows, the grey prompts in empty
    /// fields, the Vista task dialog. Every helper is optional: off Windows,
    /// or where the feature is missing, it quietly does nothing and the
    /// application keeps the framework's default appearance - which is why
    /// most of them swallow their exceptions.
    /// </summary>
    public static class Native
    {
        public static bool IsWindows
        {
            get
            {
                PlatformID p = Environment.OSVersion.Platform;
                return p == PlatformID.Win32NT || p == PlatformID.Win32Windows;
            }
        }

        /// <summary>Gives a control the Explorer look: subtle hover, themed header.</summary>
        public static void UseExplorerTheme(Control c)
        {
            if (!IsWindows || c == null || !c.IsHandleCreated) return;
            try { NativeMethods.SetWindowTheme(c.Handle, "Explorer", null); }
            catch { }
        }

        /// <summary>Native double buffering: no flicker while scrolling.</summary>
        public static void EnableDoubleBuffer(ListView lv)
        {
            if (!IsWindows || lv == null || !lv.IsHandleCreated) return;
            try
            {
                NativeMethods.SendMessage(lv.Handle, NativeMethods.LVM_SETEXTENDEDLISTVIEWSTYLE,
                                          (IntPtr)NativeMethods.LVS_EX_DOUBLEBUFFER,
                                          (IntPtr)NativeMethods.LVS_EX_DOUBLEBUFFER);
            }
            catch { }
        }

        /// <summary>Draws the sort arrow in the column header itself.</summary>
        public static void SetSortArrow(ListView lv, int column, bool ascending)
        {
            if (!IsWindows || lv == null || !lv.IsHandleCreated) return;
            try
            {
                IntPtr header = NativeMethods.SendMessage(lv.Handle, NativeMethods.LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
                if (header == IntPtr.Zero) return;
                for (int i = 0; i < lv.Columns.Count; i++)
                {
                    var item = new NativeMethods.HDITEM();
                    item.mask = NativeMethods.HDI_FORMAT;
                    NativeMethods.SendMessage(header, NativeMethods.HDM_GETITEM, (IntPtr)i, ref item);
                    item.fmt &= ~(NativeMethods.HDF_SORTUP | NativeMethods.HDF_SORTDOWN);
                    if (i == column) item.fmt |= ascending ? NativeMethods.HDF_SORTUP : NativeMethods.HDF_SORTDOWN;
                    NativeMethods.SendMessage(header, NativeMethods.HDM_SETITEM, (IntPtr)i, ref item);
                }
            }
            catch { }
        }

        /// <summary>Grey prompt shown inside an empty field.</summary>
        public static void SetCueBanner(Control c, string text)
        {
            if (!IsWindows || c == null || !c.IsHandleCreated) return;
            try
            {
                if (c is ComboBox) NativeMethods.SendMessage(c.Handle, NativeMethods.CB_SETCUEBANNER, IntPtr.Zero, text);
                else if (c is TextBox) NativeMethods.SendMessage(c.Handle, NativeMethods.EM_SETCUEBANNER, IntPtr.Zero, text);
            }
            catch { }
        }

        /// <summary>
        /// In an editable combo box made taller than its font (ItemHeight),
        /// Windows stretches the text field over the whole height and the text
        /// sits at the top. This shrinks the field to one line and puts it in
        /// the middle. Windows moves it back whenever the box is resized, so
        /// this is called again from SizeChanged.
        /// </summary>
        public static void CenterComboEdit(ComboBox c)
        {
            if (!IsWindows || c == null || !c.IsHandleCreated) return;
            if (c.DropDownStyle != ComboBoxStyle.DropDown) return;
            try
            {
                var info = new NativeMethods.COMBOBOXINFO();
                info.cbSize = Marshal.SizeOf(typeof(NativeMethods.COMBOBOXINFO));
                if (!NativeMethods.GetComboBoxInfo(c.Handle, ref info) || info.hwndItem == IntPtr.Zero) return;
                int area = info.rcItem.Bottom - info.rcItem.Top;
                int line = TextRenderer.MeasureText("Ag", c.Font).Height + 2;
                if (area <= line) return;
                int top = info.rcItem.Top + (area - line) / 2;
                NativeMethods.SetWindowPos(info.hwndItem, IntPtr.Zero, info.rcItem.Left, top,
                                           info.rcItem.Right - info.rcItem.Left, line,
                                           NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
            }
            catch { }
        }

        /// <summary>
        /// Centres the line of a multi-line text box used as a tall one-line
        /// field. The formatting rectangle is reset whenever the box is resized,
        /// so this is called again from its SizeChanged.
        /// </summary>
        public static void CenterSingleLine(TextBox t)
        {
            if (!IsWindows || t == null || !t.IsHandleCreated || !t.Multiline) return;
            try
            {
                int line = TextRenderer.MeasureText("Ag", t.Font).Height;
                var r = new NativeMethods.RECT();
                r.Left = 3;
                r.Right = Math.Max(r.Left + 1, t.ClientSize.Width - 3);
                r.Top = Math.Max(0, (t.ClientSize.Height - line) / 2);
                r.Bottom = t.ClientSize.Height;
                NativeMethods.SendMessage(t.Handle, NativeMethods.EM_SETRECT, IntPtr.Zero, ref r);
            }
            catch { }
        }

        /// <summary>The icon the shell associates with a file extension, or null.</summary>
        public static Icon FileTypeIcon(string extension)
        {
            if (!IsWindows || string.IsNullOrEmpty(extension)) return null;
            try
            {
                var info = new NativeMethods.SHFILEINFO();
                IntPtr r = NativeMethods.SHGetFileInfo("file" + extension, NativeMethods.FILE_ATTRIBUTE_NORMAL, ref info,
                                                       (uint)Marshal.SizeOf(typeof(NativeMethods.SHFILEINFO)),
                                                       NativeMethods.SHGFI_ICON | NativeMethods.SHGFI_SMALLICON
                                                       | NativeMethods.SHGFI_USEFILEATTRIBUTES);
                if (r == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;
                Icon copy = (Icon)Icon.FromHandle(info.hIcon).Clone();
                NativeMethods.DestroyIcon(info.hIcon);
                return copy;
            }
            catch { return null; }
        }

        public enum DialogIcon { None, Information, Warning, Error }

        /// <summary>
        /// Vista task dialog: large main instruction, explanatory body, standard
        /// buttons. Falls back to a message box where comctl32 v6 is missing.
        /// Its first button is always the default one, so it is used for
        /// information and questions - never to confirm something risky: see
        /// <see cref="ConfirmRisky"/>.
        /// </summary>
        public static DialogResult Show(IWin32Window owner, string title, string instruction,
                                        string content, MessageBoxButtons buttons, DialogIcon icon)
        {
            if (IsWindows)
            {
                try
                {
                    int common;
                    switch (buttons)
                    {
                        case MessageBoxButtons.OKCancel: common = 0x0001 | 0x0008; break;
                        case MessageBoxButtons.YesNo: common = 0x0002 | 0x0004; break;
                        case MessageBoxButtons.YesNoCancel: common = 0x0002 | 0x0004 | 0x0008; break;
                        default: common = 0x0001; break;
                    }
                    IntPtr hIcon = IntPtr.Zero;
                    if (icon == DialogIcon.Warning) hIcon = new IntPtr(65535);            // TD_WARNING_ICON
                    else if (icon == DialogIcon.Error) hIcon = new IntPtr(65534);         // TD_ERROR_ICON
                    else if (icon == DialogIcon.Information) hIcon = new IntPtr(65533);   // TD_INFORMATION_ICON

                    int pressed;
                    IntPtr parent = owner == null ? IntPtr.Zero : owner.Handle;
                    int hr = NativeMethods.TaskDialog(parent, IntPtr.Zero, title, instruction, content,
                                                      common, hIcon, out pressed);
                    if (hr == 0)
                    {
                        switch (pressed)
                        {
                            case 1: return DialogResult.OK;
                            case 2: return DialogResult.Cancel;
                            case 6: return DialogResult.Yes;
                            case 7: return DialogResult.No;
                            default: return DialogResult.OK;
                        }
                    }
                }
                catch { }      // older comctl32: fall through to the message box
            }

            string body = string.IsNullOrEmpty(content) ? instruction : instruction + "\r\n\r\n" + content;
            return MessageBox.Show(owner, body, title, buttons, ToMessageBoxIcon(icon));
        }

        /// <summary>
        /// Asks before something that removes files. Unlike <see cref="Show"/>,
        /// the default button - the one Enter presses - is Cancel, as the Windows
        /// guidelines ask for risky actions: a stray Enter never moves anything.
        /// True only when the user clicked OK.
        /// </summary>
        public static bool ConfirmRisky(IWin32Window owner, string title, string instruction, string content)
        {
            string body = string.IsNullOrEmpty(content) ? instruction : instruction + "\r\n\r\n" + content;
            return MessageBox.Show(owner, body, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning,
                                   MessageBoxDefaultButton.Button2) == DialogResult.OK;
        }

        private static MessageBoxIcon ToMessageBoxIcon(DialogIcon icon)
        {
            switch (icon)
            {
                case DialogIcon.Warning: return MessageBoxIcon.Warning;
                case DialogIcon.Error: return MessageBoxIcon.Error;
                case DialogIcon.Information: return MessageBoxIcon.Information;
                default: return MessageBoxIcon.None;
            }
        }
    }
}
