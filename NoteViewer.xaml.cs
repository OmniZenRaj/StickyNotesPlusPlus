using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Threading;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Controls.Primitives;
using System.Globalization;

using Microsoft.WindowsAPICodePack.Shell;
using Xceed.Wpf.Toolkit.PropertyGrid;
using System.Windows.Navigation;

using OmniZenNotes.Models;
using U = Utilities;

#pragma warning disable IDE1006 // Ignore name rule violation for XAML element objects starting with ux

namespace OmniZenNotes
{
    using S = Properties.Settings;

    public partial class NoteViewer : Window
    {
        public static List<FontFamily> FontFamilies;
        public static List<Color> BackgroundColors;
        public static List<NoteViewer> NoteViewers = new List<NoteViewer>();

        public NoteViewModel VM { get; set; }
        DispatcherTimer Timer = new DispatcherTimer();

        public NoteViewer(Note note) {
            InitializeComponent();
            VM = new NoteViewModel(this, note);
            App.NoteViewers.Add(this);

            InitializeControls();
            InitializeCommands();

            LoadSettings();
        }

        void InitializeControls() {
            uxRichTextBox.IsDocumentEnabled = true;
            uxRichTextBox.SpellCheck.IsEnabled = false;
            RTBFB.IsEnabled = false;

            MouseEnter += OnWindow_MouseEnter;
            MouseLeave += OnWindow_MouseLeave;
            uxShowNotesMenu.SubmenuOpened += OnShowNote_SubmenuOpened;
            uxSelectColorMenu.SubmenuOpened += OnSelectColor_SubmenuOpened;
            uxSelectFontMenu.SubmenuOpened += OnSelectFont_SubmenuOpened;

            Title = VM.Note.Title;
            Image AppIcon = (Image)FindResource("AppIcon");
            Icon = AppIcon.Source;
        }

        private void OnSelectColor_SubmenuOpened(object sender, RoutedEventArgs e) {
            uxSelectColorMenu.Items.Clear();
            PropertyInfo[] props = typeof(Colors).GetProperties();
            foreach (PropertyInfo p in props) {
                Color color = (Color)p.GetValue(null);
                MenuItem item = new MenuItem {
                    Header = p.Name,
                    Tag = color,
                    Background = new SolidColorBrush(color),
                    Foreground = new SolidColorBrush(AdjustColor(color)),
                };

                // Handle color selection from auto generated submenu
                item.Click += (object sender, RoutedEventArgs e) => {
                    if (sender is MenuItem mi && mi.Tag is Color color) {
                        SetBackgroundColor(color);
                    }
                };

                uxSelectColorMenu.Items.Add(item);
            }
        }

        private void OnSelectFont_SubmenuOpened(object sender, RoutedEventArgs e) {
            uxSelectFontMenu.Items.Clear();

            // TODO: Add a Font... menu item to bring up Font Dialog box

            // Load the Font Families on first time 
            if (FontFamilies == null) {
                FontFamilies = new List<FontFamily>(Fonts.SystemFontFamilies);
                FontFamilies.Sort((FontFamily x, FontFamily y) => { return x.Source.CompareTo(y.Source); });
            }

            foreach (var fontFamily in FontFamilies) {
                MenuItem item = new MenuItem {
                    Header = fontFamily.Source,
                    Tag = fontFamily,
                    FontFamily = fontFamily
                };

                // Handle font selection from auto generated submenu
                item.Click += (object sender, RoutedEventArgs e) => {
                    if (sender is MenuItem mi && mi.Tag is FontFamily fontFamily) {
                        SetFont(fontFamily, uxRichTextBox.FontSize, (uxRichTextBox.Foreground as SolidColorBrush), uxRichTextBox.FontStyle);
                    }
                };

                uxSelectFontMenu.Items.Add(item);
            };
        }


        void OnShowNote_SubmenuOpened(object sender, RoutedEventArgs e) {
            uxShowNotesMenu.Items.Clear();
            uxShowNotesMenu.Items.Add(new MenuItem() {
                Header = "Show All",
                InputGestureText = "Alt-A",
            });
            uxShowNotesMenu.Items.Add(new MenuItem() {
                Header = "Show Private",
                InputGestureText = "Alt-R"
            });

            uxShowNotesMenu.Items.Add(new MenuItem() {
                Header = "Show Public",
                InputGestureText = "Alt-P"
            });

            uxShowNotesMenu.Items.Add(new Separator());

            App.NoteViewers.Sort((NoteViewer x, NoteViewer y) => { return x.Title.CompareTo(y.Title); });

            foreach (var noteViewer in App.NoteViewers) {
                MenuItem item = new MenuItem {
                    Header = noteViewer.Title,
                    Tag = noteViewer,
                    IsChecked = noteViewer.Visibility == Visibility.Visible
                };

                if (noteViewer.uxRichTextBox.Document.Background is null) {
                    if (noteViewer.uxRichTextBox.Background is null) {
                        item.Background = noteViewer.Background.Clone();
                    } else {
                        item.Background = noteViewer.uxRichTextBox.Background.Clone();
                    }
                } else {
                    item.Background = noteViewer.uxRichTextBox.Document.Background.Clone();
                }

                uxShowNotesMenu.Items.Add(item);
            }

            foreach (var item in uxShowNotesMenu.Items) {
                if (item is MenuItem menuItem) {
                    menuItem.Click += NoteSubMenu_Clicked;
                }
            }
        }

        // Handle menu checks and visibility processing
        void NoteSubMenu_Clicked(object sender, RoutedEventArgs e) {

            if (sender is MenuItem mi) {
                if (mi.Tag is NoteViewer nv) {
                    mi.IsChecked = !mi.IsChecked; // Toggle Checked status
                    if (mi.IsChecked) { nv.Show(); nv.Activate(); } else { nv.Hide(); }

                } else if ("Show All".CompareTo(mi.Header) == 0) {
                    mi.IsChecked = true;
                    foreach (var noteViewer in NoteViewers) {
                        noteViewer.Show();
                    }

                } else if ("Show Private".CompareTo(mi.Header) == 0) {
                    mi.IsChecked = true;
                    foreach (var noteViewer in NoteViewers) {
                        if (noteViewer.VM.Note.Security.Permissions == EntityPermissions.Private) {
                            noteViewer.Show();
                        } else {
                            noteViewer.Hide();
                        }
                    }

                } else if ("Show Public".CompareTo(mi.Header) == 0) {
                    mi.IsChecked = true;
                    foreach (var noteViewer in NoteViewers) {
                        if (noteViewer.VM.Note.Security.Permissions != EntityPermissions.Private) {
                            noteViewer.Show();
                        } else {
                            noteViewer.Hide();
                        }
                    }
                }
            }
        }

        void PositionWindow(Rect restoreBounds) {
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

            return new Rect(left, top, width, height); ;
        }

        // Create, configure and bind Application Commands
        void InitializeCommands() {
            // Refresh Command
            AddCommandBinding(NavigationCommands.Refresh, OnRefreshCommand);
            // New Command
            AddCommandBinding(ApplicationCommands.New, OnNewCommand);
            // Save Note
            AddCommandBinding(ApplicationCommands.Save, OnSaveCommand);
            // Full Screen Toggle
            AddCommandBinding(NavigationCommands.Zoom, OnZoomCommand);
            InputBindings.Add(new KeyBinding(NavigationCommands.Zoom, new KeyGesture(Key.F11, ModifierKeys.None, "F11")));

            // Print / Print Preview Note TODO: Not working - might need to conver to FixedDocument to print
            // AddCommandBinding(ApplicationCommands.Print, OnPrintCommand);
            // AddCommandBinding(ApplicationCommands.PrintPreview , OnPrintPreviewCommand);

            // Spellcheck Command
            AddCommandBinding(AppCommands.SpellCheckCommand, OnSpellCheckCommand);
            InputBindings.Add(new KeyBinding(AppCommands.SpellCheckCommand, new KeyGesture(Key.F7, ModifierKeys.None, "F7")));

            // Format Bar Command
            AddCommandBinding(AppCommands.FormatBarCommand, OnFormatBarCommand);
            InputBindings.Add(new KeyBinding(AppCommands.FormatBarCommand, new KeyGesture(Key.F4, ModifierKeys.None, "F4")));

            // Set Note Font Command
            AddCommandBinding(AppCommands.SelectFontCommand, OnSelectFontCommand);

            // View Note Reminder Command
            AddCommandBinding(AppCommands.ViewNoteReminderCommand, OnViewNoteReminderCommand);
            // View Note Settings Command
            AddCommandBinding(AppCommands.ViewNoteSettingsCommand, OnViewNoteSettingsCommand);

            // Delete Command
            AddCommandBinding(AppCommands.DeleteCommand, OnDeleteCommand);

            void AddCommandBinding(ICommand command, ExecutedRoutedEventHandler handler, CanExecuteRoutedEventHandler enabler = null) {
                CommandBinding cb = new CommandBinding(command);
                cb.Executed += new ExecutedRoutedEventHandler(handler);
                cb.CanExecute += new CanExecuteRoutedEventHandler(enabler ??= (sender, e) => e.CanExecute = true);
                CommandBindings.Add(cb);
            }

            AddCommandBinding(AppCommands.ExitApplicationCommand, OnExitApplicationCommand);

        }

        private void OnLoaded(object sender, EventArgs e) {
            DataContext = VM.Note;
            NoteViewers.Add(this);

            uxSettingsPropertyGrid.SelectedObject = VM.Note;
            uxReminderPropertyGrid.SelectedObject = VM.Note.Task;
            uxRichTextBox.Document = VM.Note.Document;
            UpdatePinTabButton();
            PositionWindow(RestoreBounds);
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (VM.Note != null) { Save(saveAsync: false); }
            App.NoteViewers.Remove(this);
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

        void OnRefreshCommand(object sender, RoutedEventArgs e) {
        }

        void OnSpellCheckCommand(object sender, RoutedEventArgs e) {
            uxRichTextBox.SpellCheck.IsEnabled = !uxRichTextBox.SpellCheck.IsEnabled;
        }

        void OnFormatBarCommand(object sender, RoutedEventArgs e) {
            RTBFB.IsEnabled = !RTBFB.IsEnabled;
            RTBFB.Visibility = RTBFB.IsEnabled ? Visibility.Visible : Visibility.Hidden;
        }

        void OnDeleteCommand(object sender, RoutedEventArgs e) {
            MessageBoxResult mbr = MessageBoxResult.OK;
            if (NoteViewers.Count == 1) {
                mbr = MessageBox.Show("You about to DELETE the LAST Sticky Note. \nThis will EXIT the Application in Alpha. \n\nPress Cancel to go back.", Assembly.GetExecutingAssembly().GetName().Name, MessageBoxButton.OKCancel);
            }

            if (mbr == MessageBoxResult.OK) {
                VM.Note.Delete();
                VM.Note = null;
                Close();
            }
            e.Handled = true;
        }

        void OnExitApplicationCommand(object sender, RoutedEventArgs e) {
            Application.Current.Shutdown();
        }

        void OnSelectFontCommand(object sender, RoutedEventArgs e) {
            OnTextFormatButton_Click(sender, e);
        }

        void OnViewNoteReminderCommand(object sender, RoutedEventArgs e) {
            ShowReminderPanel(!uxViewNoteReminderMenuItem.IsChecked);
        }
        void OnViewNoteSettingsCommand(object sender, RoutedEventArgs e) {
            ShowSettingsPanel(!uxViewNoteSettingsMenuItem.IsChecked);
        }

        private void OnKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Alt) {
                if (NoteViewers.Count == 1) {
                    MessageBox.Show("You cannot HIDE the LAST Sticky Note in Alpha. \n\nUse Alt-F4 to Exit Application", Assembly.GetExecutingAssembly().GetName().Name);
                } else {
                    Visibility = Visibility.Hidden;
                }
                e.Handled = true;
            }
        }

        private void OnRichTextBox_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (Keyboard.Modifiers == ModifierKeys.Control) {
                // TODO: Make Text Selections work
                TextRange range = uxRichTextBox.Selection;
                if (!string.IsNullOrEmpty(range.Text)) {
                    var para = range.Start.Paragraph;
                    para.FontSize = Math.Max(e.Delta > 0 ? para.FontSize + 1 : para.FontSize - 1, 6);
                } else {
                    double fontSize = Math.Max(e.Delta > 0 ? uxRichTextBox.FontSize + 1 : uxRichTextBox.FontSize - 1, 6);
                    SetFont(uxRichTextBox.FontFamily, fontSize, uxRichTextBox.Foreground, uxRichTextBox.FontStyle);
                }
            }
        }
        private void OnZoomCommand(object sender, RoutedEventArgs e) {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
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

            using var fd = new System.Windows.Forms.FontDialog {
                Font = new System.Drawing.Font(GetFamilyFontName(uxRichTextBox.FontFamily), (float)uxRichTextBox.FontSize)
            };

            if (uxRichTextBox.Foreground is SolidColorBrush scb) {
                fd.Color = System.Drawing.Color.FromArgb(scb.Color.A, scb.Color.R, scb.Color.G, scb.Color.B);
                fd.ShowColor = true;
            }

            var dr = fd.ShowDialog();
            if (dr == System.Windows.Forms.DialogResult.OK) {
                var fontFamily = new FontFamily(fd.Font.FontFamily.ToString());
                var fontColor = Color.FromArgb(fd.Color.A, fd.Color.R, fd.Color.G, fd.Color.B);
                var fontSize = fd.Font.Size;
                //var fsConverter = new FontStyleConverter();

                //var fontStyle = (FontStyle)fsConverter.ConvertFrom(fd.Font.Style.ToString());

                SetFont(fontFamily, fontSize, fontColor, FontStyle);
                uxSettingsPropertyGrid.Update();
            }
        }
        private void SetFont(FontFamily fontFamily, double fontSize, Brush foreGround, FontStyle fontStyle, bool updateUXSettings = true) {
            if (foreGround is SolidColorBrush scb) {
                SetFont(fontFamily, fontSize, scb.Color, fontStyle, updateUXSettings);
            }
        }


        private void SetFont(FontFamily fontFamily, double fontSize, Color fontColor, FontStyle fontStyle, bool updateUXSettings = true) {
            // Keep the Window Font in sync with the RichTextBox Font:
            if (fontFamily != null) {
                var doc = uxRichTextBox.Document;

                // Create a TextRange for the Selected Text or the entire document.
                TextRange range = uxRichTextBox.Selection;
                if (string.IsNullOrEmpty(uxRichTextBox.Selection.Text)) {
                    range = new TextRange(doc.ContentStart, doc.ContentEnd);
                    range.Select(range.Start, range.End);
                }

                // Set the Font for the Selected Text or the whole RichTextBox:
                if (!range.IsEmpty) {
                    range.ApplyPropertyValue(FlowDocument.FontSizeProperty, fontSize);
                    range.ApplyPropertyValue(FlowDocument.FontStyleProperty, fontStyle.ToString());
                    range.ApplyPropertyValue(FlowDocument.ForegroundProperty, fontColor.ToString());
                    range.ApplyPropertyValue(FlowDocument.FontFamilyProperty, GetFamilyFontName(fontFamily));
                }

                // Set the Font for the whole RichTextBox when no Text was selected
                if (string.IsNullOrEmpty(uxRichTextBox.Selection?.Text)) {
                    uxRichTextBox.FontFamily = fontFamily;
                    uxRichTextBox.FontSize = fontSize;
                    uxRichTextBox.Foreground = new SolidColorBrush(fontColor);
                    uxRichTextBox.FontStyle = fontStyle;
                    uxRichTextBox.Foreground = fontColor != null ? new SolidColorBrush(fontColor) : uxRichTextBox.Foreground;
                }

            }

            if (updateUXSettings) {
                VM.Note.UXSettings.FontFamily = uxRichTextBox.FontFamily;
                VM.Note.UXSettings.FontSize = uxRichTextBox.FontSize;
                VM.Note.UXSettings.FontColor = (uxRichTextBox.Foreground as SolidColorBrush).Color;
                VM.Note.UXSettings.FontStyle = uxRichTextBox.FontStyle;
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
            if (uxRichTextBox.Background is SolidColorBrush scb) {
                cd.Color = System.Drawing.Color.FromArgb(scb.Color.A, scb.Color.R, scb.Color.G, scb.Color.B);
            }

            var dr = cd.ShowDialog();
            if (dr == System.Windows.Forms.DialogResult.OK) {
                Color color = Color.FromArgb(cd.Color.A, cd.Color.R, cd.Color.G, cd.Color.B);
                SetBackgroundColor(color);
                uxSettingsPropertyGrid.Update();
            }
        }

        private void SetBackgroundColor(Color color, bool updateUXSettings = true) {
            if (updateUXSettings) {
                VM.Note.UXSettings.BackgroundColor = color;
            };

            uxColorPicker.SelectedColor = color;

            Background = new SolidColorBrush(color);
            uxRichTextBox.Document.Background = new SolidColorBrush(color);

            Color colorScRgb = AdjustColor(color);
            if (colorScRgb.A == 0) { colorScRgb.A = 0x01; }

            Background = new SolidColorBrush(colorScRgb);
            uxToolBar.Background = Background;

            if (uxRichTextBox.Document.Background is ImageBrush backgroundBrush) {
                backgroundBrush.Opacity = colorScRgb.A / 255.0f;
            }

            uxRichTextBox.SelectionBrush = colorScRgb.ScA <= 0.75 ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(colorScRgb);
        }

        private Color AdjustColor(Color color) {
            // Tweak the textbox color for a nice background accent color for the toolbar.
            U.Graphics.RgbToHls(color.R, color.G, color.B, out double h, out double l, out double s);
            if (l > 0.50f) l *= 0.25f; else if (l > 0.35f) l *= 0.35f; else if (l > 0.25f) l *= 1.25f; else if (l > 0.00f) l *= 2.00f; else l = 0.35f;
            if (l < 0.35f) s *= 0.50f; else if (l < 0.35f) s *= 0.75f;

            U.Graphics.HlsToRgb(h, l, s, out int r, out int g, out int b);
            float scA = color.A / 255.0f, scR = r / 255.0f, scG = g / 255.0f, scB = b / 255.0f;
            var colorScRgb = Color.FromScRgb(scA, scR, scG, scB);

            return colorScRgb;
        }

        private void OpenNewWindow(Note note) {
            var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
            var area = screen.WorkingArea;

            // Create new NoteViewer at same size & slightly to left & lower than this one
            var noteViewer = new NoteViewer(note) {
                Left = Left > area.Left ? Left >= 0 ? Left + (area.Width * 0.02) : Left - (area.Width * 0.02) : Left + (area.Width * 0.02),
                Top = Top > area.Top ? Top >= 0 ? Top + (area.Height * 0.03) : Top - (area.Height * 0.03) : (area.Height * 0.03),
                Width = Width,
                Height = Height
            };
            noteViewer.Show();
        }

        private void OnNoteTitleLabel_MouseDoubleClick(object sender, RoutedEventArgs e) {
            if (WindowState == WindowState.Maximized) { WindowState = WindowState.Normal; }
            uxToolBar.LayoutTransform = uxToolBar.LayoutTransform == Transform.Identity ? new ScaleTransform(0.5, 0.5) : Transform.Identity;
        }

        private void OnAddNoteButton_Click(object sender, RoutedEventArgs e) {
            OnNewCommand(sender, e);
        }

        private void OnNewCommand(object sender, RoutedEventArgs e) {
            OpenNewWindow(VM.CreateNewNote(copy: VM.Note));
        }

        private void OnDelNoteButton_Click(object sender, RoutedEventArgs e) {
            OnDeleteCommand(sender, e);
        }

        private void OnReminderButton_Click(object sender, RoutedEventArgs e) {
            if (sender is ToggleButton toggleButton) {
                ShowReminderPanel(toggleButton.IsChecked);
            }
        }

        private void ShowReminderPanel(bool? show) {
            uxReminderPanel.Visibility = show == true ? Visibility.Visible : Visibility.Collapsed;
            uxReminderButton.IsChecked = show == true;
            uxViewNoteReminderMenuItem.IsChecked = show == true;
            uxSettingsButton.IsChecked = false;
            uxViewNoteSettingsMenuItem.IsChecked = uxSettingsButton.IsChecked == true;
        }

        private void OnSettingsButton_Click(object sender, RoutedEventArgs e) {
            if (sender is ToggleButton toggleButton) {
                ShowSettingsPanel(toggleButton.IsChecked);
            }
        }

        private void ShowSettingsPanel(bool? show) {
            uxSettingsPanel.Visibility = show == true ? Visibility.Visible : Visibility.Collapsed;
            uxSettingsButton.IsChecked = true;
            uxViewNoteSettingsMenuItem.IsChecked = uxSettingsButton.IsChecked == true;
            uxReminderButton.IsChecked = false;
            uxViewNoteReminderMenuItem.IsChecked = uxReminderButton.IsChecked == true;
        }

        private void OnOptionsExpander_Expanded(object sender, RoutedEventArgs e) {
            if (sender is Expander expander) {
                VM.Note.UXSettings.OptionsExpanded = expander.IsExpanded;
            }
        }

        private void OnOptionsExpander_Collapsed(object sender, RoutedEventArgs e) {
            if (sender is Expander expander) {
                VM.Note.UXSettings.OptionsExpanded = expander.IsExpanded;
            }
        }

        private void OnPinTabButton_Click(object sender, RoutedEventArgs e) {
            Topmost = !Topmost;
            UpdatePinTabButton();
            uxSettingsPropertyGrid.Update();
        }

        private void UpdatePinTabButton() {
            VM.Note.UXSettings.Topmost = Topmost;

            Image image = uxPinTab.Content as Image;
            /*             Image icon = Topmost ? (Image)FindResource("PinTabOn") : (Image)FindResource("PinTabOff");
                        image.Source = icon.Source; */

            image.RenderTransform = new RotateTransform(Topmost ? 0 : 90);
            image.RenderTransformOrigin = new Point(0.5, 0.5);
        }

#pragma warning disable IDE0051

        private void SaveToFile() {
            var sfd = new System.Windows.Forms.SaveFileDialog {
                Filter = "XAML Files (*.xaml)|*.xaml|RichText Files (*.rtf)|*.rtf|All Files (*.*)|*.*"
            };

            var dr = sfd.ShowDialog();
            if (dr == System.Windows.Forms.DialogResult.OK) {
                TextRange range = new TextRange(uxRichTextBox.Document.ContentStart, uxRichTextBox.Document.ContentEnd);
                using FileStream fs = File.Create(sfd.FileName);
                range.Save(fs, DataFormats.XamlPackage);
            }
        }

        private void LoadFromFile() {
            var ofd = new System.Windows.Forms.OpenFileDialog {
                Filter = "XAML Files (*.xaml)|*.xaml|RichText Files (*.rtf)|*.rtf|All Files (*.*)|*.*"
            };

            var dr = ofd.ShowDialog();
            if (dr == System.Windows.Forms.DialogResult.OK) {
                TextRange range = new TextRange(uxRichTextBox.Document.ContentStart, uxRichTextBox.Document.ContentEnd);
                using FileStream fs = File.OpenRead(ofd.FileName);
                range.Load(fs, DataFormats.XamlPackage);
            }
        }

        // TODO: Print & PrintPreview NOT working (may only work with FixedDocument (not FlowDocument))
        private void OnPrintCommand(object sender, RoutedEventArgs e) {
            var printDialog = new PrintDialog(); // Create a PrintDialog.

            // Show the dialog and print the document if successful
            if (printDialog.ShowDialog() == true) {
                printDialog.PrintDocument((((IDocumentPaginatorSource)uxRichTextBox.Document).DocumentPaginator), $"Printing ");
            }
        }

        #endregion

        #region Settings & Configuration

        // Loaded from App Settings located @ C:\User\{User}\AppData\Local\OmniZenNotes\OmniZenNote.exe_...
        private void LoadSettings() {
            try {
                /*                 // Restore Window position and size from user settings save of last session
                                if (S.Default?.RestoreBounds is Rect restoreBounds)
                                {
                                    Left = restoreBounds.Left; Top = restoreBounds.Top;
                                    Width = restoreBounds.Width; Height = restoreBounds.Height;
                                }
                                // Restore the Window State (minimized gets converted to be Normal to avoid user not seeing it)
                                WindowState = S.Default?.WindowState is WindowState windowState ? windowState : System.Windows.WindowState.Normal;
                                WindowState = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
                                SetFont(S.Default.Font, S.Default.FontSize, S.Default.FontColor, FontStyle, updateUXSettings: false);
                                uxColorPicker.SelectedColor = S.Default.BackgroundColor;
                                SetBackgroundColor(S.Default.BackgroundColor, updateUXSettings: false);
                                uxOptionsExpander.IsExpanded = S.Default.OptionsExpanded;
                                Topmost = S.Default.Topmost;
                 */
                // Auto Save Settings
                if (S.Default.AutoSave is int seconds && seconds > 0) {
                    Timer = new DispatcherTimer();
                    Timer.Tick += new EventHandler((sender, e) => Save(saveAsync: true));
                    Timer.Interval = TimeSpan.FromSeconds(seconds);
                    Timer.Start();
                }
            } catch {
                Left = 1; Top = 1; Width = 480; Height = 480;
            }

            LoadUXSettings();
            if (uxRichTextBox.Background is SolidColorBrush scba) {
                uxColorPicker.SelectedColor = scba.Color;
            }
        }

        // Restore the Note specific settings (which override the App level settings)
        private void LoadUXSettings() {
            if (VM.Note.UXSettings.RestoreBounds is Rect restoreBounds && double.IsFinite(restoreBounds.Left) && double.IsFinite(restoreBounds.Top)) {
                Left = restoreBounds.Left; Top = restoreBounds.Top;
                Width = restoreBounds.Width; Height = restoreBounds.Height;
            }

            // Restore the Window State (minimized gets converted to be Normal to avoid user not seeing it)
            SetFont(VM.Note.UXSettings.FontFamily, VM.Note.UXSettings.FontSize, VM.Note.UXSettings.FontColor, FontStyle);
            uxColorPicker.SelectedColor = VM.Note.UXSettings.BackgroundColor;
            SetBackgroundColor(VM.Note.UXSettings.BackgroundColor);
            uxOptionsExpander.IsExpanded = VM.Note.UXSettings.OptionsExpanded;
            Topmost = VM.Note.UXSettings.Topmost;
        }

        private void SaveUXSettings() {
            // Save the Note specific settings (which override the App level settings)
            if (VM.Note != null) {
                VM.Note.UXSettings ??= new UXSettings();
                VM.Note.UXSettings.RestoreBounds = RestoreBounds;
                VM.Note.UXSettings.WindowState = WindowState;
                VM.Note.UXSettings.FontFamily = uxRichTextBox.FontFamily;
                VM.Note.UXSettings.FontSize = uxRichTextBox.FontSize;
                if (uxRichTextBox.Foreground is SolidColorBrush fgscb) {
                    VM.Note.UXSettings.FontColor = fgscb.Color;
                }
                if (uxRichTextBox.Background is SolidColorBrush scba) {
                    VM.Note.UXSettings.BackgroundColor = scba.Color;
                }
                VM.Note.UXSettings.OptionsExpanded = uxOptionsExpander.IsExpanded;
                VM.Note.UXSettings.Topmost = Topmost;
            }
        }

        private void SaveSettings() {

            SaveUXSettings();
            /*
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
            */

            S.Default.Save();
        }

        #endregion

        private void OnRichTextBox_TextChanged(object sender, TextChangedEventArgs e) {
        }

        private void uxColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) {
            SetBackgroundColor((Color)e.NewValue);
            uxSettingsPropertyGrid.Update();
        }

        private void uxReminderPanel_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                uxReminderPanel.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        private void uxSettingsPanel_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                uxSettingsPanel.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        private void uxReminderPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (e.NewValue is bool visible && visible) {
                uxSettingsPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void uxSettingsPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (e.NewValue is bool visible && visible) {
                uxReminderPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void uxReminderPropertyGrid_PropertyValueChanged(object sender, PropertyValueChangedEventArgs e) {
        }

        private void uxSettingsPropertyGrid_PropertyValueChanged(object sender, PropertyValueChangedEventArgs e) {
            if (e.OriginalSource is PropertyItem item) {
                Color foregroundColor = Colors.Black;
                if (uxRichTextBox.Foreground is SolidColorBrush scba) {
                    foregroundColor = scba.Color;
                }

                switch (item.PropertyName) {
                    case "BackgroundColor":
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
                    case "Topmost":
                        Topmost = (bool)e.NewValue;
                        UpdatePinTabButton();
                        break;
                    case "Title":
                        Title = (string)e.NewValue;
                        break;

                    default: break;
                }
            }
        }

        private void OnRichTextBox_PreviewDragOver(object sender, DragEventArgs args) {
            args.Effects = args.KeyStates == (DragDropKeyStates.LeftMouseButton | DragDropKeyStates.ControlKey) ? DragDropEffects.Copy : DragDropEffects.Link;
            args.Handled = true;
        }

        private void OnRichTextBox_PreviewDrop(object sender, DragEventArgs args) {
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
                    // Insert the file text content directly into the Document
                    case ".text":
                    case ".txt": {
                            using var fileToLoad = new StreamReader(fileInfo.FullName);
                            uxRichTextBox.AppendText(fileToLoad.ReadToEnd());
                            fileToLoad.Close();
                            break;
                        }
                    // Create an Image element and set the Source to the dropped file path
                    case ".png":
                    case ".jpg":
                    case ".gif":
                    case ".bmp":
                        var image = new Image();
                        var bitmap = new BitmapImage(new Uri(fileInfo.FullName));
                        image.Source = bitmap;
                        image.Width = Math.Min(bitmap.PixelWidth, Width);
                        image.Height = Math.Min(bitmap.PixelHeight, Height);
                        if (bitmap.PixelWidth > Width || bitmap.PixelHeight > Height) {
                            image.SetBinding(WidthProperty, "{Binding ActualWidth,  Mode=OneWay, ElementName=uxRichTextBox}");
                            image.SetBinding(HeightProperty, "{Binding ActualHeight, Mode=OneWay, ElementName=uxRichTextBox}");
                        }
                        var iuic_image = new InlineUIContainer(image, tp);
                        break;

                    // Create a MediaElement and set the Source to the dropped file path
                    case ".mp4":
                    case ".mpg":
                    case ".mp3":
                    case ".wma":
                    case ".wmv":
                    case ".avi":
                    case ".mkv":
                        var me = new MediaElement { Source = new Uri(fileInfo.FullName), ToolTip = fileInfo.FullName };
                        var iuic_me = new InlineUIContainer(me, tp);
                        break;
                }
            } else if (args.KeyStates == DragDropKeyStates.AltKey) {
                // Insert the contents of supported dropped file:
                switch (fileInfo.Extension.ToLower()) {
                    case ".png":
                    case ".jpg":
                    case ".gif":
                    case ".bmp":
                        var bitmap = new BitmapImage(new Uri(fileInfo.FullName));
                        var imageBrush = new ImageBrush(bitmap);
                        if (uxRichTextBox.Background is SolidColorBrush scb && scb.Color.A < 255) {
                            imageBrush.Opacity = scb.Color.A / 255.0f;
                        }
                        uxRichTextBox.Document.Background = imageBrush;
                        break;
                    case ".wmv":
                        var me = new MediaElement { Source = new Uri(fileInfo.FullName) };
                        var iuic_me = new InlineUIContainer(me, tp);
                        break;
                }
            } else {
                // Create a Hyperlink to the dropped file/folder
                var image = new Image {
                    // ToolTip is used for xaml Image Style for FilePathToThumbNailConverter to display image as thumbnail
                    ToolTip = fileInfo.FullName,
                    Tag = .50d,
                    Height = DefaultThumbnailSize.Medium.Height,
                    Width = DefaultThumbnailSize.Medium.Width,
                };

                // Create a Hyperlink with the Shell thumbnail image & display name
                Hyperlink hyperLink = new Hyperlink(new Run(" "), tp) {
                    NavigateUri = new Uri(fileInfo.FullName),
                    Tag = image,
                };

                hyperLink.RequestNavigate += (object sender, RequestNavigateEventArgs e) => {
                    Debug.WriteLine($"RequestNavigate for {sender} with {e}");
                };

                ShellObject shellObject = ShellObject.FromParsingName(fileInfo.FullName);
                hyperLink.Inlines.Add(image);
                hyperLink.Inlines.Add(new Run($" {shellObject?.Name} "));

                // Wrap the Hyperlink in a Paragraph to keep it isolated and editable
                var para = new Paragraph(new Run(" "));
                para.Inlines.Add(hyperLink);
                para.Inlines.Add(new Run(" ", tp));
                uxRichTextBox.Document.Blocks.Add(para);
            }
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
                var error = $"Media FAILED for {me.Source} : ";
                var iuic = me.Parent as InlineUIContainer;
                iuic.ContentStart.Paragraph.Inlines.Add(new Run($"{error} {e.ErrorException.Message}"));
                //U.Exceptions.LogException(e.ErrorException, error);
            }
        }

        public BitmapSource GetShellThumbNail(string filePath) {
            ShellObject shellObject = ShellObject.FromParsingName(filePath);
            return shellObject?.Thumbnail.SmallBitmapSource;
        }

        public void OnHyperlink_MouseDown(object sender, MouseButtonEventArgs e) {
            Debug.WriteLine($"OnHyperlink_MouseDown for {e.Source}");
            if (sender is Hyperlink hyperlink && e.MouseDevice.LeftButton == MouseButtonState.Pressed) {
                var fileInfo = new FileInfo(Uri.UnescapeDataString(hyperlink.NavigateUri.AbsolutePath));
                U.Shell.ShellOpen(fileInfo);
                e.Handled = true;
            }
        }

        // BUG: Once Document has been saved/restored, Image is no longer resizable
        public void OnHyperlink_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (sender is Hyperlink hyperlink && Keyboard.Modifiers == ModifierKeys.Control) {
                Image image = hyperlink.Tag as Image;
                image.Tag = e.Delta > 0 ? (double)image.Tag * 1.10 : (double)image.Tag * 0.90;
                Debug.WriteLine($"OnHyperlink_MouseWheel Delta={e.Delta} Image {image.Height} x {image.Width} scaled by {image.Tag}");
                e.Handled = true;
            }
        }

        private string IsSingleFileOrDir(DragEventArgs args) {
            // Check for files in the hovering data object.
            if (args.Data.GetDataPresent(DataFormats.FileDrop, true)) {
                var fileNames = args.Data.GetData(DataFormats.FileDrop, true) as string[];
                if (fileNames?.Length is 1) {
                    if (File.Exists(fileNames[0]) || Directory.Exists(fileNames[0])) {
                        return fileNames[0];
                    }
                }
            }
            return null;
        }
    }

    public class FilePathToThumbNailConverter : IValueConverter
    {
        object IValueConverter.Convert(object o, Type type, object parameter, CultureInfo culture) {
            if (o is Image image && image.ToolTip is string tooltip) {
                FileInfo fileInfo = new FileInfo(tooltip);
                try {
                    ShellObject shellObject = ShellObject.FromParsingName(fileInfo.FullName);
                    double scaleH = image.Height * (double)image.Tag;
                    double scaleW = image.Width * (double)image.Tag;
                    var thumbnail = scaleW switch
                    {
                        double w when w >= DefaultThumbnailSize.ExtraLarge.Height => shellObject?.Thumbnail.ExtraLargeBitmapSource,
                        double w when w >= DefaultThumbnailSize.Large.Height => shellObject?.Thumbnail.ExtraLargeBitmapSource,
                        double w when w >= DefaultThumbnailSize.Medium.Height => shellObject?.Thumbnail.LargeBitmapSource,
                        double w when w >= DefaultThumbnailSize.Small.Height => shellObject?.Thumbnail.MediumBitmapSource,
                        _ => shellObject?.Thumbnail.SmallBitmapSource
                    };
                    var properties = shellObject.Properties;
                    var prop = properties.System.Title.Value;
                    var comment = properties.System.Comment.Value;

                    Debug.WriteLine($"FilePathToThumbNailConverter Thumbnail {thumbnail.Height} x {thumbnail.Width} for scaleW {scaleW} scaled by {image.Tag}");
                    return thumbnail;
                } catch {
                    try {
                        return U.Shell.GetShellIcon(fileInfo);
                    } catch { }
                }
            }

            return null;
        }

        object IValueConverter.ConvertBack(object o, Type type, object parameter, CultureInfo culture) => null;
    }

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
}