using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using MS.WindowsAPICodePack.Internal;

namespace Utilities
{
    using G = Graphics;
    using SH = Shell;

    #region Json System Utilities
    public class Json
    {
        public static T GetObjectFromJson<T>(string json) {
            StringReader textReader = new (string.IsNullOrEmpty(json) ? "{}" : json);
            var jsonSerializer = Newtonsoft.Json.JsonSerializer.Create();
            Newtonsoft.Json.JsonTextReader jsonReader = new (textReader);
            return jsonSerializer.Deserialize<T>(jsonReader);
        }

        public static string GetJsonFromObject(object T) {
            if (T is null) return String.Empty;
            StringWriter textWriter = new ();
            var json = Newtonsoft.Json.JsonSerializer.Create();
            Newtonsoft.Json.JsonTextWriter jsonWriter = new (textWriter);
            json.Serialize(jsonWriter, T);
            return textWriter.ToString();
        }
    }
    #endregion

    #region Graphics Icon and Bitmap Utilities
    // Methods for handling Icons, Bitmaps and other Graphics Objects

    // Enable easy optimized XAML access to System Icons stored in various DLLs
    // To use in XAML set the DataContext to an instance of this class (eg DataContext = VM)
    // Then use in XAML Elements as follows: Source="{Binding Path=Icons.ImageRes[176]}"
    public class SystemIconResources
    {
        // Windows ICONs from imageres.dll or shell32.dll (can change look with different OS versions)
        public ImageResIcons ImageRes_S { get; } = new (G.IconSize.Small);
        public ImageResIcons ImageRes { get; } = new (G.IconSize.Large);
        public Shell32Icons Shell32_S { get; } = new (G.IconSize.Small);
        public Shell32Icons Shell32 { get; } = new (G.IconSize.Large);
        public AccessIcons Access_S { get; } = new (G.IconSize.Large);
        public AccessIcons Access { get; } = new (G.IconSize.Large);

        // Property Indexers to allow simplifed XAML Binding and Path access to System Icons
        // To use in XAML use Binding as follows: Source="{Binding Path=Icons.ImageRes[176]}"
        public class ImageResIcons
        {
            readonly G.IconSize IconSize;
            public ImageResIcons(G.IconSize iconSize) { IconSize = iconSize; }
            public BitmapImage this[int index] => G.GetBitmapImage(SH.IMAGERES_DLL, index, IconSize);
        };

        public class Shell32Icons
        {
            readonly G.IconSize IconSize;
            public Shell32Icons(G.IconSize iconSize) { IconSize = iconSize; }
            public BitmapImage this[int index] => G.GetBitmapImage(SH.SHELL32_DLL, index, IconSize);
        };

        public class AccessIcons
        {
            readonly G.IconSize IconSize;
            public AccessIcons(G.IconSize iconSize) { IconSize = iconSize; }
            public BitmapImage this[int index] => G.GetBitmapImage(SH.ACCICONS_EXE, index, IconSize);
        };
    }

    public static class Graphics
    {
        public enum FolderType { Closed, Open }
        public enum IconSize { Large, Small }

        static readonly Dictionary<string, BitmapImage> BitmapImageCache = new ();

        public static Icon ExtractIcon(FileInfo fi, int iconIndex, IconSize iconSize = IconSize.Large) {
            try {
                int rc = ExtractIconEx(fi.FullName, iconIndex, out IntPtr iconLarge, out IntPtr iconSmall, 1);
                if (rc != 2) {
                    throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
                }
                return Icon.FromHandle(iconSize == IconSize.Large ? iconLarge : iconSmall);
            } catch (Exception ex) {
                Exceptions.LogException(ex, $"Unable to Extract {iconSize} Icon {iconIndex} from {fi.FullName}");
            }
            return null;
        }

        public static BitmapImage GetBitmapImage(FileInfo fi, int resourceIndex, IconSize iconSize = IconSize.Large) {
            // Check the Cache first and then Extract the Icon if required
            string key = $"{fi.FullName}:{iconSize}:{resourceIndex}";
            if (!BitmapImageCache.TryGetValue(key, out BitmapImage bmi)) {
                var icon = ExtractIcon(fi, resourceIndex, iconSize);
                bmi = icon != null ? GetBitmapImage(icon) : new ();
                BitmapImageCache.Add(key, bmi);
            }
            return bmi;
        }

        public static BitmapImage GetBitmapImage(Icon icon) {
            BitmapImage bitmapImage = new ();
            try {
                Bitmap bitmap = icon.ToBitmap();
                MemoryStream stream = new ();
                bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                bitmapImage.BeginInit();
                stream.Seek(0, SeekOrigin.Begin);
                bitmapImage.StreamSource = stream;
                bitmapImage.EndInit();
            } catch { }

            return bitmapImage;
        }

        /* C# uses uses RGB (red, green, and blue) component values to specify colors where each component value is between 0 and 255.
         * The HLS system uses the values hue, lightness, and saturation where:
         * Hue determines the color with a 0 to 360 degree direction on a color wheel.
         * Lightness indicates how much light is in the color.
         *  When lightness = 0, the color is black.
         *  When lightness = 1, the color is white.
         *  When lightness = 0.5, the color is as “pure” as possible.
         *
         * Saturation indicates the amount of color added. You can think of this as the opposite of “grayness.”
         *  When saturation = 0, the color is pure gray.
         *  When lightness = 0.5 you get a neutral color.
         *  When saturation is 1, the color is “pure.”
         */

        // Convert an RGB value into an HLS value.
        public static void RgbToHls(int r, int g, int b, out double h, out double l, out double s) {
            // Convert RGB to a 0.0 to 1.0 range.
            double double_r = r / 255.0;
            double double_g = g / 255.0;
            double double_b = b / 255.0;

            // Get the maximum and minimum RGB components.
            double max = double_r;
            if (max < double_g) max = double_g;
            if (max < double_b) max = double_b;
            double min = double_r;
            if (min > double_g) min = double_g;
            if (min > double_b) min = double_b;

            double diff = max - min;
            l = (max + min) / 2;
            if (Math.Abs(diff) < 0.00001) {
                s = 0;
                h = 0; // H is really undefined.
            } else {
                if (l <= 0.5) s = diff / (max + min);
                else s = diff / (2 - max - min);

                double r_dist = (max - double_r) / diff;
                double g_dist = (max - double_g) / diff;
                double b_dist = (max - double_b) / diff;

                if (double_r == max) h = b_dist - g_dist;
                else if (double_g == max) h = 2 + r_dist - b_dist;
                else h = 4 + g_dist - r_dist;
                h *= 60;
                if (h < 0) h += 360;
            }
        }

        // Convert an HLS value into an RGB value.
        public static void HlsToRgb(double h, double l, double s, out int r, out int g, out int b) {
            double p2;

            if (l <= 0.5) p2 = l * (1 + s);
            else p2 = l + s - l * s;

            double p1 = 2 * l - p2;
            double double_r, double_g, double_b;

            if (s == 0) {
                double_r = l;
                double_g = l;
                double_b = l;
            } else {
                double_r = QqhToRgb(p1, p2, h + 120);
                double_g = QqhToRgb(p1, p2, h);
                double_b = QqhToRgb(p1, p2, h - 120);
            }

            // Convert RGB to the 0 to 255 range.
            r = (int)(double_r * 255.0);
            g = (int)(double_g * 255.0);
            b = (int)(double_b * 255.0);
        }

        private static double QqhToRgb(double q1, double q2, double hue) {
            if (hue > 360) hue -= 360;
            else if (hue < 0) hue += 360;
            if (hue < 60) return q1 + (q2 - q1) * hue / 60;
            if (hue < 180) return q2;
            if (hue < 240) return q1 + (q2 - q1) * (240 - hue) / 60;
            return q1;
        }
        // Deal with font family property carefully converting from fontFamily to string name
        public static string GetFamilyFontName(System.Windows.Media.FontFamily fontFamily) {
            string fontName = fontFamily.Source;
            if (fontName.IndexOf("=") > 0 && fontName.IndexOf("]") > 1) {
                fontName = fontName.Substring(fontName.IndexOf("=") + 1, fontName.IndexOf("]") - fontName.IndexOf("=") - 1);
            }

            return fontName;
        }

        // Get the Screen associated with the JSON monitor information
        public static Screen GetScreen(string monitorInfo) {
            try {
                var monitor = Json.GetObjectFromJson<dynamic>(monitorInfo);
                if (monitor != null) {
                    ArrayList allScreens = new (Screen.AllScreens);
                    foreach (Screen screen in allScreens) {
                        if (screen.DeviceName?.Equals(monitor.DeviceName?.Value, StringComparison.OrdinalIgnoreCase)) {
                            return screen;
                        }
                    }
                }
            } catch {}
            return null;
        }
        
        // Get the JSON monitor information for given Window 
        public static string GetMonitorInfo(System.Windows.Window window) {
            var screen = Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(window).Handle);
            return Json.GetJsonFromObject(screen);
        }

        public static Rectangle GetWorkingArea(System.Windows.Window window) {

            RECT lprc = new () {
                Left = (int)window.Left,
                Top = (int)window.Top,
                Bottom = (int)(window.Top + window.Height),
                Right = (int)(window.Left + window.Width)
            };

            uint dwFlags1 = 2; /* uint dwFlags2 = 0;*/
            // if (!(MonitorFromRect(ref lprc, dwFlags2) == IntPtr.Zero)) return new Rectangle();

            IntPtr hMonitor = MonitorFromRect(ref lprc, dwFlags1);
            if (hMonitor == IntPtr.Zero) return new ();

            MONITORINFO lpmi = new ();
            lpmi.cbSize = Marshal.SizeOf((object)lpmi);

            bool rc = GetMonitorInfo(hMonitor, lpmi);
            if (rc) {
                RECT wa = lpmi.rcWork;
                return new (wa.Left, wa.Top, wa.Right, wa.Bottom);
            }
            
            return new ();
        }

        public static bool MonitorExists(int monitorNumber) {
            return monitorNumber <= Screen.AllScreens.Length;
        }
        
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetMonitorInfo(IntPtr hMonitor, [In, Out] MONITORINFO lpmi);

        [DllImport("user32.dll")]
        static extern IntPtr MonitorFromRect([In] ref RECT lprc, uint dwFlags);

        [DllImport("Shell32.dll", EntryPoint = "ExtractIconExW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        internal static extern int ExtractIconEx(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, int nIcons);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyIcon(IntPtr hIcon);
    }
    #endregion

    #region  Windows Shell Utilities
    public static class Shell
    {
        static readonly Dictionary<string, string> DocTypeCache = new ();
        static readonly Dictionary<string, Icon> IconCache = new ();
        public enum DocumentType { All, Access, Acrobat, Excel, PowerPoint, Publisher, Word, Other }

        public static DocumentType GetDocumentType(FileInfo fi) {
            try {
                // Check the Cache first and then use the Shell API if required
                if (!DocTypeCache.TryGetValue(fi.Extension, out string docType)) {
                    docType = GetShellTypeName(fi).ToLower();
                    DocTypeCache.Add(fi.Extension, docType);
                }

                // Document type contains string describing the file type as the shell sees it
                return docType switch
                {
                    string s when s.Contains("excel") => DocumentType.Excel,
                    string s when s.Contains("word") => DocumentType.Word,
                    string s when s.Contains("powerpoint") => DocumentType.PowerPoint,
                    string s when s.Contains("publisher") => DocumentType.Publisher,
                    string s when s.Contains("access") => DocumentType.Access,
                    string s when s.Contains("acrobat") => DocumentType.Acrobat,
                    _ => DocumentType.Other,
                };
            } catch (Exception ex) {
                Exceptions.LogException(ex, $"Unable to Get Document Type of {fi.FullName}");
            }
            return DocumentType.Other;
        }

        // Open a Windows standard FolderBrowser Dialog to select a Directory
        // RND: Determine how to use newer FolderBrowser that is used by .NET Core 3.1
        public static DirectoryInfo ChooseFolder(string title) {
            using var fbd = new FolderBrowserDialog { Description = title };
            return fbd.ShowDialog() == DialogResult.OK && Directory.Exists(fbd.SelectedPath) ? new (fbd.SelectedPath) : null;
        }

        public static Icon GetShellIcon(FileSystemInfo fsi, Graphics.IconSize size = Graphics.IconSize.Large) {
            try {
                // Check the Cache first and only use slower Shell API if required
                var key = fsi is DirectoryInfo di ? di.FullName : fsi is FileInfo fi ? fi.Extension : "";
                if (IconCache.TryGetValue($"{key}:{size}", out Icon icon)) { return icon; }

                uint flags = size == Graphics.IconSize.Small ? SHGFI_ICON + SHGFI_SMALLICON : SHGFI_ICON + SHGFI_LARGEICON;
                SHFILEINFO shfi = new ();

                var res = SHGetFileInfo(fsi.FullName, FILE_ATTRIBUTE_NORMAL, out shfi, (uint)Marshal.SizeOf(shfi), flags);
                if (res == IntPtr.Zero) {
                    return null;
                    // TODO: If we cannot get a Shell Icon (missing Net Drive or Authorization etc), provide a default one:
                    //throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
                }

                Icon.FromHandle(shfi.hIcon); // Load the icon from an HICON handle
                // Clone icon, so that it can be successfully stored by WPF
                icon = (Icon)Icon.FromHandle(shfi.hIcon).Clone();   // Copy it
                Graphics.DestroyIcon(shfi.hIcon);                   // Clean it up
                IconCache.Add($"{key}:{size}", icon);               // Add Cloned one to cache
                return icon;

            } catch (Exception ex) {
                Exceptions.LogException(ex, $"Unable to Get Shell Icon for {fsi.FullName}");
            }
            return null;
        }

        public static string GetShellTypeName(FileInfo fi) {
            try {
                uint flags = SHGFI_TYPENAME;
                SHFILEINFO shfi = new ();
                var res = SHGetFileInfo(fi.FullName, FILE_ATTRIBUTE_NORMAL, out shfi, (uint)Marshal.SizeOf(shfi), flags);
                if (res == IntPtr.Zero) { throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()); }
                return shfi.szTypeName;
            } catch (Exception ex) {
                Exceptions.LogException(ex, $"Unable to Get Shell Type of {fi.FullName}");
            }
            return "*Unknown";
        }

        // Return the best size matched thumbnail for given width
        public static BitmapSource GetShellThumbnail(string localPath, double width) {
            
            double XL =Math.Abs(DefaultThumbnailSize.ExtraLarge.Width - width);
            double L = Math.Abs(DefaultThumbnailSize.Large.Width - width);
            double M = Math.Abs(DefaultThumbnailSize.Medium.Width - width);
            double S = Math.Abs(DefaultThumbnailSize.Small.Width - width);

            /* BUG: #85 If Shell API in Microsoft.WindowsAPICodePack.Shell cannot get/generate a Thumbnail (eg from PDF files), 
                An deadly exception occurs when accessing Thumbnail properties (as we do below) and also 
                causes a major problem of suspending the Dispatcher! which locks up the UI.
                To reproduce, try dragging in a PDF (one that Windows Shell has not created a thumbnail for yet)
                It will not work and it will lockup the UI. (In Release Mode it manages to chug along after a while)
                Use S:\Furniture Shop\PDF Standard Prints\Series (PDF)\Series - Fremont (PDF)\Fremont Assemblies (PDF)\Desks (1mm) PDF\FRD6630-01-STD.pdf
                The problem is that Windows starts up Acrobat Reader to generate a thumbnail if one does not exist for the file.
                Adobe Acrobat is so slow that the Shell API times out waiting for it - and we crash inside WindowsBase.dll.
            */

            try {
                using ShellObject shellObject = ShellObject.FromParsingName(localPath);
                var o = shellObject.Thumbnail?.LargeIcon; // check Thumbnail OK
                return width switch
                {
                    // RND: Cleaner scaling might be done by using delta with size diff accounted for
                    double w when w >= DefaultThumbnailSize.ExtraLarge.Width => XL < L ? shellObject?.Thumbnail.ExtraLargeBitmapSource : shellObject?.Thumbnail.LargeBitmapSource,
                    double w when w >= DefaultThumbnailSize.Large.Width => XL < L ? shellObject?.Thumbnail.ExtraLargeBitmapSource : shellObject?.Thumbnail.LargeBitmapSource,
                    double w when w >= DefaultThumbnailSize.Medium.Width => L < M ? shellObject?.Thumbnail.LargeBitmapSource : shellObject?.Thumbnail.MediumBitmapSource,
                    double w when w >= DefaultThumbnailSize.Small.Width => M < S ? shellObject?.Thumbnail.MediumBitmapSource : shellObject?.Thumbnail.SmallBitmapSource,
                    _ => shellObject?.Thumbnail.SmallBitmapSource
                };
            } catch {
                Icon icon = GetShellIcon(new FileInfo(localPath));
                return Graphics.GetBitmapImage(icon);
            }
        }

        public static void ShellOpenDirectory(FileInfo fileInfo) {
            ShellOpen(new DirectoryInfo(fileInfo.DirectoryName));
        }

        public static void ShellOpen(string uriPath) {
            try {
                ProcessStartInfo psi = new () {
                    FileName = uriPath,
                    UseShellExecute = true,
                    Verb = "Open"
                };

                Process.Start(psi);
            } catch (Exception ex) {
                Exceptions.LogException(ex, $"Unable to Shell Open {uriPath}");
            }
        }

        public static void ShellOpen(FileSystemInfo fileInfoOrDirectoryInfo) {
            try {
                ProcessStartInfo psi = new () {
                    FileName = fileInfoOrDirectoryInfo.FullName,
                    UseShellExecute = true,
                    Verb = "Open"
                };

                Process.Start(psi);
            } catch (Exception ex) {
                Exceptions.LogException(ex, $"Unable to Shell Open {fileInfoOrDirectoryInfo.FullName}");
            }
        }

        public static FileInfo CreateURLShortcut( Uri uri) {

            ShellLink shellLink = new ();
            IShellLinkW iShellLink = (IShellLinkW)shellLink;
            iShellLink.SetPath(uri.AbsoluteUri);

            // TODO: Set the Shortcut / Link Properties (not working yet)
            using PropVariant target = new (uri.AbsoluteUri);
            using PropVariant comment = new ("COMMENT COOL");
            (iShellLink as IPropertyStore).SetValue(SystemProperties.System.Link.TargetParsingPath, target);
            (iShellLink as IPropertyStore).SetValue(SystemProperties.System.Link.Comment, comment);
            (iShellLink as IPropertyStore).SetValue(SystemProperties.System.Link.Description, target);
            (iShellLink as IPropertyStore).Commit();

            FileInfo fi = new (Path.GetTempFileName());
            string file = Path.ChangeExtension(fi.FullName, "lnk");
            (iShellLink as IPersistFile).Save(file, true); // Save the shortcut

            fi.Delete();    // delete the original temp file
            return new (file);
        }

        // Get Office Binary from different versions of Office and return the latest version found:
        public static FileInfo GetOfficeBinary(string binaryName, string subDir = ".") {
            string[] officeVersions = { "Office19", "Office16", "Office15", "Office14", "Office13" };

            foreach (var officeVersion in officeVersions) {
                FileInfo exe64 = new (Path.Combine($@"C:\Program Files\Microsoft Office\{officeVersion}", subDir, binaryName));
                if (exe64.Exists) { return exe64; }
                FileInfo exe32 = new (Path.Combine($@"C:\Program Files (x86)\Microsoft Office\{officeVersion}", subDir, binaryName));
                if (exe32.Exists) { return exe32; }
            }
            return null;
        }

        public static readonly FileInfo IMAGERES_DLL = new (Path.Combine(System.Environment.GetEnvironmentVariable("SYSTEMROOT"), "SYSTEM32", "imageres.dll"));
        public static readonly FileInfo SHELL32_DLL = new (Path.Combine(System.Environment.GetEnvironmentVariable("SYSTEMROOT"), "SYSTEM32", "shell32.dll"));
        public static readonly FileInfo ACCICONS_EXE = GetOfficeBinary("ACCICONS.EXE");

        public const uint SHGFI_ICON = 0x000000100;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        public const uint SHGFI_OPENICON = 0x000000002;
        public const uint SHGFI_SMALLICON = 0x000000001;
        public const uint SHGFI_LARGEICON = 0x000000000;
        public const uint SHGFI_TYPENAME = 0x000000400;
        public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x00008000;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        };

        // Adds an Icon to the TaskBar Notification Area.
        // hwnd - handle to the window to receive callback messages 
        // uID - identifier of the icon 
        // hicon - handle to the icon to add 
        // lpszTip - tooltip text
        // Returns TRUE if successful, or FALSE otherwise. 
        public static bool AddTaskBarIcon(System.Windows.Window window, int uID, Icon icon, string toolTip) {

            NOTIFYICONDATA nid = new NOTIFYICONDATA {
                hWnd = new System.Windows.Interop.WindowInteropHelper(window).Handle,
                uID = uID,
                uFlags = NIF.NIF_ICON | NIF.NIF_TIP | NIF.NIF_SHOWTIP,
                dwState = NIS.NIS_SHAREDICON,
                hIcon = icon.Handle,
        };
            nid.cbSize = Marshal.SizeOf((object)nid);
            toolTip.CopyTo(0, nid.szTip, 0, toolTip.Length);

            bool rc = Shell_NotifyIcon(NIM.NIM_ADD, nid);
            if (rc == true) {
                rc = Shell_NotifyIcon(NIM.NIM_SETVERSION, nid);
            }
            return rc;
        }
        
        public static bool ModifyTaskBarIcon(System.Windows.Window window, int uID, Icon icon, string toolTip) {

            NOTIFYICONDATA nid = new NOTIFYICONDATA {
                hWnd = new System.Windows.Interop.WindowInteropHelper(window).Handle,
                uID = uID,
                uFlags = NIF.NIF_INFO,
                hIcon = icon.Handle,
            };
            nid.cbSize = Marshal.SizeOf((object)nid);
            toolTip.CopyTo(0, nid.szTip, 0, toolTip.Length);
            "The Notification Info Message   ".CopyTo(0, nid.szInfo, 0, 32);
            "The Notification Info Title     ".CopyTo(0, nid.szInfoTitle, 0, 32);

            bool rc = Shell_NotifyIcon(NIM.NIM_MODIFY, nid);
            return rc;
        }
        // Deletes an Icon from the Task Bar Notification Area.
        // hwnd - handle to the window that added the icon. 
        // uID - identifier of the icon to delete.
        // Returns TRUE if successful, or FALSE otherwise.
        public static bool DeleteTaskBarIcon(System.Windows.Window window, int uID) {
            
            NOTIFYICONDATA nid = new NOTIFYICONDATA {
                hWnd = new System.Windows.Interop.WindowInteropHelper(window).Handle,
                uID = uID
            };
            nid.cbSize = Marshal.SizeOf((object)nid);
            
            return Shell_NotifyIcon(NIM.NIM_DELETE, nid);
        }

        public static bool GetTaskBarIconLocation(System.Windows.Window window, int uID) {

            NOTIFYICONIDENTIFIER nci = new NOTIFYICONIDENTIFIER {
                hWnd = new System.Windows.Interop.WindowInteropHelper(window).Handle,
                uID = uID
            };
            nci.cbSize = Marshal.SizeOf((object)nci);

            RECT rect = new ();

        bool rc = Shell_NotifyIconGetRect(nci, rect);
            return rc;
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct NOTIFYICONIDENTIFIER
        {
            public int cbSize;
            public IntPtr hWnd;
            public int uID;
            public Guid guidItem;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public long left;
            public long top;
            public long right;
            public long bottom;
        }
        
        // @see https://docs.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shell_notifyicongetrect
        [DllImport("shell32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool Shell_NotifyIconGetRect([In] NOTIFYICONIDENTIFIER identifier, [Out] RECT iconLocation);

        // @see https://docs.microsoft.com/en-us/windows/win32/api/shellapi/ns-shellapi-notifyicondataa
        [DllImport("shell32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool Shell_NotifyIcon(NIM dwMessage, [In] NOTIFYICONDATA lpdata);

        public enum NIM : uint {
            NIM_ADD         = 0x00000000,   // Adds an icon to the status area.The icon is given an identifier in the NOTIFYICONDATA structure pointed to by lpdata—either through its uID or guidItem member.This id is used in subsequent calls to Shell_NotifyIcon to perform later actions on the icon.
            NIM_MODIFY      = 0x00000001,   // Modifies an icon in the status area.NOTIFYICONDATA structure pointed to by lpdata uses the ID originally assigned to the icon when it was added to the notification area(NIM_ADD) to identify the icon to be modified.
            NIM_DELETE      = 0x00000002,   // Deletes an icon from the status area.NOTIFYICONDATA structure pointed to by lpdata uses the ID originally assigned to the icon when it was added to the notification area(NIM_ADD) to identify the icon to be deleted.
            NIM_SETFOCUS    = 0x00000003,   // Shell32.dll version 5.0 and later only.Returns focus to the taskbar notification area.Notification area icons should use this message when they have completed their UI operation. For example, if the icon displays a shortcut menu, but the user presses ESC to cancel it, use NIM_SETFOCUS to return focus to the notification area.
            NIM_SETVERSION  = 0x00000004,   // Shell32.dll version 5.0 and later only. Instructs the notification area to behave according to the version number specified in the uVersion member of the structure pointed to by lpdata. The version number specifies which members are recognized.NIM_SETVERSION must be called every time a notification area icon is added (NIM_ADD). It does not need to be called with NIM_MODIFY.The version setting is not persisted once a user logs off.
        }
        
        public enum NIF : uint {
            NIF_MESSAGE     = 0x00000001,       // The uCallbackMessage member is valid.
            NIF_ICON        = 0x00000002,       // The hIcon member is valid.
            NIF_TIP         = 0x00000004,       // The szTip member is valid.
            NIF_STATE       = 0x00000008,       // The dwState and dwStateMask members are valid.
            NIF_INFO        = 0x00000010,       // Display a balloon notification.The szInfo, szInfoTitle, dwInfoFlags, and uTimeout members are valid.
            /* Note that uTimeout is valid only in Windows 2000 and Windows XP. To display the balloon notification, specify NIF_INFO and provide text in szInfo. To remove a balloon notification, specify NIF_INFO and provide an empty string through szInfo. To add a notification area icon without displaying a notification, do not set the NIF_INFO flag. */
            NIF_GUID        = 0x00000020,       // Windows 7 and later: The guidItem is valid. Windows Vista and earlier: Reserved.
            NIF_REALTIME    = 0x00000040,       // Windows Vista and later.If the balloon notification cannot be displayed immediately, discard it. Use this flag for notifications that represent real-time information which would be meaningless or misleading if displayed at a later time.For example, a message that states "Your telephone is ringing." NIF_REALTIME is meaningful only when combined with the NIF_INFO flag.
            NIF_SHOWTIP     = 0x00000080        // Windows Vista and later. Use the standard tooltip. Normally, when uVersion is set to NOTIFYICON_VERSION_4, the standard tooltip is suppressed and can be replaced by the application-drawn, pop-up UI. If the application wants to show the standard tooltip with NOTIFYICON_VERSION_4, it can specify NIF_SHOWTIP to indicate the standard tooltip should still be shown.
        }
        
        public enum NIS : uint {
            NIS_HIDDEN      = 0x00000001,       // The icon is hidden.
            NIS_SHAREDICON  = 0x00000002        // The icon resource is shared between multiple icons.
        }

        [StructLayout(LayoutKind.Sequential)]
            public struct NOTIFYICONDATA
            {
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
                public char[] szTip = new char[128];
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
                public char[] szInfo = new char[256];
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
                public char[] szInfoTitle = new char[64];
                public int cbSize;
                public IntPtr hWnd;
                public int uID;
                public NIF uFlags;
                public int uCallbackMessage;
                public IntPtr hIcon;
                public NIS dwState;
                public uint dwStateMask;
                public uint uVersion;
                public uint dwInfoFlags;
                public Guid guidItem;
                public IntPtr hBalloonIcon;
            }
    }
    
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellLinkW
        {
            void GetPath(
                [Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile,
                int cchMaxPath,
                IntPtr pfd,
                uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription(
                [Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile,
                int cchMaxName);
            void SetDescription(
                [MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory(
                [Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir,
                int cchMaxPath
                );
            void SetWorkingDirectory(
                [MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments(
                [Out(), MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs,
                int cchMaxPath);
            void SetArguments(
                [MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotKey(out short wHotKey);
            void SetHotKey(short wHotKey);
            void GetShowCmd(out uint iShowCmd);
            void SetShowCmd(uint iShowCmd);
            void GetIconLocation(
                [Out(), MarshalAs(UnmanagedType.LPWStr)] out StringBuilder pszIconPath,
                int cchIconPath,
                out int iIcon);
            void SetIconLocation(
                [MarshalAs(UnmanagedType.LPWStr)] string pszIconPath,
                int iIcon);
            void SetRelativePath(
                [MarshalAs(UnmanagedType.LPWStr)] string pszPathRel,
                uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath(string pszFile);
        }

        [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPersistFile
        {
            string GetCurFile();
            [PreserveSig]
            uint IsDirty();
            void Load(string pszFileName, long dwMode);
            void Save(string pszFileName, bool fRemember);
            void SaveCompleted(string pszFileName);
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPropertyStore
        {
            uint GetCount();
            PropertyKey GetAt(uint propertyIndex);
            PropVariant GetValue([In] ref PropertyKey key);
            void SetValue([In] ref PropertyKey key, PropVariant pv);
            void Commit();
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046"), ClassInterface(ClassInterfaceType.None)]
        class ShellLink { }    
    #endregion

    #region Exception Handling Utilities
    public static class Exceptions
    {
        public static void LogException(Exception ex, string errorMessage = "", bool showMessage = false) {
            string msg = ex.FormatException(errorMessage);
            Debug.WriteLine(msg);
#if RELEASE
                if (!showMessage) return;
#endif
            var assembly = System.Reflection.Assembly.GetEntryAssembly();
            string title = assembly.GetName().Name;

            MessageBox.Show(msg, $"{title}: Exception Error");
        }
    }

    #endregion

    #region .NET Extension Methods
    /* Extension classes must be static and can only be nested to one level. If extension methods are
     * in a nested static class beyond the first level, the compiler will complain. You cannot use
     * alias using statements such as using EXT = Utilities.Extensions due to compiler not able to
     * locate and resolve the extended call. You can use a normal using statement to access the
     * extensions and add another using alias to access the other normal methods. You cannot refer to
     * dynamic vars (such as COM types) as parameters or return types on extension methods - you will
     * get a compiler error such as "extension methods cannot be dynamically dispatched". */

    public static class Extensions
    {

        // ListBox Generic Extension Methods:

        // Run an Action delegate on all the Selected Items of the ListBox
        public static void RunAction<T>(this System.Windows.Controls.ListBox self, Action<T> action) {
            if (self.DataContext is ICollection<T>) {
                ArrayList clonedList = new (self.SelectedItems); // Clone to safely iterate
                foreach (T item in clonedList) {
                    try {
                        action(item);
                    } catch {
                        throw;  // just rethrow the exception
                    }
                }
            } else {
                throw new InvalidOperationException($"DataContext of {self} is of type {self.DataContext}. Must be of type {typeof(T)} to run action {action}");
            }
        }

        // ObservableCollection Extension methods:
        // Check to see if an item in the collection Exists based on user specified predicate
        // Contains will not suffice due to it compares object instances not property values
        public static bool Exists<T>(this ObservableCollection<T> self, Predicate<T> predicate) {
            foreach (T item in self) {
                if (predicate(item)) { return true; }
            }
            return false;
        }

        // ItemCollection Extension Methods
        public static void Map(System.Windows.Controls.ItemCollection self, Predicate<object> filter, Action<object> action) {
            foreach (object item in self) {
                if (filter(item)) { action(item); }
            }
        }

        // Exception Handling Extension Methods:
        // Pretty format an Exception including the inner exception(s)
        public static string FormatException(this Exception self, string errorMessage, int indent = 0) {
            string indentStr = new ('\t', indent);
            StringBuilder msg = new (errorMessage + "\n\n");
            string[] exMessageLines = self.ToString().Split('\n');
            foreach (string line in exMessageLines) {
                msg.Append(indentStr);
                msg.Append(line);
                msg.Append('\n');
            }
            if (self.InnerException != null) {
                msg.Append("\n\n");
                msg.Append(self.InnerException.FormatException("", indent + 1));
            }
            return msg.ToString();
        }

        // string Extension Methods:

        // Compare to another string - Case Insensitive
        public static bool EqualsIC(this string self, string other, bool IgnoreCase = true) {
            return self.Equals(other, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        // Contains another string - Case Insensitive
        public static bool ContainsIC(this string self, string other) {
            return self.ToLower().Contains(other.ToLower());
        }
        // DirectoryInfo Extension Methods:

        // Equals another DirectoryInfo based on it's FullName (path)
        public static bool EqualsIC(this DirectoryInfo self, DirectoryInfo other, bool ignoreCase = true) {
            return self.FullName.EqualsIC(other.FullName, ignoreCase);
        }

        // FileInfo Extension Methods:
        // Equals another FileInfo based on it's FullName (path)
        public static bool EqualsIC(this FileInfo self, FileInfo other, bool ignoreCase = true) {
            return self.FullName.EqualsIC(other.FullName, ignoreCase);
        }

        // Contains another FileInfo as part of it's FullName (path)
        public static bool ContainsIC(this FileInfo self, FileInfo other, bool ignoreCase = true) {
            return ignoreCase ? self.FullName.ContainsIC(other.FullName) : self.FullName.Contains(other.FullName);
        }

        // Contains a string path - case insensitive
        public static bool ContainsIC(this FileInfo self, string other, bool ignoreCase = true) {
            return ignoreCase ? self.FullName.ContainsIC(other) : self.FullName.Contains(other);
        }

        #endregion
    }
}