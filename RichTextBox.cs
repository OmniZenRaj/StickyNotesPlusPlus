using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Interop;
using System.Windows.Markup;
using Microsoft.AspNetCore.SignalR.Client;
using System.Windows.Media.Imaging;
using System.Net.Http;
using System.Windows.Controls.Primitives;
using Xceed.Wpf.Toolkit;

namespace OmniZenNotes;

public class RichTextBox : Xceed.Wpf.Toolkit.RichTextBox
{
    NoteViewer NoteViewer { get { return GetNoteViewer();} }
    readonly FlowDocument FlowDocument;

    public RichTextBox() :base() { }
    public RichTextBox(FlowDocument document) : base(document) {
        FlowDocument = document;
    }    
    

    NoteViewer GetNoteViewer() {
        var parent = Parent;
        while (parent is FrameworkElement p)
        if ( p is NoteViewer nv) {
            return nv;
        } else {
            parent = p.Parent;
        }
        return null;
    }
    
    protected override void OnTextChanged(TextChangedEventArgs e) {
        if (Document.ContentEnd == Document.ContentStart) {
            Console.WriteLine(e.ToString());
        }
    }
    
    protected override void OnMouseWheel(MouseWheelEventArgs e) {
        if (Keyboard.Modifiers == ModifierKeys.Control) {
            if (! Selection.IsEmpty) {
                double fontSize = (double)Selection.GetPropertyValue(FontSizeProperty);
                fontSize =  Math.Max(e.Delta > 0 ? fontSize + 1 : fontSize - 1, 6);
                Selection.ApplyPropertyValue(FontSizeProperty, fontSize);
            } else {
                double fontSize = Math.Max(e.Delta > 0 ? FontSize + 1 : FontSize - 1, 6);
                if (Foreground is SolidColorBrush scb) {
                    SetFont(FontFamily, fontSize, scb.Color, FontStyle);
                }
            }
        }
    }

    protected override void OnPreviewDragOver(DragEventArgs args) {
        args.Effects = args.KeyStates == (DragDropKeyStates.LeftMouseButton | DragDropKeyStates.ControlKey) ? DragDropEffects.Copy : DragDropEffects.Link;
        args.Handled = true;
    }

    protected override void OnPreviewDrop(DragEventArgs args) {
        args.Handled = true;
        // Check for files in the hovering data object.
        if (args.Data.GetDataPresent(DataFormats.FileDrop, true)) {
            foreach (var file in args.Data.GetData(DataFormats.FileDrop, true) as string[]) {
                try {
                    DropFile(new(file), args);
                } catch { }
            }
        }

        // Create a Hyperlink to given URL (happens when dragging URL from Chrome)
        if (args.Data.GetDataPresent(DataFormats.Text, true)) {
            string uri = args.Data.GetData(DataFormats.Text, true) as string;
            NoteViewer.CreateHyperLink(uri, CaretPosition, Selection);
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
                        FlowDocument.CreateMediaElement(fi, CaretPosition);
                        break;
                    default: {
                            // Try to insert it into the Note as Text:
                            FlowDocument.InsertTextFromFile(fi, CaretPosition);
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
                        NoteViewer.SetDocumentBackground(fi, Document);
                        break;
                }
            } else {
                // Create Hyperlink to dropped file/folder/URL
                NoteViewer.CreateHyperLink(fi, CaretPosition);
            }
            // Set the Note Title to the Dropped file's name:
            NoteViewer.SetNoteTitle(fi);
        }
    }    
    
    public void SetFont(FontFamily fontFamily, double fontSize, Color fontColor, FontStyle fontStyle, bool updateUXSettings = true) {
        // Keep the Window Font in sync with the RichTextBox Font:
        if (fontFamily != null) {
            var doc = Document;

            // Create a TextRange for the Selected Text or the entire document.
            TextRange range = Selection;
            if (string.IsNullOrEmpty(Selection.Text)) {
                range = new(doc.ContentStart, doc.ContentEnd);
                range.Select(range.Start, range.End);
            }

            // Set the Font for the Selected Text or the whole RichTextBox:
            if (!range.IsEmpty) {
                range.ApplyPropertyValue(FlowDocument.FontSizeProperty, fontSize);
                range.ApplyPropertyValue(FlowDocument.FontStyleProperty, fontStyle.ToString());
                range.ApplyPropertyValue(FlowDocument.ForegroundProperty, fontColor.ToString());
                range.ApplyPropertyValue(FlowDocument.FontFamilyProperty, U.Graphics.GetFamilyFontName(fontFamily));
            }

            // Set the Font for the whole RichTextBox when no Text was selected
            if (string.IsNullOrEmpty(Selection?.Text)) {
                FontFamily = fontFamily;
                FontSize = fontSize;
                Foreground = new SolidColorBrush(fontColor);
                FontStyle = fontStyle;
                Foreground = new SolidColorBrush(fontColor);
            }
        }

        if (updateUXSettings && DataContext is Note note) {
            note.UXSettings.FontFamily = FontFamily;
            note.UXSettings.FontSize = FontSize;
            note.UXSettings.FontColor = (Foreground as SolidColorBrush).Color;
            note.UXSettings.FontStyle = FontStyle;
        }
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e) {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Enter) {
            SetParagraphProperties(Document, CaretPosition.Paragraph);
            string xaml = XamlWriter.Save(CaretPosition.Paragraph);
            SignalRClient.OnSendSignalR(GetNoteViewer(), xaml);
            base.OnPreviewKeyUp(e);
        }

        void SetParagraphProperties(System.Windows.Documents.FlowDocument doc, Paragraph p) {
            p.Background = doc.Background;
            p.FontFamily = doc.FontFamily;
            p.FontSize = doc.FontSize;
            p.FontStyle = doc.FontStyle;
            p.FontStretch = doc.FontStretch;
            p.FontWeight = doc.FontWeight;
            p.Foreground = doc.Foreground;
            if (DataContext is Note note) { p.Tag = note.ID; }
        }
    }
    
    /*     protected override Size MeasureOverride(Size constraint) {
            if (this.Parent is System.Windows.Controls.StackPanel sp) {
                OzRichTextBox rtb2 = sp.FindName("RichTextBox2") as OzRichTextBox;
                var h = rtb2.Width;
            }

            return constraint;
        }

        protected override Size ArrangeOverride(Size finalSize) {
            return finalSize;
        } */
}