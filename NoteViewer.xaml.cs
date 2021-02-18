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
using System.Collections;

#pragma warning disable IDE1006 // Ignore name rule violation for XAML element objects starting with ux

namespace OmniZenNotes
{
    using S = Properties.Settings;

    public partial class NoteViewer : Window
    {
        public static List<FontFamily> FontFamilies;
        public static List<PropertyInfo> BackgroundColors;

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
            OnFormatBarCommand(null, null); // SETTINGS: FormatBar Enabled ON/OFF
            uxRichTextBox.SpellCheck.IsEnabled = true;
            OnSpellCheckCommand(null, null); // SETTINGS: SpellCheck ON/OFF

            uxShowNotesMenuItem.SubmenuOpened += OnShowNotes_SubmenuOpened;
            uxSelectColorMenuItem.SubmenuOpened += OnSelectColor_SubmenuOpened;
            uxSelectFontMenuItem.SubmenuOpened += OnSelectFont_SubmenuOpened;

            Title = VM.Note.Title;
            Image AppIcon = (Image)FindResource("AppIcon");
            Icon = AppIcon.Source;
        }

        // Create, configure and bind Application Commands
        void InitializeCommands() {

            // Show Notes Commands
            AddCommandBinding(AppCommands.ShowAllNotesCommand, OnShowAllNotesCommand);
            InputBindings.Add(new KeyBinding(AppCommands.ShowAllNotesCommand, new KeyGesture(Key.X, ModifierKeys.Alt, "Alt-X")));
            AddCommandBinding(AppCommands.ShowPrivateNotesCommand, OnShowPrivateNotesCommand);
            InputBindings.Add(new KeyBinding(AppCommands.ShowPrivateNotesCommand, new KeyGesture(Key.Y, ModifierKeys.Alt, "Alt-Y")));
            AddCommandBinding(AppCommands.ShowPublicNotesCommand, OnShowPublicNotesCommand);
            InputBindings.Add(new KeyBinding(AppCommands.ShowPublicNotesCommand, new KeyGesture(Key.Z, ModifierKeys.Alt, "Alt-X")));

            // Set Note Font Command
            AddCommandBinding(AppCommands.SelectFontCommand, OnSelectFontCommand);

            // Format Bar Command
            AddCommandBinding(AppCommands.FormatBarCommand, OnFormatBarCommand);
            InputBindings.Add(new KeyBinding(AppCommands.FormatBarCommand, new KeyGesture(Key.F4, ModifierKeys.None, "F4")));
            // Spellcheck Command
            AddCommandBinding(AppCommands.SpellCheckCommand, OnSpellCheckCommand);
            InputBindings.Add(new KeyBinding(AppCommands.SpellCheckCommand, new KeyGesture(Key.F7, ModifierKeys.None, "F7")));

            // View Note Reminder Command
            AddCommandBinding(AppCommands.ViewNoteReminderCommand, OnViewNoteReminderCommand);
            InputBindings.Add(new KeyBinding(AppCommands.ViewNoteReminderCommand, new KeyGesture(Key.R, ModifierKeys.Alt, "Alt-R")));
            // View Note Settings Command
            AddCommandBinding(AppCommands.ViewNoteSettingsCommand, OnViewNoteSettingsCommand);
            InputBindings.Add(new KeyBinding(AppCommands.ViewNoteSettingsCommand, new KeyGesture(Key.S, ModifierKeys.Alt, "Alt-S")));

            // New Command
            AddCommandBinding(ApplicationCommands.New, OnNewCommand);
            // Refresh Command
            AddCommandBinding(NavigationCommands.Refresh, OnRefreshCommand);
            // Save Note
            AddCommandBinding(ApplicationCommands.Save, OnSaveCommand);
            // Hide Command
            AddCommandBinding(AppCommands.HideCommand, OnHideCommand);
            InputBindings.Add(new KeyBinding(AppCommands.HideCommand, new KeyGesture(Key.H, ModifierKeys.Alt, "Alt-H")));
            // Delete Command
            AddCommandBinding(AppCommands.DeleteCommand, OnDeleteCommand);
            InputBindings.Add(new KeyBinding(AppCommands.DeleteCommand, new KeyGesture(Key.D, ModifierKeys.Alt, "Alt-D")));
            // Full Screen Toggle
            AddCommandBinding(NavigationCommands.Zoom, OnZoomCommand);
            InputBindings.Add(new KeyBinding(NavigationCommands.Zoom, new KeyGesture(Key.F11, ModifierKeys.None, "F11")));

            // Print / Print Preview Note TODO: Not working - might need to conver to FixedDocument to print
            // AddCommandBinding(ApplicationCommands.Print, OnPrintCommand);
            // AddCommandBinding(ApplicationCommands.PrintPreview , OnPrintPreviewCommand);
            
            // Exit Application Command
            AddCommandBinding(AppCommands.ExitApplicationCommand, OnExitApplicationCommand);
            InputBindings.Add(new KeyBinding(AppCommands.ExitApplicationCommand, new KeyGesture(Key.F4, ModifierKeys.Alt, "F4")));

            void AddCommandBinding(ICommand command, ExecutedRoutedEventHandler handler, CanExecuteRoutedEventHandler enabler = null) {
                CommandBinding cb = new CommandBinding(command);
                cb.Executed += new ExecutedRoutedEventHandler(handler);
                cb.CanExecute += new CanExecuteRoutedEventHandler(enabler ??= (sender, e) => e.CanExecute = true);
                CommandBindings.Add(cb);
            }

        }

        private void OnLoaded(object sender, EventArgs e) {
            DataContext = VM.Note;

            uxSettingsPropertyGrid.SelectedObject = VM.Note;
            uxReminderPropertyGrid.SelectedObject = VM.Note.Task;
            uxRichTextBox.Document = VM.Note.Document;
            UpdatePinTabButton();

            // Make sure we don't go off screen (if user monitor changes etc.)
            var rect = KeepWindowInBounds(RestoreBounds);
            Width = rect.Width; Height = rect.Height;
            Left = rect.Left; Top = rect.Top;
        }

        Rect KeepWindowInBounds(Rect restoreBounds) {
            var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
            var area = screen.WorkingArea;

            double width = restoreBounds.Width > 20 ? restoreBounds.Width : area.Width / 2.25;
            double height = restoreBounds.Height > 20 ? restoreBounds.Height : area.Height / 5.5;
            double left = restoreBounds.Left > area.Right ? area.Width / 2 - Width / 2 : restoreBounds.Left; // Center Horz
            double top = restoreBounds.Top > area.Top ? restoreBounds.Top : area.Height / 2 - Height / 2;  // Center Vert

            return new Rect(left, top, width, height); ;
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
            uxSpellCheckMenuItem.IsChecked = uxRichTextBox.SpellCheck.IsEnabled;
        }

        void OnFormatBarCommand(object sender, RoutedEventArgs e) {
            RTBFB.IsEnabled = !RTBFB.IsEnabled;
            RTBFB.Visibility = RTBFB.IsEnabled ? Visibility.Visible : Visibility.Hidden;
            uxFormatBarMenuItem.IsChecked = RTBFB.IsEnabled;
        }

        void OnHideCommand(object sender, RoutedEventArgs e) {
            if (App.NoteViewers.Count == 1) {
                MessageBox.Show("You cannot HIDE the LAST Sticky Note. \n\nUse Alt-F4 to Exit Application", Assembly.GetExecutingAssembly().GetName().Name);
            } else {
                Hide();
            }
            e.Handled = true;
        }

        void OnDeleteCommand(object sender, RoutedEventArgs e) {
            string title = Assembly.GetExecutingAssembly().GetName().Name;
            MessageBoxResult mbr = MessageBoxResult.OK;
            if (App.NoteViewers.Count == 1) {
                mbr = MessageBox.Show("You are about to DELETE the LAST Sticky Note. \nThis will EXIT the Application. \n\nPress OK to continue. \n\nPress Cancel to go back.", $"EXIT Application {title}", MessageBoxButton.OKCancel, MessageBoxImage.Stop, MessageBoxResult.Cancel);
            }

            if (mbr == MessageBoxResult.OK) {
                mbr = MessageBox.Show("Are you sure you want to permanently delete this Note?", $"Delete Note - {title}", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
                if (mbr == MessageBoxResult.Yes) {
                    VM.Note.Delete();
                    VM.Note = null;
                    Close();
                    App.NoteViewers.Remove(this);
                }
            }

            e.Handled = true;
        }

        void OnExitApplicationCommand(object sender, RoutedEventArgs e) {
            Application.Current.Shutdown();
        }

        void OnSelectFontCommand(object sender, RoutedEventArgs e) {
            OnTextFormatButton_Click(sender, e);
        }

        void OnViewNoteReminderCommand(object sender, ExecutedRoutedEventArgs e) {
            if (e.Source is RichTextBox || e.Source is NoteViewer) { // Menu item or Accelerator Selected
                uxViewNoteReminderMenuItem.IsChecked = !uxViewNoteReminderMenuItem.IsChecked;
                uxReminderButton.IsChecked = uxViewNoteReminderMenuItem.IsChecked;
            }
            ShowReminderPanel(uxReminderButton);
        }
        void OnViewNoteSettingsCommand(object sender, ExecutedRoutedEventArgs e) {
            if (e.Source is RichTextBox || e.Source is NoteViewer) { // Menu item or Accelerator Selected
                uxViewNoteSettingsMenuItem.IsChecked = !uxViewNoteSettingsMenuItem.IsChecked;
                uxSettingsButton.IsChecked = uxViewNoteSettingsMenuItem.IsChecked;
            }
            ShowSettingsPanel(uxSettingsButton);
        }

        void OnShowAllNotesCommand(object sender, RoutedEventArgs e) {
            foreach (var nv in App.NoteViewers) {
                if (nv.Visibility != Visibility.Visible) { nv.Show(); }
            }
        }

        void OnShowPrivateNotesCommand(object sender, RoutedEventArgs e) {
            foreach (var nv in App.NoteViewers) {
                if (nv.VM.Note.Security.Permissions == EntityPermissions.Private) {
                    if (nv.Visibility != Visibility.Visible) { nv.Show(); }
                } else {
                    if (nv.Visibility == Visibility.Visible) { nv.Hide(); }
                }
            }
        }

        void OnShowPublicNotesCommand(object sender, RoutedEventArgs e) {
            foreach (var nv in App.NoteViewers) {
                if (nv.VM.Note.Security.Permissions != EntityPermissions.Private) {
                    if (nv.Visibility != Visibility.Visible) { nv.Show(); }
                } else {
                    if (nv.Visibility == Visibility.Visible) { nv.Hide(); }
                }
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

        private void OnToolBar_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                DragMove();
            }
        }

        private void OnActivated(object sender, EventArgs e) {
            ToggleToolBar(Visibility.Visible);
        }

        private void OnDeactivated(object sender, EventArgs e) {
            ToggleToolBar(Visibility.Collapsed);
        }

        private void OnWindow_MouseEnter(object sender, EventArgs e) {
            ToggleToolBar(Visibility.Visible);
        }

        private void OnWindow_MouseLeave(object sender, EventArgs e) {
            if (!IsActive) {
                ToggleToolBar(Visibility.Collapsed);
            }
        }
        private void ToggleToolBar(Visibility visibility) {
            uxToolBar.Visibility = visibility;
            // uxToolBar.LayoutTransform = visibility == Visibility.Hidden ? new ScaleTransform(0.05, 0.05) : Transform.Identity;
        }

        private void OnButton_MouseEnter(object sender, EventArgs e) {
        }

        private void OnButton_MouseLeave(object sender, EventArgs e) {
        }

        #region UX Control Event Handlers

        private void OnTextFormatButton_Click(object sender, RoutedEventArgs e) {

            using var fd = new System.Windows.Forms.FontDialog {
                Font = new System.Drawing.Font(U.Graphics.GetFamilyFontName(uxRichTextBox.FontFamily), (float)uxRichTextBox.FontSize)
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
                    range.ApplyPropertyValue(FlowDocument.FontFamilyProperty, U.Graphics.GetFamilyFontName(fontFamily));
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
            uxToolBar.Background = new SolidColorBrush(colorScRgb);

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
            System.Windows.Forms.Screen[] allScreens = System.Windows.Forms.Screen.AllScreens;
            var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
            var area = screen.WorkingArea;

            // Create new NoteViewer at same size & slightly to left & lower than this one
            /* TODO: Use MS StickyNotes 3.7.71 model for new Note placements. 
                1. New Notes are created to the right of existing note (with padW)
                2. If not enough space on the right to fit entire note, 
                    a) If more than padW avail, align right side of new note to edge of screen
                    b) If less than padW avail, create to left of new note with padW
                3. If enough space on right, place new note padW from right side of other note
                4. If not enough space on either side, then go lower and to the right
                    If another note occupies the wanted space, then try to go to left side
                5. If Note is touching or across left side, new Note goes to left side  + padW of next screen
                6. If new Note would overlap another existing Note, place it below current Note
            */
            var padW = area.Width * 0.01;
            double right = Left + Width;
            double newLeft = Left + Width + padW;    // 1
            double newRight = newLeft + Width;
            if (newRight > area.Right) {
                if (right + padW <= area.Right) { newLeft = area.Right - Width; }  // 2a
                if (right + padW >= area.Right) { newLeft = Left - Width - padW; }  // 2b
            }

            if (IsScreenAdjacentToARightScreen(screen, allScreens)) {
                if (right >= area.Right) { newLeft = area.Right + padW; }    // 5
            }

            double top = Top;
            var noteViewer = new NoteViewer(note) {
                Left = newLeft,
                Top = top,
                Width = Width,
                Height = Height
            };
            noteViewer.Show();

            static bool IsScreenAdjacentToARightScreen(System.Windows.Forms.Screen screen, System.Windows.Forms.Screen[] allScreens) {
                foreach (var s in allScreens) {
                    if (s.WorkingArea.Right > screen.WorkingArea.Right) { return true; }
                }
                return false;
            }

#pragma warning disable CS8321 // The local function 'f' is declared but never used
            static bool IsScreenAdjacentToALeftScreen(System.Windows.Forms.Screen screen, System.Windows.Forms.Screen[] allScreens) {
                foreach (var s in allScreens) {
                    if (s.WorkingArea.Left < screen.WorkingArea.Left) { return true; }
                }
                return false;
            }
#pragma warning restore CS8321
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


        // Reminder and Settings PropertyGrid UX Management:
        private void ShowReminderPanel(ToggleButton toggleButton) {
            uxReminderPanel.Visibility = toggleButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowSettingsPanel(ToggleButton toggleButton) {
            uxSettingsPanel.Visibility = toggleButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void uxReminderPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (e.NewValue is bool visible) {
                uxViewNoteReminderMenuItem.IsChecked = visible;
                // Toggle Settings panel & button to be mutualy exclusive of the Reminder panel
                uxSettingsPanel.Visibility = visible ? Visibility.Collapsed : uxSettingsPanel.Visibility;
                uxSettingsButton.IsChecked = !visible && uxSettingsButton.IsChecked == true;
                uxViewNoteSettingsMenuItem.IsChecked = uxSettingsButton.IsChecked == true;
            }
        }

        private void uxSettingsPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (e.NewValue is bool visible) {
                uxViewNoteSettingsMenuItem.IsChecked = visible;
                // Toggle Reminder panel & button to be mutualy exclusive of the Settings panel
                uxReminderPanel.Visibility = visible ? Visibility.Collapsed : uxReminderPanel.Visibility;
                uxReminderButton.IsChecked = !visible && uxReminderButton.IsChecked == true;
                uxViewNoteReminderMenuItem.IsChecked = uxReminderButton.IsChecked == true;
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
        // Dynamic Submenu Open Processing
        private void OnSelectColor_SubmenuOpened(object sender, RoutedEventArgs e) {
            uxSelectColorMenuItem.Items.Clear();

            // Load the Colors if not already loaded
            if (BackgroundColors == null || BackgroundColors.Count == 0) {
                BackgroundColors = new List<PropertyInfo>(typeof(Colors).GetProperties());
            }

            // Add a Colors... Menu Item to bring up Colors Dialog box
            // TODO: Try to use the newer Wpf Toolkit ColorPicker
            MenuItem item = new MenuItem { Header = "Colors...", };
            item.Click += (object sender, RoutedEventArgs e) => {
                if (sender is MenuItem mi) { OnFillBackgroundButton_Click(sender, e); }
            };
            uxSelectColorMenuItem.Items.Add(item);
            uxSelectColorMenuItem.Items.Add(new Separator());

            foreach (PropertyInfo prop in BackgroundColors) {
                Color color = (Color)prop.GetValue(null);
                item = new MenuItem {
                    Header = prop.Name,
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

                uxSelectColorMenuItem.Items.Add(item);
            }
        }

        private void OnSelectFont_SubmenuOpened(object sender, RoutedEventArgs e) {
            uxSelectFontMenuItem.Items.Clear();

            // Load the Font Families if not already loaded
            if (FontFamilies == null || FontFamilies.Count == 0) {
                FontFamilies = new List<FontFamily>(Fonts.SystemFontFamilies);
                FontFamilies.Sort((FontFamily x, FontFamily y) => { return x.Source.CompareTo(y.Source); });
            }

            // Add a Fonts... Menu Item to bring up Font Dialog box
            MenuItem item = new MenuItem { Header = "Fonts...", };
            item.Click += (object sender, RoutedEventArgs e) => {
                if (sender is MenuItem mi) { OnTextFormatButton_Click(sender, e); }
            };
            uxSelectFontMenuItem.Items.Add(item);
            uxSelectFontMenuItem.Items.Add(new Separator());

            foreach (var fontFamily in FontFamilies) {
                item = new MenuItem {
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

                uxSelectFontMenuItem.Items.Add(item);
            };
        }

        void OnShowNotes_SubmenuOpened(object sender, RoutedEventArgs e) {

            uxShowNotesMenuItem.Items.Add(new Separator());

            App.NoteViewers.Sort((NoteViewer x, NoteViewer y) => { return x.Title.CompareTo(y.Title); });

            foreach (var noteViewer in App.NoteViewers) {
                MenuItem item = new MenuItem {
                    Header = noteViewer.Title,
                    Tag = noteViewer,
                    ToolTip = noteViewer.VM.Note.Description,
                    IsChecked = noteViewer.Visibility == Visibility.Visible
                };

                // MenuItem Background cloned from Document, RichTextBox or NoteViewer Background (in presidence order)
                item.Background = null;
                item.Background ??= noteViewer.uxRichTextBox?.Document?.Background?.Clone();
                item.Background ??= noteViewer.uxRichTextBox?.Background?.Clone();
                item.Background ??= noteViewer?.Background?.Clone();

                item.Click += uxShowNotesMenuItem_Clicked;
                uxShowNotesMenuItem.Items.Add(item);
            }
        }

        // Handle Show Notes visibility processing
        void uxShowNotesMenuItem_Clicked(object sender, RoutedEventArgs e) {
            if (sender is MenuItem mi) {
                if (mi.Tag is NoteViewer nv) {
                    mi.IsChecked = !mi.IsChecked; // Toggle Checked status
                    if (mi.IsChecked) { nv.Show(); nv.Activate(); } else { nv.OnHideCommand(sender, e); }
                }
            }
        }

        #endregion

        #region Settings & Configuration

        // Loaded from App Settings located @ C:\User\{User}\AppData\Local\OmniZenNotes\OmniZenNote.exe_...
        private void LoadSettings() {
            // BUG: First time run brings up dark grey background - s/b using System colors
            try {
                    // Restore Window position and size from user settings save of last session
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
            Debug.WriteLine($"sender {sender} TextChangedEventArgs {e}");
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
                        SetFont((FontFamily)e.NewValue, uxRichTextBox.FontSize, foregroundColor, uxRichTextBox.FontStyle);
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
                Hyperlink hyperlink = new Hyperlink(new Run(" "), tp) { NavigateUri = new Uri(fileInfo.FullName), };

                // Add an Imange and Display Name text inside the Hyperlink
                AddImageToHyperLink(hyperlink, DefaultThumbnailSize.Medium.Height, DefaultThumbnailSize.Medium.Width, addTextRun: true);

                // Wrap the Hyperlink in a Paragraph to keep it isolated and editable
                var para = new Paragraph(new Run(" "));
                para.Inlines.Add(hyperlink);
                para.Inlines.Add(new Run(" ", tp));
                uxRichTextBox.Document.Blocks.Add(para);

                // RND: Experiments with programatic hyperlink navigation 
                hyperlink.RequestNavigate += (object sender, RequestNavigateEventArgs e) => {
                    Debug.WriteLine($"RequestNavigate for {sender} with {e}");
                };
            }
        }

        private void AddImageToHyperLink(Hyperlink hyperlink, double height, double width, bool addTextRun = false) {
            // Create a new image with given height and width
            var image = new Image {
                // ToolTip object is used for xaml Image Style for FilePathToThumbNailConverter to display image as thumbnail
                ToolTip = hyperlink.NavigateUri.LocalPath,
                // Tag object is used to scale factor for LayoutTransform of the thumbnail image @See NoteViewer.xaml
                Tag = 1.0d,
                Height = height,
                Width = width,
            };

            // Add an image and name text for the Hyperlink using Windows Shell thumbnail image & display name
            using ShellObject shellObject = ShellObject.FromParsingName(hyperlink.NavigateUri.LocalPath);
            if (addTextRun) {
                hyperlink.Inlines.Add(new Run($" {shellObject?.Name} "));
            }

            InlineUIContainer iluic = new InlineUIContainer(image);
            hyperlink.Inlines.InsertBefore(hyperlink.Inlines.LastInline, iluic);
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
                var error = $"Media FAILED for {me.Source} : ";
                var iuic = me.Parent as InlineUIContainer;
                iuic.ContentStart.Paragraph.Inlines.Add(new Run($"{error} {e.ErrorException.Message}"));
                //U.Exceptions.LogException(e.ErrorException, error);
            }
        }

        public void OnHyperlink_MouseDown(object sender, MouseButtonEventArgs e) {
            Debug.WriteLine($"OnHyperlink_MouseDown for {e.Source}");
            if (sender is Hyperlink hyperlink && e.MouseDevice.LeftButton == MouseButtonState.Pressed) {
                var fileInfo = new FileInfo(Uri.UnescapeDataString(hyperlink.NavigateUri.AbsolutePath));
                U.Shell.ShellOpen(fileInfo);
                e.Handled = true;
            }
        }

        public void OnHyperlink_MouseWheel(object sender, MouseWheelEventArgs e) {
            if (sender is Hyperlink hyperlink && Keyboard.Modifiers == ModifierKeys.Control) {

                // Each Hyperlink has an associated image stored in the Tag object property
                Image image = hyperlink.Tag as Image;
                // Each Image uses Tag object to scale factor for LayoutTransform of the thumbnail image @See NoteViewer.xaml
                image.Tag = e.Delta > 0 ? (double)image.Tag * 1.01 : (double)image.Tag * 0.99;
                // Scale the thumbnail image using the LayoutTransform until approach closer to the next size
                // When image size changes across the S/M/L/XL thumbnail boundary size, recreate image to new size
                double newWidth = image.Width * (double)image.Tag;
                double newHeight = image.Height * (double)image.Tag;
                var thumbnail = U.Shell.GetShellThumbnail(hyperlink.NavigateUri.LocalPath, newWidth);

                // Recreate image in order to trigger update of image when new size is wanted
                if (thumbnail.Width > image.Width || thumbnail.Width < image.Width) {
                    // Must create a copy of the collection for safe iteration when updating same collection
                    ArrayList inlines = new ArrayList(hyperlink.Inlines.Count);
                    foreach (var i in hyperlink.Inlines) { inlines.Add(i); }
                    // foreach (Inline inline in inlines) { hyperlink.Inlines.Remove(inline);}
                    foreach (var inline in hyperlink.Inlines) {
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

    // Convert from Image ToolTip string file path to a Thumbnail BitmapSource (@see Style TargetType="{x:Type Image} Source XAML")
    public class FilePathToThumbNailConverter : IValueConverter
    {
        object IValueConverter.Convert(object o, Type type, object parameter, CultureInfo culture) {
            if (o is Image image && image.ToolTip is string tooltip) {
                FileInfo fileInfo = new FileInfo(tooltip);
                try {
                    return U.Shell.GetShellThumbnail(fileInfo.FullName, image.Width * (double)image.Tag);
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
                return visibility == Visibility.Visible ? true : false;
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