using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections;
using System.Windows.Navigation;
using Microsoft.WindowsAPICodePack.Shell;

namespace OmniZenNotes;

public class FlowDocument : System.Windows.Documents.FlowDocument {    
    public FlowDocument() : base() { }
    public FlowDocument(Block block) : base(block) { }    
   
    // Create an MediaElement and set the Source to the given file
    public void CreateMediaElement(FileInfo fi, TextPointer tp) {

        MediaElement me = new() { Source = new Uri(fi.FullName), ToolTip = fi.FullName };
        me.MediaOpened += (object sender, RoutedEventArgs e) => {
            if (sender is MediaElement me && me.Position == TimeSpan.FromSeconds(0)) {
                me.Position = TimeSpan.FromSeconds(0);
            };
        };

        me.MediaEnded += (object sender, RoutedEventArgs e) => {
            if (sender is MediaElement me) {
                me.Position = TimeSpan.FromSeconds(0);
            }
        };
        
        me.MediaFailed += (object sender, ExceptionRoutedEventArgs e) => {
            if (sender is MediaElement me) {
                // Ignore {System.Runtime.InteropServices.COMException(0xC00D109B): 0xC00D109B}
                if (e.ErrorException.HResult != -1072885605) {   // Erroneous error before MediaOpen is OK
                    var error = $"{me.STR("strMediaFailedMsg")} {me.Source} : ";
                    var iuic = me.Parent as InlineUIContainer;
                    iuic.ContentStart.Paragraph.Inlines.Add(new Run($"{error} {e.ErrorException.Message}"));
                    //EX.LogException(e.ErrorException, error);
                }
            }
        };

#pragma warning disable CA1806 // Never used - is OK due to weak ref
        new InlineUIContainer(me, tp);
#pragma warning restore CA1806
        Debug.WriteLine($"Inserted MediaElement Blocks.Count = {Blocks.Count}");
    }
    // Insert Text from given file as a Text Run
    public void InsertTextFromFile(FileInfo fi, TextPointer tp) {
        // Try to insert it into the Note as Text:
        // Detect byte order marks at the beginning of the file to see if we have text
        using StreamReader sr = new(fi.FullName, true);
        if (sr.CurrentEncoding.EncodingName.Contains("utf", StringComparison.OrdinalIgnoreCase)) {
            // TODO: Determine MAX size text file supported (currently 64KB)
            const long MAX_TEXT = 1024 * 64;
            if (fi.Length <= MAX_TEXT) {
                tp.InsertTextInRun(sr.ReadToEnd());
                sr.Close();
                Debug.WriteLine($" Inserted Text Blocks.Count = {Blocks.Count}");
            }
            EX.LogException(new Exception($"{fi.FullName} Exceed MAX Text Size of {MAX_TEXT}"), "File Size Exceeded MAX", true);
        }
    }
}

public partial class NoteViewer : Window
{
    // Set the Document background to the given file
    // RND: Make a MediaElement the Background
    public void SetDocumentBackground(FileInfo fi, System.Windows.Documents.FlowDocument doc) {
        Image image = CreateImage(fi);
        ImageBrush imageBrush = new(image.Source);
        if (doc.Background is SolidColorBrush scb && scb.Color.A < 255) {
            imageBrush.Opacity = scb.Color.A / 255.0f;
        }
        doc.Background = imageBrush;
        Debug.WriteLine($" SetDocumentBackground Blocks.Count = {doc.Blocks.Count}");
    }
    
    // Create Hyperlink for given file/folder/URL
    public static void CreateHyperLink(FileInfo fi, TextPointer tp) {
        try {
            CreateHyperLink(tp, tp, new(fi.FullName));
        } catch (Exception ex) {
            if (ex.HResult == -2146233079) {
                // Add the new Hyperlink to the end of the current Hyperlink
                tp = tp.GetNextInsertionPosition(LogicalDirection.Forward);
                try {
                    CreateHyperLink(tp, tp, new(fi.FullName));
                } catch {
                    tp = tp.Paragraph != null ? tp.Paragraph.ElementEnd : tp.DocumentEnd;
                    tp.InsertLineBreak();
                    CreateHyperLink(tp, tp, new(fi.FullName));
                }
            }
        }
    }
    // Create HyperLink to given URL 
    public static void CreateHyperLink(string uri, TextPointer tp, TextRange range) {
        try {
            TextPointer tpStart = range.IsEmpty ? tp : range.Start;
            TextPointer tpEnd = range.IsEmpty ? tp : range.End;
            CreateHyperLink(tpStart, tpEnd, new(uri));
        } catch { }


    }

    // Create Hyperlink at given TextPointer position for given URI
    static Hyperlink CreateHyperLink(TextPointer tpStart, TextPointer tpEnd, Uri uri) {
        // Create a Hyperlink to the given file/folder
        Hyperlink hyperlink = new(tpStart, tpEnd) { NavigateUri = uri, };
        if (tpStart.CompareTo(tpEnd) == 0) {
            // Add an Image and Display Name text inside the Hyperlink
            double height = hyperlink.NavigateUri.IsFile ? DefaultThumbnailSize.Small.Height : DefaultIconSize.Small.Height;
            double width = hyperlink.NavigateUri.IsFile ? DefaultThumbnailSize.Small.Width : DefaultIconSize.Small.Width;
            AddImageToHyperLink(hyperlink, height, width, addText: true);
        }
        return hyperlink;
    }

    // Create an Image Element for use in a FlowDocument (creates a temp copy to avoid locking the original)
    Image CreateImage(FileInfo fi) {
        Image image = new();
        try {
            var tempFileName = Path.GetTempFileName();
            File.Copy(fi.FullName, tempFileName, true);
            BitmapImage bitmap = new(new(tempFileName));
            image.Source = bitmap;
            image.Width = Math.Min(bitmap.PixelWidth, Width);
            image.Height = Math.Min(bitmap.PixelHeight, Height - uxToolBar.ActualHeight);
            if (bitmap.PixelWidth > Width || bitmap.PixelHeight > Height) {
                image.SetBinding(WidthProperty, "{Binding ActualWidth,  Mode=OneWay, ElementName=uxRichTextBox}");
                image.SetBinding(HeightProperty, "{Binding ActualHeight, Mode=OneWay, ElementName=uxRichTextBox}");
            }
            image.ToolTip = fi.FullName; image.Tag = fi;
        } catch (Exception ex) { EX.LogException(ex); }
        return image;
    }

    static void AddImageToHyperLink(Hyperlink hyperlink, double height, double width, bool addText = false) {

        Uri uri = hyperlink.NavigateUri;
        string name = !uri.IsFile ? System.Net.WebUtility.UrlDecode(uri.PathAndQuery) : new FileInfo(uri.LocalPath).Name;

        // Create a new image with given height and width
        Image image = new() {
            // ToolTip object is used for xaml Image Style for FilePathToThumbNailConverter to display image as thumbnail
            ToolTip = !uri.IsFile ? System.Net.WebUtility.UrlDecode(uri.OriginalString) : uri.LocalPath,
            // Tag object is used to scale factor for LayoutTransform of the thumbnail image @See NoteViewer.xaml
            Tag = 1.0d,
            Height = height,
            Width = width,
        };

        // Add an image and name text for the Hyperlink
        if (addText) { hyperlink.Inlines.Add($"{ name} "); }
        InlineUIContainer iluic = new(image);
        hyperlink.Inlines.InsertBefore(hyperlink.Inlines.FirstInline, iluic);
        if (addText) { iluic.ElementEnd.InsertLineBreak(); }
        hyperlink.Tag = image;
    }

    // Image Element was Loaded
    public void OnImageElement_Loaded(object sender, RoutedEventArgs e) {
        if (sender is Image im) {
            // Images copy the source uri into a temp file to prevent file locking
            // If the temp file is no longer available, try recreating with original uri
            if (im.Source == null && im.Tag is Uri uri) {
                if (File.Exists(uri.AbsolutePath)) {
                    Image i = CreateImage(new(uri.AbsolutePath));
                    im.Source = i.Source;
                } else {
                    // Inform the user that the image file no longer found
                    string error = $"{SK("strImageFailedMsg")} {uri.AbsolutePath}";
                    InlineUIContainer iuic = im.Parent as InlineUIContainer;
                    if (iuic.ContentStart.Paragraph.Inlines.Count < 2) {
                        iuic.ContentStart.Paragraph.Inlines.Add(new Run($"{error}"));
                    }
                }
            }
        }
    }
    public void OnHyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
        Debug.WriteLine($"OnHyperlink_RequestNavigate for {sender} with {e}");
        if (sender is Hyperlink hyperlink) {
            var uriPath = Uri.UnescapeDataString(hyperlink.NavigateUri.IsFile ?
                hyperlink.NavigateUri.AbsolutePath : hyperlink.NavigateUri.AbsoluteUri);
            U.Shell.ShellOpen(uriPath);
            e.Handled = true;
        }
    }

    public void OnHyperlink_MouseDown(object sender, MouseButtonEventArgs e) {
        Debug.WriteLine($"OnHyperlink_MouseDown for {e.Source}");
        if (sender is Hyperlink hyperlink && e.MouseDevice.LeftButton == MouseButtonState.Pressed) {
            OnHyperlink_RequestNavigate(sender, new(hyperlink.NavigateUri, hyperlink.Name));
        }
    }

    public void OnHyperlink_MouseWheel(object sender, MouseWheelEventArgs e) {
        if (sender is Hyperlink hyperlink && Keyboard.Modifiers == ModifierKeys.Control) {
            e.Handled = true;
            if (!hyperlink.NavigateUri.IsFile) { return; }

            // Each Hyperlink has an associated image stored in the Tag object property
            Image image = hyperlink.Tag as Image;
            // Each Image uses Tag object to scale factor for LayoutTransform of the thumbnail image @See NoteViewer.xaml
            image.Tag = e.Delta > 0 ? (double)image.Tag * 1.10 : (double)image.Tag * 0.90;
            // Scale the thumbnail image using the LayoutTransform until approach closer to the next size
            // When image size changes across the S/M/L/XL thumbnail boundary size, recreate image to new size
            double newWidth = image.Width * (double)image.Tag;
            double newHeight = image.Height * (double)image.Tag;
            var thumbnail = U.Shell.GetShellThumbnail(hyperlink.NavigateUri.LocalPath, newWidth);

            // Recreate image in order to trigger update of image when new size is wanted
            if (thumbnail.Width > image.Width || thumbnail.Width < image.Width) {
                ArrayList inlines = new(hyperlink.Inlines); // Clone to safely iterate
                foreach (var inline in inlines) {
                    if (inline is InlineUIContainer uiContainer) {
                        hyperlink.Inlines.Remove(uiContainer);
                        // Recreate the new sized image and display text inlines in the hyperlink
                        image.Tag = newWidth / thumbnail.Width;  // Scale to new thumbnail size
                        AddImageToHyperLink(hyperlink, newHeight, newWidth);
                        break;
                    }
                }
            }
            Debug.WriteLine($"OnHyperlink_MouseWheel Delta={e.Delta:F0} newWidth {newWidth:F0} Thumbnail {thumbnail.Width} scaled by {image.Tag:F2}");
        }
    }
    

}