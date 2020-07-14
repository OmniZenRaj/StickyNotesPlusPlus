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
using System.Windows.Input;

namespace Utilities
{
    using G = Graphics;
    using SH = Shell;

    #region Json System Utilities
    public class Json
    {
        public static T GetObjectFromJson<T>(string json) {
            var textReader = new StringReader(string.IsNullOrEmpty(json) ? "{}" : json);
            var jsonSerializer = Newtonsoft.Json.JsonSerializer.Create();
            var jsonReader = new Newtonsoft.Json.JsonTextReader(textReader);
            return jsonSerializer.Deserialize<T>(jsonReader);
        }

        public static string GetJsonFromObject(object T) {
            if (T is null) return String.Empty;
            var textWriter = new StringWriter();
            var json = Newtonsoft.Json.JsonSerializer.Create();
            var jsonWriter = new Newtonsoft.Json.JsonTextWriter(textWriter);
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
        public ImageResIcons ImageRes_S { get; } = new ImageResIcons(G.IconSize.Small);
        public ImageResIcons ImageRes { get; } = new ImageResIcons(G.IconSize.Large);
        public Shell32Icons Shell32_S { get; } = new Shell32Icons(G.IconSize.Small);
        public Shell32Icons Shell32 { get; } = new Shell32Icons(G.IconSize.Large);
        public AccessIcons Access_S { get; } = new AccessIcons(G.IconSize.Large);
        public AccessIcons Access { get; } = new AccessIcons(G.IconSize.Large);

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

        static readonly IDictionary<string, BitmapImage> BitmapImageCache = new Dictionary<string, BitmapImage>();

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
                bmi = icon != null ? GetBitmapImage(icon) : new BitmapImage();
                BitmapImageCache.Add(key, bmi);
            }
            return bmi;
        }

        public static BitmapImage GetBitmapImage(Icon icon) {
            var bitmapImage = new BitmapImage();
            try {
                Bitmap bitmap = icon.ToBitmap();
                MemoryStream stream = new MemoryStream();
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
        static readonly IDictionary<string, string> DocTypeCache = new Dictionary<string, string>();
        static readonly IDictionary<string, Icon> IconCache = new Dictionary<string, Icon>();
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
            return fbd.ShowDialog() == DialogResult.OK && Directory.Exists(fbd.SelectedPath) ? new DirectoryInfo(fbd.SelectedPath) : null;
        }

        public static Icon GetShellIcon(FileSystemInfo fsi, Graphics.IconSize size = Graphics.IconSize.Large) {
            try {
                // Check the Cache first and only use slower Shell API if required
                var key = fsi is DirectoryInfo di ? di.FullName : fsi is FileInfo fi ? fi.Extension : "";
                if (IconCache.TryGetValue($"{key}:{size}", out Icon icon)) { return icon; }

                uint flags = size == Graphics.IconSize.Small ? SHGFI_ICON + SHGFI_SMALLICON : SHGFI_ICON + SHGFI_LARGEICON;
                var shfi = new SHFILEINFO();

                var res = SHGetFileInfo(fsi.FullName, FILE_ATTRIBUTE_NORMAL, out shfi, (uint)Marshal.SizeOf(shfi), flags);
                if (res == IntPtr.Zero) {
                    throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error());
                }

                Icon.FromHandle(shfi.hIcon); // Load the icon from an HICON handle
                // Clone icon, so that it can be successfully stored by WPF
                icon = (Icon)Icon.FromHandle(shfi.hIcon).Clone();   // Copy it
                Graphics.DestroyIcon(shfi.hIcon);                          // Clean it up
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
                var shfi = new SHFILEINFO();
                var res = SHGetFileInfo(fi.FullName, FILE_ATTRIBUTE_NORMAL, out shfi, (uint)Marshal.SizeOf(shfi), flags);
                if (res == IntPtr.Zero) { throw Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()); }
                return shfi.szTypeName;
            } catch (Exception ex) {
                Exceptions.LogException(ex, $"Unable to Get Shell Type of {fi.FullName}");
            }
            return "*Unknown";
        }

        public static void ShellOpenDirectory(FileInfo fileInfo) {
            ShellOpen(new DirectoryInfo(fileInfo.DirectoryName));
        }

        public static void ShellOpen(FileSystemInfo fileInfoOrDirectoryInfo) {
            try {
                ProcessStartInfo psi = new ProcessStartInfo() {
                    FileName = fileInfoOrDirectoryInfo.FullName,
                    UseShellExecute = true,
                    Verb = "Open"
                };

                Process.Start(psi);
            } catch (Exception ex) {
                Exceptions.LogException(ex, $"Unable to Shell Open {fileInfoOrDirectoryInfo.FullName}");
            }
        }

        // Get Office Binary from different versions of Office and return the latest version found:
        public static FileInfo GetOfficeBinary(string binaryName, string subDir = ".") {
            string[] officeVersions = { "Office19", "Office16", "Office15", "Office14", "Office13" };

            foreach (var officeVersion in officeVersions) {
                var exe64 = new FileInfo(Path.Combine($@"C:\Program Files\Microsoft Office\{officeVersion}", subDir, binaryName));
                if (exe64.Exists) { return exe64; }
                var exe32 = new FileInfo(Path.Combine($@"C:\Program Files (x86)\Microsoft Office\{officeVersion}", subDir, binaryName));
                if (exe32.Exists) { return exe32; }
            }
            return null;
        }

        public static readonly FileInfo IMAGERES_DLL = new FileInfo(Path.Combine(System.Environment.GetEnvironmentVariable("SYSTEMROOT"), "SYSTEM32", "imageres.dll"));
        public static readonly FileInfo SHELL32_DLL = new FileInfo(Path.Combine(System.Environment.GetEnvironmentVariable("SYSTEMROOT"), "SYSTEM32", "shell32.dll"));
        public static readonly FileInfo ACCICONS_EXE = GetOfficeBinary("ACCICONS.EXE");

        public const uint SHGFI_ICON = 0x000000100;
        public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        public const uint SHGFI_OPENICON = 0x000000002;
        public const uint SHGFI_SMALLICON = 0x000000001;
        public const uint SHGFI_LARGEICON = 0x000000000;
        public const uint SHGFI_TYPENAME = 0x000000400;
        public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x00008000;

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

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
    }
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
                var clonedList = new ArrayList(self.SelectedItems); // Clone to safely iterate
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

        // Exception Handling Extension Methods:
        // Pretty format an Exception including the inner exception(s)
        public static string FormatException(this System.Exception self, string errorMessage, int indent = 0) {
            String indentStr = new String('\t', indent);
            StringBuilder msg = new StringBuilder(errorMessage + "\n\n");
            String[] exMessageLines = self.ToString().Split('\n');
            foreach (String line in exMessageLines) {
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

        // ItemCollection Extension Methods
        public static void Map(System.Windows.Controls.ItemCollection self, Predicate<object> filter, Action<object> action) {
            foreach (object item in self) {
                if (filter(item)) { action(item); }
            }
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

    // Application Level Commands
    public static class AppCommands
    {
        public static RoutedUICommand RefreshCommand = new RoutedUICommand("Refresh", "Refresh", typeof(AppCommands));
        public static RoutedUICommand DeleteCommand = new RoutedUICommand("DeleteNote", "DeleteNote", typeof(AppCommands));
    }
}