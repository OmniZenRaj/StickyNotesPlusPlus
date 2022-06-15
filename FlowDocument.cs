using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections;
using System.Diagnostics;
using System.Windows.Navigation;
using Microsoft.WindowsAPICodePack.Shell;

namespace OmniZenNotes
{
    using U = Utilities;
    using Utilities;

    public partial class NoteViewer : Window
    {
        void uxRichTextBox_TextChanged(object sender, TextChangedEventArgs e) {
        }

        // Create an MediaElement and set the Source to the given file
        public void CreateMediaElement(FileInfo fi) {
            TextPointer tp = uxRichTextBox.CaretPosition;

            var me = new MediaElement { Source = new Uri(fi.FullName), ToolTip = fi.FullName };
            #pragma warning disable CA1806 // Never used - is OK due to weak ref
            new InlineUIContainer(me, tp);
            #pragma warning restore CA1806

        }
        
        // Insert Text from given file as a Text Run
        public void InsertTextFromFile(FileInfo fi) {
            // Try to insert it into the Note as Text:
            // Detect byte order marks at the beginning of the file to see if we have text
            TextPointer tp = uxRichTextBox.CaretPosition;
            using StreamReader sr = new StreamReader(fi.FullName, true);
            if (sr.CurrentEncoding.EncodingName.Contains("utf", StringComparison.InvariantCultureIgnoreCase)) {
                // TODO: Determine MAX size text file supported
                tp.InsertTextInRun(sr.ReadToEnd());
                sr.Close();
                SetFont(VM.Note.UXSettings.FontFamily, VM.Note.UXSettings.FontSize, VM.Note.UXSettings.FontColor, FontStyle);
            }

        }
        
        // Set the Document background to the given file
        // RND: Make a MediaElement the Background
        public void SetDocumentBackground(FileInfo fi) {
            var image = CreateImage(fi);
            var imageBrush = new ImageBrush(image.Source);
            if (uxRichTextBox.Document.Background is SolidColorBrush scb && scb.Color.A < 255) {
                imageBrush.Opacity = scb.Color.A / 255.0f;
            }
            uxRichTextBox.Document.Background = imageBrush;
        }
        
        // Create Hyperlink for given file/folder/URL
        public void CreateHyperLink(FileInfo fi) {
            TextPointer tp = uxRichTextBox.CaretPosition;
            try {
                CreateHyperLink(tp, tp, new Uri(fi.FullName));
            } catch (Exception ex) {
                if (ex.HResult == -2146233079) {
                    // Add the new Hyperlink to the end of the current Hyperlink
                    tp = tp.GetNextInsertionPosition(LogicalDirection.Forward);
                    try {
                        CreateHyperLink(tp, tp, new Uri(fi.FullName));
                    } catch {
                        tp = tp.Paragraph != null ? tp.Paragraph.ElementEnd : tp.DocumentEnd;
                        tp.InsertLineBreak();
                        CreateHyperLink(tp, tp, new Uri(fi.FullName));
                    }
                }
            }
        }
        // Create HyperLink to given URL 
        public void CreateHyperLink(string uri) {
            TextPointer tp = uxRichTextBox.CaretPosition;
            try {
                TextRange range = uxRichTextBox.Selection;
                TextPointer tpStart = range.IsEmpty ? tp : range.Start;
                TextPointer tpEnd = range.IsEmpty ? tp : range.End;
                CreateHyperLink(tpStart, tpEnd, new Uri(uri));
            } catch { }


        }
        
        // Create Hyperlink at given TextPointer position for given URI
        static Hyperlink CreateHyperLink(TextPointer tpStart, TextPointer tpEnd, Uri uri) {
            // Create a Hyperlink to the given file/folder
            Hyperlink hyperlink = new Hyperlink(tpStart, tpEnd) { NavigateUri = uri, };
            if (tpStart.CompareTo(tpEnd) == 0) {
                // Add an Image and Display Name text inside the Hyperlink
                double height = hyperlink.NavigateUri.IsFile ? DefaultThumbnailSize.Medium.Height : DefaultIconSize.Large.Height;
                double width = hyperlink.NavigateUri.IsFile ? DefaultThumbnailSize.Medium.Width : DefaultIconSize.Large.Width;
                AddImageToHyperLink(hyperlink, height, width, addText: true);
            }
            return hyperlink;
        }
        
        // Create an Image Element for use in Document
        Image CreateImage(FileInfo fi) {
            var image = new Image();
            try {
                var temp = Path.GetTempFileName();
                File.Copy(fi.FullName, temp, true);
                var bitmap = new BitmapImage(new Uri(temp));
                image.Source = bitmap;
                image.Width = Math.Min(bitmap.PixelWidth, Width);
                image.Height = Math.Min(bitmap.PixelHeight, Height);
                if (bitmap.PixelWidth > Width || bitmap.PixelHeight > Height) {
                    image.SetBinding(WidthProperty, "{Binding ActualWidth,  Mode=OneWay, ElementName=uxRichTextBox}");
                    image.SetBinding(HeightProperty, "{Binding ActualHeight, Mode=OneWay, ElementName=uxRichTextBox}");
                }
                image.ToolTip = fi.FullName; image.Tag = fi;
            } catch (Exception ex) { U.Exceptions.LogException(ex); }
            return image;
        }

        static void AddImageToHyperLink(Hyperlink hyperlink, double height, double width, bool addText = false) {

            var uri = hyperlink.NavigateUri;
            var name = !uri.IsFile ? System.Net.WebUtility.UrlDecode(uri.PathAndQuery) : new FileInfo(uri.LocalPath).Name;

            // Create a new image with given height and width
            var image = new Image {
                // ToolTip object is used for xaml Image Style for FilePathToThumbNailConverter to display image as thumbnail
                ToolTip = !uri.IsFile ? System.Net.WebUtility.UrlDecode(uri.OriginalString) : uri.LocalPath,
                // Tag object is used to scale factor for LayoutTransform of the thumbnail image @See NoteViewer.xaml
                Tag = 1.0d,
                Height = height,
                Width = width,
            };

            // Add an image and name text for the Hyperlink
            if (addText) { hyperlink.Inlines.Add($"{ name} "); }
            InlineUIContainer iluic = new InlineUIContainer(image);
            hyperlink.Inlines.InsertBefore(hyperlink.Inlines.FirstInline, iluic);
            if (addText) { iluic.ElementEnd.InsertLineBreak(); }
            hyperlink.Tag = image;
        }

        public void OnMediaElement_MediaEnded(object sender, RoutedEventArgs e) {
            if (sender is MediaElement me) {
                me.Position = TimeSpan.FromSeconds(0);
            }
        }

        public void OnMediaElement_MediaOpened(object sender, RoutedEventArgs e) {
            if (sender is MediaElement me && me.Position == TimeSpan.FromSeconds(0)) {
                me.Position = TimeSpan.FromSeconds(0);
            }
        }

        public void OnMediaElement_MediaFailed(object sender, ExceptionRoutedEventArgs e) {
            if (sender is MediaElement me) {
                // Ignore {System.Runtime.InteropServices.COMException(0xC00D109B): 0xC00D109B}
                if (e.ErrorException.HResult != -1072885605) {   // Erroneous error before MediaOpen is OK
                    var error = $"{STR("strMediaFailedMsg")} {me.Source} : ";
                    var iuic = me.Parent as InlineUIContainer;
                    iuic.ContentStart.Paragraph.Inlines.Add(new Run($"{error} {e.ErrorException.Message}"));
                    //U.Exceptions.LogException(e.ErrorException, error);
                }
            }
        }

        // Image Element was Loaded
        public void OnImageElement_Loaded(object sender, RoutedEventArgs e) {
            if (sender is Image im) {
                // Images copy the source uri into a temp file to prevent file locking
                // If the temp file is no longer available, try recreating with original uri
                if (im.Source == null && im.Tag is Uri uri) {
                    if (File.Exists(uri.AbsolutePath)) {
                        var i = CreateImage(new FileInfo(uri.AbsolutePath));
                        im.Source = i.Source;
                    } else {
                        // Inform the user that the image file no longer found
                        var error = $"{STR("strImageFailedMsg")} {uri.AbsolutePath}";
                        var iuic = im.Parent as InlineUIContainer;
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
                Shell.ShellOpen(uriPath);
                e.Handled = true;
            }
        }

        public void OnHyperlink_MouseDown(object sender, MouseButtonEventArgs e) {
            Debug.WriteLine($"OnHyperlink_MouseDown for {e.Source}");
            if (sender is Hyperlink hyperlink && e.MouseDevice.LeftButton == MouseButtonState.Pressed) {
                OnHyperlink_RequestNavigate(sender, new RequestNavigateEventArgs(hyperlink.NavigateUri, hyperlink.Name));
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
                    var inlines = new ArrayList(hyperlink.Inlines); // Clone to safely iterate
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
        
        // Set the Note Title from given file if NOT already set
        public void SetNoteTitle(FileInfo fi) {
            if (VM.Note.Title.Contains("New Note", StringComparison.InvariantCultureIgnoreCase)) {
                VM.Note.Title = fi.Name; Title = fi.Name;
                uxNoteTitleLabel.Content = fi.Name;
                uxSettingsPropertyGrid.Update();
            }
        }
    }
}