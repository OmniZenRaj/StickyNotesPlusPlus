using System.IO;
using System.Windows;

namespace OmniZenNotes
{
    public partial class NoteViewer : Window
    {
        void uxRichTextBox_PreviewDragOver(object sender, DragEventArgs args) {
            args.Effects = args.KeyStates == (DragDropKeyStates.LeftMouseButton | DragDropKeyStates.ControlKey) ? DragDropEffects.Copy : DragDropEffects.Link;
            args.Handled = true;
        }

        void uxRichTextBox_PreviewDrop(object sender, DragEventArgs args) {
            args.Handled = true;
            // Check for files in the hovering data object.
            if (args.Data.GetDataPresent(DataFormats.FileDrop, true)) {
                foreach (var file in args.Data.GetData(DataFormats.FileDrop, true) as string[]) {
                    try {
                        DropFile(new FileInfo(file), args);
                    } catch { }
                }
            }

            // Create a Hyperlink to given URL (happens when dragging URL from Chrome)
            if (args.Data.GetDataPresent(DataFormats.Text, true)) {
                string uri = args.Data.GetData(DataFormats.Text, true) as string;
                CreateHyperLink(uri);
            }

            // Handle File based Drag & Drop Operation:
            void DropFile(FileInfo fi, DragEventArgs args) {
                // Insert the contents of supported dropped file:
                if (args.KeyStates == DragDropKeyStates.ControlKey) {
                    switch (fi.Extension.ToLower()) {
                        // Create an MediaElement and set the Source to the dropped file path
                        case ".png":
                        case ".jpg":
                        case ".jpeg":
                        case ".gif":
                        case ".bmp":
                        case ".tiff":
                        case ".ico":
                        case ".mp4":
                        case ".mpg":
                        case ".mp3":
                        case ".wma":
                        case ".wmv":
                        case ".avi":
                        case ".mkv":
                            // Create an MediaElement and set the Source to the given file
                            CreateMediaElement(fi);
                            break;
                        default: {
                            // Try to insert it into the Note as Text:
                            InsertTextFromFile(fi);
                            break;
                            }
                    }
                } else if (args.KeyStates == DragDropKeyStates.AltKey) {
                    // Set the Document background from dropped file:
                    switch (fi.Extension.ToLower()) {
                        case ".png":
                        case ".jpg":
                        case ".jpeg":
                        case ".gif":
                        case ".bmp":
                        case ".tiff":
                        case ".ico":
                            SetDocumentBackground(fi);
                            break;
                    }
                } else {
                    // Create Hyperlink to dropped file/folder/URL
                    CreateHyperLink(fi);
                }
                // Set the Note Title to the Dropped file's name:
                SetNoteTitle(fi);
            }
        }
    }
}