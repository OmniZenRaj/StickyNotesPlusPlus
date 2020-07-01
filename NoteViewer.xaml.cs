using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Diagnostics;

using OmniZenNotes.Models;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;
using MS.WindowsAPICodePack.Internal;
using System.Windows.Threading;
using Xceed.Wpf.Toolkit.PropertyGrid;

using U = Utilities;
using System.Windows.Navigation;
using System.Windows.Interop;

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


#pragma warning disable IDE1006 // Ignore name rule violation for XAML element objects starting with ux

namespace OmniZenNotes
{
    using MessageBox = Xceed.Wpf.Toolkit.MessageBox;
    using S = Properties.Settings;

    public partial class NoteViewer : Window
    {
        public NoteViewModel VM { get; set; }
        DispatcherTimer Timer = new DispatcherTimer();

        public static List<NoteViewer> NoteViewers = new List<NoteViewer>();

        public NoteViewer(Note note) {
            InitializeComponent();
            VM = new NoteViewModel(this, note);

            MouseEnter += OnWindow_MouseEnter;
            MouseLeave += OnWindow_MouseLeave;

            AddCommandBinding(ApplicationCommands.Save, OnSaveCommand);
            InputBindings.Add(new KeyBinding(ApplicationCommands.Save, new KeyGesture(Key.S, ModifierKeys.Control)));

            LoadSettings();
        }
        
        void PositionWindow(Rect restoreBounds)
        {
            var rect = CalcWindowBounds(restoreBounds);
            Width = rect.Width;
            Height = rect.Height;
            Left = rect.Left;
            Top = rect.Top;
        }

        Rect CalcWindowBounds(Rect restoreBounds) {
            var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
            var area = screen.WorkingArea;

            double width = restoreBounds.Width > 20 ? restoreBounds.Width : area.Width / 2.25;
            double height = restoreBounds.Height > 20 ? restoreBounds.Height : area.Height / 5.5;
            double left = restoreBounds.Left > area.Left ? restoreBounds.Left : area.Width / 2 - Width / 2; // Center Horz
            double top = restoreBounds.Top > area.Top ? restoreBounds.Top : area.Height / 2 - Height / 2;  // Center Vert

            return new Rect(left,top, width, height);;
        }

        void AddCommandBinding(ICommand command, ExecutedRoutedEventHandler handler, CanExecuteRoutedEventHandler enabler = null) {
            CommandBinding cb = new CommandBinding(command);
            cb.Executed += new ExecutedRoutedEventHandler(handler);
            // Default can execute to always true unless otherwise provied.
            cb.CanExecute += new CanExecuteRoutedEventHandler(enabler ??= (sender, e) => e.CanExecute = true);
            CommandBindings.Add(cb);
        }

        private void OnLoaded(object sender, EventArgs e) {
            DataContext = VM.Note;
            NoteViewers.Add(this);
            
            uxInfoPropertyGrid.SelectedObject = VM.Note;
            uxAlertPropertyGrid.SelectedObject = VM.Note.Task;
            uxRichTextBox.Document = VM.Note.Document;
            UpdatePinTabButton();
            PositionWindow(RestoreBounds);
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e) {
            Save(saveAsync: false);            
        }

        void OnSaveCommand(object sender, RoutedEventArgs e) {
            Save(saveAsync: false);
        }

        private void Save(bool saveAsync = true) {
            SaveSettings();
            if (VM.Note != null) {
                VM.Note.Save(saveAsync);
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape || (e.Key == Key.F4 && Keyboard.Modifiers == ModifierKeys.Alt)) { Close(); }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e) {
            if (Keyboard.Modifiers == ModifierKeys.Control) {
                FontSize = e.Delta > 0 ? FontSize + 1 : FontSize - 1;
            }
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e) {

            uxToolBar.Visibility = Visibility.Visible;

            if (e.LeftButton == MouseButtonState.Pressed) {
                DragMove();
            }
        }

        private void OnActivated(object sender, EventArgs e) {
            uxToolBar.Visibility = Visibility.Visible;
        }

        private void OnDeactivated(object sender, EventArgs e) {
            uxToolBar.Visibility = Visibility.Hidden;
        }

        private void OnWindow_MouseEnter(object sender, EventArgs e) {
            uxToolBar.Visibility = Visibility.Visible;

        }

        private void OnWindow_MouseLeave(object sender, EventArgs e) {
            if (!IsActive) {
                uxToolBar.Visibility = Visibility.Hidden;
            }
        }

        private void OnButton_MouseEnter(object sender, EventArgs e) {

        }

        private void OnButton_MouseLeave(object sender, EventArgs e) {

        }

        #region UX Control Event Handlers

        private void OnTextFormatButton_Click(object sender, RoutedEventArgs e) {

            using var fd = new System.Windows.Forms.FontDialog();

            fd.Font = new System.Drawing.Font(GetFamilyFontName(uxRichTextBox.FontFamily), (float)uxRichTextBox.FontSize); ;
            if (uxRichTextBox.Foreground is SolidColorBrush scb) {
                fd.Color = System.Drawing.Color.FromArgb(scb.Color.A, scb.Color.R, scb.Color.G, scb.Color.B);
                fd.ShowColor = true;
            }

            var dr = fd.ShowDialog();
            if (dr == System.Windows.Forms.DialogResult.OK) {
                var fontFamily = new FontFamily(fd.Font.FontFamily.ToString());
                var fontColor = Color.FromArgb(fd.Color.A, fd.Color.R, fd.Color.G, fd.Color.B);
                var fontSize = fd.Font.Size;
                var fsConverter = new FontStyleConverter();

                //var fontStyle = (FontStyle)fsConverter.ConvertFrom(fd.Font.Style.ToString());

                SetFont(fontFamily, fontSize, fontColor, FontStyle);
                uxInfoPropertyGrid.Update();
            }
        }


        private void SetFont(FontFamily fontFamily, double fontSize, Color fontColor, FontStyle fontStyle) {

            // Keep the Window Font in sync with the RichTextBox Font:
            if (fontFamily != null) {

                VM.Note.UXSettings.FontFamily = fontFamily;
                VM.Note.UXSettings.FontSize = fontSize;
                VM.Note.UXSettings.FontColor = fontColor;
                VM.Note.UXSettings.FontStyle = fontStyle;

                // Set the Font settings for the RichTextBox:
                uxRichTextBox.FontFamily = fontFamily;
                uxRichTextBox.FontSize = fontSize;
                uxRichTextBox.Foreground = new SolidColorBrush(fontColor);
                uxRichTextBox.FontStyle = fontStyle;

                // Create a TextRange around the entire document.
                var doc = uxRichTextBox.Document;
                TextRange range = uxRichTextBox.Selection;
                if (string.IsNullOrEmpty(uxRichTextBox.Selection.Text))
                {
                    // Create a TextRange around the entire document.
                    range = new TextRange(doc.ContentStart, doc.ContentEnd);
                    range.Select(range.Start, range.End);
                }                

                // Now apply the various font properties
                range.ApplyPropertyValue(FlowDocument.FontSizeProperty, fontSize);
                range.ApplyPropertyValue(FlowDocument.FontStyleProperty, fontStyle.ToString());
                range.ApplyPropertyValue(FlowDocument.ForegroundProperty, fontColor.ToString());
                range.ApplyPropertyValue(FlowDocument.FontFamilyProperty, GetFamilyFontName(fontFamily));

            }

            if (fontColor != null) {
                uxRichTextBox.Foreground = new SolidColorBrush(fontColor);
            }
        }

        // Deal with font family property carefully converting from fontFamily to string name
        private string GetFamilyFontName(FontFamily fontFamily) {
            string fontName = fontFamily.Source;
            if (fontName.IndexOf("=") > 0 && fontName.IndexOf("]") > 1) {
                fontName = fontName.Substring(fontName.IndexOf("=") + 1, fontName.IndexOf("]") - fontName.IndexOf("=") - 1);
            }

            return fontName;
        }

        private void OnFillBackgroundButton_Click(object sender, RoutedEventArgs e) {

            using var cd = new System.Windows.Forms.ColorDialog();
            if( uxRichTextBox.Background is SolidColorBrush scb) {
                cd.Color = System.Drawing.Color.FromArgb(scb.Color.A, scb.Color.R, scb.Color.G, scb.Color.B);
            }

            var dr = cd.ShowDialog();
            if (dr == System.Windows.Forms.DialogResult.OK) {
                Color color = Color.FromArgb(cd.Color.A, cd.Color.R, cd.Color.G, cd.Color.B);
                SetBackgroundColor(color);
                uxInfoPropertyGrid.Update();
            }
        }

        private void SetBackgroundColor(Color color) {
            VM.Note.UXSettings.BackgroundColor = color;
            uxColorPicker.SelectedColor = color;

            Background = new SolidColorBrush(color);
            uxRichTextBox.Background = new SolidColorBrush(color);

            // Tweak the textbox color for a nice background accent color for the toolbar.
            U.Graphics.RgbToHls(color.R, color.G, color.B, out double h, out double l, out double s);
            if (l > 0.50f) l *= 0.25f; else if (l > 0.35f) l *= 0.35f; else if (l > 0.25f) l *= 1.25f; else if (l > 0.00f) l *= 2.00f;  else l = 0.35f;
            if (l < 0.35f) s *= 0.50f; else if (l < 0.35f) s *= 0.75f;

            U.Graphics.HlsToRgb(h, l, s, out int r, out int g, out int b);
            float scA = color.A / 255.0f, scR = r / 255.0f, scG = g / 255.0f, scB = b / 255.0f;
            var colorScRgb = Color.FromScRgb(scA, scR, scG, scB);

            Background = new SolidColorBrush(colorScRgb);
            uxToolBar.Background = Background;
            uxRichTextBox.SelectionBrush = new SolidColorBrush(colorScRgb);

        }

        private void uxRichTextBox_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (Keyboard.Modifiers == ModifierKeys.Control) {
                FontSize = e.Delta > 0 ? FontSize + 1 : FontSize - 1;
            }
        }

        private void OpenNewWindow(Note note) {
            var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
            var area = screen.WorkingArea;

            // Create new NoteViewer at same size & slightly to left & lower than this one
            var noteViewer = new NoteViewer(note);
            noteViewer.Left = Left > area.Left ? Left >= 0 ? Left + (area.Width*0.02) : Left - (area.Width* 0.02) : Left + (area.Width * 0.02);
            noteViewer.Top = Top > area.Top ? Top >= 0 ? Top + (area.Height* 0.03) : Top - (area.Height* 0.03) : (area.Height * 0.03);
            noteViewer.Width = Width;
            noteViewer.Height = Height;
            noteViewer.Show();        
        }

        private void OnAddNoteButton_Click(object sender, RoutedEventArgs e) {
            OpenNewWindow(VM.CreateNewNote());
        }

        private void OnDelNoteButton_Click(object sender, RoutedEventArgs e) {
            VM.Note.Delete();
            VM.Note = null;
            Close();
        }

        private void OnAlertsButton_Click(object sender, RoutedEventArgs e) {
            uxAlertPanel.Visibility = uxAlertPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnInfoButton_Click(object sender, RoutedEventArgs e) {
            uxInfoPanel.Visibility = uxInfoPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnPinTabButton_Click(object sender, RoutedEventArgs e)
        {
            Topmost = !Topmost;
            UpdatePinTabButton();
        }        

        private void UpdatePinTabButton() {
            Image image = uxPinTab.Content as Image;
            image.RenderTransform = new RotateTransform(Topmost ? 0 : 90);
            image.RenderTransformOrigin = new Point(0.5, 0.5);

            VM.Note.UXSettings.Topmost = Topmost;
            uxInfoPropertyGrid.Update();            
        }

#pragma warning disable IDE0051

        private void SaveToFile() {
            var sfd = new System.Windows.Forms.SaveFileDialog {
                Filter = "XAML Files (*.xaml)|*.xaml|RichText Files (*.rtf)|*.rtf|All Files (*.*)|*.*"
            };

            var dr = sfd.ShowDialog();
            if (dr == System.Windows.Forms.DialogResult.OK) {
                TextRange range = new TextRange( uxRichTextBox.Document.ContentStart, uxRichTextBox.Document.ContentEnd);
                using FileStream fs = File.Create(sfd.FileName);
                range.Save(fs, DataFormats.XamlPackage);
            }
        }

        private void LoadFromFile()
        {
            var ofd = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "XAML Files (*.xaml)|*.xaml|RichText Files (*.rtf)|*.rtf|All Files (*.*)|*.*"
            };

            var dr = ofd.ShowDialog();
            if (dr == System.Windows.Forms.DialogResult.OK)
            {
                TextRange range = new TextRange(uxRichTextBox.Document.ContentStart, uxRichTextBox.Document.ContentEnd);
                using FileStream fs = File.OpenRead(ofd.FileName);
                range.Load(fs, DataFormats.XamlPackage);
            }
        }

        private void Print() {
            // Create a PrintDialog.
            var printDialog = new PrintDialog();

            // Show the dialog and print the document if successful
            if (printDialog.ShowDialog() == true) {
                printDialog.PrintDocument((((IDocumentPaginatorSource)uxRichTextBox.Document).DocumentPaginator),$"Printing ");
            }
        }

        #endregion

        #region Settings & Configuration

        // Loaded from App Settings located @ C:\User\{User}\AppData\Local\OmniZenNotes\OmniZenNote.exe_...
        private void LoadSettings()
        {
            try
            {
                // Restore Window position and size from user settings save of last session
                if (S.Default?.RestoreBounds is Rect restoreBounds)
                {
                    Left = restoreBounds.Left; Top = restoreBounds.Top;
                    Width = restoreBounds.Width; Height = restoreBounds.Height;
                }
                // Restore the Window State (minimized gets converted to be Normal to avoid user not seeing it)
                WindowState = S.Default?.WindowState is WindowState windowState ? windowState : System.Windows.WindowState.Normal;
                WindowState = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
                SetFont(S.Default.Font, S.Default.FontSize, S.Default.FontColor, FontStyle);
                uxColorPicker.SelectedColor = S.Default.BackgroundColor;
                SetBackgroundColor(S.Default.BackgroundColor);

                uxOptionsExpander.IsExpanded = S.Default.OptionsExpanded;
                Topmost = S.Default.Topmost;

                // Auto Save Settings
                if (S.Default.AutoSave is int seconds && seconds > 0)
                {
                    Timer = new DispatcherTimer();
                    Timer.Tick += new EventHandler((sender, e) => Save(saveAsync: true));
                    Timer.Interval = TimeSpan.FromSeconds(seconds);
                    Timer.Start();
                }

                // Restore the Note specific settings (which override the App level settings)
                if (VM.Note.UXSettings.RestoreBounds is Rect restoreBoundz)
                {
                    Left = restoreBoundz.Left; Top = restoreBoundz.Top;
                    Width = restoreBoundz.Width; Height = restoreBoundz.Height;
                }
                // Restore the Window State (minimized gets converted to be Normal to avoid user not seeing it)
                SetFont(VM.Note.UXSettings.FontFamily, VM.Note.UXSettings.FontSize, VM.Note.UXSettings.FontColor, FontStyle);
                uxColorPicker.SelectedColor = VM.Note.UXSettings.BackgroundColor;
                SetBackgroundColor(VM.Note.UXSettings.BackgroundColor);
                uxOptionsExpander.IsExpanded = VM.Note.UXSettings.OptionsExpanded;
                Topmost = VM.Note.UXSettings.Topmost;
            }
            catch
            {
                Left = 1; Top = 1; Width = 480; Height = 480;
            }
        }

        private void SaveUXSettings() {
            // Save the Note specific settings (which override the App level settings)
            if (VM.Note != null)
            {
                VM.Note.UXSettings ??= new UXSettings();
                VM.Note.UXSettings.RestoreBounds = RestoreBounds;
                VM.Note.UXSettings.WindowState = WindowState;
                VM.Note.UXSettings.FontFamily = FontFamily;
                VM.Note.UXSettings.FontSize = FontSize;
                if (uxRichTextBox.Foreground is SolidColorBrush fgscb)
                {
                    VM.Note.UXSettings.FontColor = fgscb.Color;
                }
                if (uxRichTextBox.Background is SolidColorBrush scba)
                {
                    VM.Note.UXSettings.BackgroundColor = scba.Color;
                }
                VM.Note.UXSettings.OptionsExpanded = uxOptionsExpander.IsExpanded;
                VM.Note.UXSettings.Topmost = Topmost;
            }
        }

        private void SaveSettings()
        {
            SaveUXSettings();

            // Save the App wide default settings:
            S.Default.RestoreBounds = RestoreBounds;
            S.Default.WindowState = WindowState;
            S.Default.Font = FontFamily;
            S.Default.FontSize = FontSize;

            if (uxRichTextBox.Foreground is SolidColorBrush fgscb2)
            {
                S.Default.FontColor = fgscb2.Color;
            }

            if (uxRichTextBox.Background is SolidColorBrush scb)
            {
                S.Default.BackgroundColor = scb.Color;
            }

            S.Default.OptionsExpanded = uxOptionsExpander.IsExpanded;
            S.Default.Topmost = Topmost;

            S.Default.Save();
        }

        #endregion

        private void OnRichTextBox_TextChanged(object sender, TextChangedEventArgs e) {
        }

        private void uxColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) {
            SetBackgroundColor((Color)e.NewValue);
            uxInfoPropertyGrid.Update();            
        }

        private void uxAlertPanel_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                uxAlertPanel.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        private void uxInfoPanel_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                uxInfoPanel.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        private void uxAlertPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (e.NewValue is bool visible && visible) {
                uxInfoPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void uxInfoPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (e.NewValue is bool visible && visible) {
                uxAlertPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void uxAlertPropertyGrid_PropertyValueChanged(object sender, Xceed.Wpf.Toolkit.PropertyGrid.PropertyValueChangedEventArgs e) {
        }

        private void uxInfoPropertyGrid_PropertyValueChanged(object sender, PropertyValueChangedEventArgs e) {
            if (e.OriginalSource is PropertyItem item) {
                Color foregroundColor = Colors.Black;
                if (uxRichTextBox.Foreground is SolidColorBrush scba)
                {
                    foregroundColor = scba.Color;
                }

                switch(item.PropertyName) {
                    case "BackgroundColor" :
                        SetBackgroundColor((Color)e.NewValue);
                        break;
                    case "FontFamily":
                        FontFamily = (FontFamily)e.NewValue;
                        SetFont(uxRichTextBox.FontFamily, uxRichTextBox.FontSize, foregroundColor, uxRichTextBox.FontStyle);
                        break;
                    case "FontSize":
                        SetFont(uxRichTextBox.FontFamily, (double)e.NewValue, foregroundColor, uxRichTextBox.FontStyle);
                        break;
                    case "FontColor":
                        SetFont(uxRichTextBox.FontFamily, uxRichTextBox.FontSize, (Color)e.NewValue, uxRichTextBox.FontStyle);
                        break;
                    case "FontStyle":
                        SetFont(uxRichTextBox.FontFamily, uxRichTextBox.FontSize, foregroundColor, (FontStyle)e.NewValue);                    
                        break;
                    default: break;
                }
            }
        }

        private void uxRichTextBox_PreviewDragOver(object sender, DragEventArgs args)
        {
            args.Effects = args.KeyStates == (DragDropKeyStates.LeftMouseButton | DragDropKeyStates.ControlKey) ? DragDropEffects.Copy : DragDropEffects.Link;
            args.Handled = true;
        }

        private void uxRichTextBox_PreviewDrop(object sender, DragEventArgs args)
        {
            args.Handled = true;

            var fileName = IsSingleFileOrDir(args);
            if (fileName == null) return;
            var fileInfo = new FileInfo(fileName);
            FlowDocument doc = uxRichTextBox.Document;
            TextRange tr = new TextRange(doc.ContentStart, doc.ContentEnd);
            TextPointer tp = uxRichTextBox.CaretPosition;

            if (args.KeyStates == DragDropKeyStates.ControlKey) {

                // Insert the contents of supported dropped file:
                switch (fileInfo.Extension.ToLower()) {
                    case ".text": case ".txt":
                    {
                        using var fileToLoad = new StreamReader(fileInfo.FullName);
                        uxRichTextBox.AppendText(fileToLoad.ReadToEnd());
                        fileToLoad.Close();
                        break;
                     };
                    case ".png": case ".jpg": case ".bmp": {
                        var image = new Image();
                        var bitmap = new BitmapImage(new Uri(fileInfo.FullName));
                        image.Source = bitmap;
                        image.Width = Math.Min(bitmap.PixelWidth, Width);
                        image.Height = Math.Min(bitmap.PixelHeight, Height);
                        if (bitmap.PixelWidth > Width || bitmap.PixelHeight > Height)
                        {
                            image.SetBinding(WidthProperty, "{Binding ActualWidth,  Mode=OneWay, ElementName=uxRichTextBox}");
                            image.SetBinding(HeightProperty, "{Binding ActualHeight, Mode=OneWay, ElementName=uxRichTextBox}");
                        }
                        var iuic_image = new InlineUIContainer(image, tp);
                        break;
                    }                    
                    case ".mp4" : case ".mpg": case ".mp3":case ".wmv": case ".avi": case ".mkv": {
                        var me = new MediaElement { Source = new Uri(fileInfo.FullName) };
                        if (me.HasVideo) {
                            me.Width = Math.Min(me.NaturalVideoWidth, Width);
                            me.Height = Math.Min(me.NaturalVideoHeight, Height);
                        }
                        var iuic_me = new InlineUIContainer(me, tp);
                        break;
                    }
                }
            } else if (args.KeyStates == DragDropKeyStates.AltKey)
            {
                // Insert the contents of supported dropped file:
                switch (fileInfo.Extension.ToLower())
                {
                    case ".png": case ".jpg": case ".bmp": {
                            var bitmap = new BitmapImage(new Uri(fileInfo.FullName));
                            uxRichTextBox.Document.Background = new ImageBrush(bitmap);
                            break; 
                    }
                }
            } else {
                // Create a Hyperlink to the dropped file
                var image = new Image();
                var icon = U.Shell.GetShellIcon(fileInfo);
                var bitmap = U.Graphics.GetBitmapImage(icon);
                image.Source = bitmap;
                image.Height = bitmap.PixelHeight;
                image.Width = bitmap.PixelWidth;

                var hyperLink = new Hyperlink(new Run(" "), tp)
                {
                    NavigateUri = new Uri(fileInfo.FullName),
                    ToolTip = $@"Click to open {fileInfo.FullName}",
                    IsEnabled = true
                };

                hyperLink.Inlines.Add(image);
                hyperLink.Inlines.Add(new Run($"{fileInfo.Name} "));
                hyperLink.RequestNavigate += (object sender, RequestNavigateEventArgs e) =>
                {
                    Debug.WriteLine($"HyperLink RequestNavigate to {e.Uri}");
                };
            }
        }

        private string IsSingleFileOrDir(DragEventArgs args)
        {
            // Check for files in the hovering data object.
            if (args.Data.GetDataPresent(DataFormats.FileDrop, true))
            {
                var fileNames = args.Data.GetData(DataFormats.FileDrop, true) as string[];
                if (fileNames?.Length is 1)
                {
                    if (File.Exists(fileNames[0]) || Directory.Exists(fileNames[0]))
                    {
                        return fileNames[0];
                    }
                }
            }
            return null;
        }        
    }
}