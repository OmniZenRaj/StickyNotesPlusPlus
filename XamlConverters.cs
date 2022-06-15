using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Data;
using System.Globalization;

namespace OmniZenNotes
{
    using U = Utilities;
    using Utilities;

    // Convert from Image ToolTip string file path to a Thumbnail BitmapSource (@see Style TargetType="{x:Type Image} Source XAML")
    public class FilePathToThumbNailConverter : IValueConverter
    {
        object IValueConverter.Convert(object o, Type type, object parameter, CultureInfo culture) {
            if (o is Image image && image.ToolTip is string tooltip && image.Tag is double scale) {
                Uri uri = new Uri(tooltip);
                if (uri.IsFile) {
                    FileInfo fileInfo = new FileInfo(tooltip);
                    try {
                        return U.Shell.GetShellThumbnail(fileInfo.FullName, image.Width * scale);
                    } catch {
                        try {
                            System.Drawing.Icon icon = U.Shell.GetShellIcon(fileInfo);
                            return U.Graphics.GetBitmapImage(icon);
                        } catch { }
                    }
                } else {
                    // TODO: Get the favicon for the given url site OR just a system one for now
                    return Graphics.GetBitmapImage(Shell.SHELL32_DLL, 13);
                }
            }

            return null;
        }

        object IValueConverter.ConvertBack(object o, Type type, object parameter, CultureInfo culture) => null;
    }

    // Convert a Tag object double value to a ScaleTransform (@see Style TargetType="{x:Type Image} LayoutTransform XAML")
    public class TagToLayoutTransformConverter : IValueConverter
    {
        object IValueConverter.Convert(object o, Type type, object parameter, CultureInfo culture) {
            if (o is double scale) {
                return new ScaleTransform(scale, scale);
            }

            return null;
        }

        object IValueConverter.ConvertBack(object o, Type type, object parameter, CultureInfo culture) => null;
    }

    // Convert C# Boolean to a Visibility enum for XAML binding conversions (@see uxReminderPanel Visibility XAML)
    public class BooleanToVisibilityConverter : IValueConverter
    {
        object IValueConverter.Convert(object o, Type type, object parameter, CultureInfo culture) {
            if (o is bool visible) {
                return visible == true ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Visible;
        }

        object IValueConverter.ConvertBack(object o, Type type, object parameter, CultureInfo culture) {
            if (o is Visibility visibility) {
                if (visibility == Visibility.Visible) return true; else return false;
            }
            return true;
        }
    }

    // Convert from any Object to string (required for some XAML properties not able to do their own conversion)
    public class ObjectToStringConverter : IValueConverter
    {
        object IValueConverter.Convert(object o, Type type, object parameter, CultureInfo culture) {
            return o.ToString();
        }

        object IValueConverter.ConvertBack(object o, Type type, object parameter, CultureInfo culture) => null;

    }
}