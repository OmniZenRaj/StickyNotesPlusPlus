using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Navigation;
using System.Windows.Threading;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Controls.Primitives;
using System.Globalization;

using Microsoft.WindowsAPICodePack.Shell;
using Xceed.Wpf.Toolkit.PropertyGrid;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.WindowsAPICodePack.Taskbar;

#pragma warning disable IDE1006 // Ignore name rule violation for XAML element objects starting with ux

namespace OmniZenNotes
{
    using OmniZenNotes.Models;
    using S = Properties.Settings;
    using U = Utilities;
    using Utilities;

    public partial class NoteViewer : Window
    {
        NoteViewModel VM { get; set; }

        static List<FontFamily> FontFamilies;
        static List<PropertyInfo> BackgroundColors;
        static bool IsExiting = false;
        DispatcherTimer Timer = new DispatcherTimer();

        public NoteViewer(Note note, Rect placement = new Rect()) {
            InitializeComponent();
            VM = new NoteViewModel(this, note);
            App.NoteViewers.Add(this);

            InitializeControls();
            InitializeCommands();

            LoadSettings();
            InitSignalR();
            
            if (!placement.IsEmpty && placement.Height != 0 && placement.Width != 0) {
                Top = placement.Top; Left = placement.Left;
                Width = placement.Width; Height = placement.Height;
            }

            Visibility = VM.Note.UXSettings.Visibility;
        }

        void InitializeControls() {
            uxRichTextBox.IsDocumentEnabled = true;

            uxShowNotesMenuItem.SubmenuOpened += OnShowNotes_SubmenuOpened;
            uxSelectBackgroundMenuItem.SubmenuOpened += OnSelectBackgroundColor_SubmenuOpened;
            uxSelectFontMenuItem.SubmenuOpened += OnSelectFont_SubmenuOpened;

            Title = VM.Note.Title;
            Image AppIcon = (Image)FindResource("AppIcon");
            Icon = AppIcon.Source;
            /*          RND: Use Tray Utils to show balloontip (cannot get to work due to COMObject required)   
                        CommonUtils cu = new CommonUtils((new System.Windows.Interop.WindowInteropHelper(this).Handle));
                        TrayUtils tu = cu.Tray;
                        tu.ShowBalloonTip(10000, "Tip Title", "Tip Text", System.Windows.Forms.ToolTipIcon.Info);
             */
        }

    #region Window Initalization
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
            AddCommandBinding(AppCommands.FormatBarCommand, OnToggleFormatBarCommand);
            InputBindings.Add(new KeyBinding(AppCommands.FormatBarCommand, new KeyGesture(Key.F4, ModifierKeys.None, "F4")));
            // Spellcheck Command
            AddCommandBinding(AppCommands.SpellCheckCommand, OnToggleSpellCheckCommand);
            InputBindings.Add(new KeyBinding(AppCommands.SpellCheckCommand, new KeyGesture(Key.F7, ModifierKeys.None, "F7")));

            // View Note Reminder Command
            AddCommandBinding(AppCommands.ViewNoteReminderCommand, OnViewNoteReminderCommand);
            InputBindings.Add(new KeyBinding(AppCommands.ViewNoteReminderCommand, new KeyGesture(Key.R, ModifierKeys.Alt, "Alt-R")));
            // View Note Settings Command
            AddCommandBinding(AppCommands.ViewNoteSettingsCommand, OnViewNoteSettingsCommand);
            InputBindings.Add(new KeyBinding(AppCommands.ViewNoteSettingsCommand, new KeyGesture(Key.S, ModifierKeys.Alt, "Alt-S")));
            // Toggle Pin On/Off Command
            AddCommandBinding(AppCommands.TogglePinCommand, OnTogglePinCommand);
            InputBindings.Add(new KeyBinding(AppCommands.TogglePinCommand, new KeyGesture(Key.P, ModifierKeys.Alt, "Alt-P")));

            // New Command
            AddCommandBinding(ApplicationCommands.New, OnNewCommand);
            // Refresh Command
            AddCommandBinding(NavigationCommands.Refresh, OnRefreshCommand);
            // Save Note
            AddCommandBinding(ApplicationCommands.Save, OnSaveCommand);
            // Hide Command
            AddCommandBinding(AppCommands.HideCommand, OnHideCommand);
            InputBindings.Add(new KeyBinding(AppCommands.HideCommand, new KeyGesture(Key.H, ModifierKeys.Alt, "Alt-H")));
            // Close Command
            AddCommandBinding(AppCommands.CloseCommand, OnCloseCommand);
            InputBindings.Add(new KeyBinding(AppCommands.CloseCommand, new KeyGesture(Key.F4, ModifierKeys.Alt, "Alt-F4")));
            // Delete Command
            AddCommandBinding(AppCommands.DeleteCommand, OnDeleteCommand);
            InputBindings.Add(new KeyBinding(AppCommands.DeleteCommand, new KeyGesture(Key.D, ModifierKeys.Alt, "Alt-D")));
            // Full Screen Toggle
            AddCommandBinding(NavigationCommands.Zoom, OnZoomCommand);
            InputBindings.Add(new KeyBinding(NavigationCommands.Zoom, new KeyGesture(Key.F11, ModifierKeys.None, "F11")));

            // Print / Print Preview Note TODO: Not working - might need to conver to FixedDocument to print
            // AddCommandBinding(ApplicationCommands.Print, OnPrintCommand);
            // AddCommandBinding(ApplicationCommands.PrintPreview , OnPrintPreviewCommand);

            // Config Application Command
            AddCommandBinding(AppCommands.ApplicationPrefsCommand, OnApplicationPrefsCommand);
            InputBindings.Add(new KeyBinding(AppCommands.ApplicationPrefsCommand, new KeyGesture(Key.F1, ModifierKeys.Alt, "Alt-F1")));

            // Exit Application Command
            AddCommandBinding(AppCommands.ExitApplicationCommand, OnExitApplicationCommand);
            InputBindings.Add(new KeyBinding(AppCommands.ExitApplicationCommand, new KeyGesture(Key.F4, ModifierKeys.Alt, "Alt-F4")));

            // Local Function to Simplify Default Command Binding
            void AddCommandBinding(ICommand command, ExecutedRoutedEventHandler handler, CanExecuteRoutedEventHandler enabler = null) {
                CommandBinding cb = new CommandBinding(command);
                cb.Executed += new ExecutedRoutedEventHandler(handler);
                cb.CanExecute += new CanExecuteRoutedEventHandler(enabler ??= (sender, e) => e.CanExecute = true);
                CommandBindings.Add(cb);
            }

        }

        void OnLoaded(object sender, EventArgs e) {
            DataContext = VM.Note;

            uxSettingsPropertyGrid.SelectedObject = VM.Note;
            uxReminderPropertyGrid.SelectedObject = VM.Note.Task;
            uxRichTextBox.Document = VM.Note.Document;
            UpdatePinTabUX();
            
            VM.Note.UXSettings.RestoreBounds = RestoreBounds;
        }


        void OnClosed(object sender, EventArgs e) {
            App.NoteViewers.Remove(this);
        }

        void OnClosing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (!IsExiting && App.NoteViewers.Count == 1 && VM.Note != null) {
                string title = $"{STR("strCloseLastNoteTitle")} {Assembly.GetExecutingAssembly().GetName().Name}";
                string msg = $"{STR("strCloseLastNoteConfirmPrompt")}";
                e.Cancel = !ConfirmUserAction(title, msg);
            }

            if (VM.Note != null) { Save(saveAsync: false); }
        }
    #endregion

    #region Command Processors
        void OnSaveCommand(object sender, RoutedEventArgs e) {
            Save(saveAsync: false);
        }

        void Save(bool saveAsync = true) {
            SaveSettings();
            if (VM.Note != null) {
                VM.Note.Save(saveAsync);
            }
        }

        void OnRefreshCommand(object sender, RoutedEventArgs e) {
        }

        void OnToggleSpellCheckCommand(object sender, RoutedEventArgs e) {
            uxRichTextBox.SpellCheck.IsEnabled = !uxRichTextBox.SpellCheck.IsEnabled;
            uxSpellCheckMenuItem.IsChecked = uxRichTextBox.SpellCheck.IsEnabled;
        }

        void OnToggleFormatBarCommand(object sender, RoutedEventArgs e) {
            RTBFB.IsEnabled = !RTBFB.IsEnabled;
            RTBFB.Visibility = RTBFB.IsEnabled ? Visibility.Visible : Visibility.Hidden;
            uxFormatBarMenuItem.IsChecked = RTBFB.IsEnabled;
        }

        void OnHideCommand(object sender, RoutedEventArgs e) {
            int visibleNotes = 0;
            foreach (var nv in App.NoteViewers) {
                if (nv.Visibility == Visibility.Visible) {
                    visibleNotes++;
                }

                if (visibleNotes > 1) break;    // More than 1 is only factor
            }

            if (visibleNotes == 1) {
                string title = $"{STR("strHideLastNoteTitle")} {Assembly.GetExecutingAssembly().GetName().Name}";
                string msg = $"{STR("strHideLastNoteConfirmPrompt")}";
                if (ConfirmUserAction(title, msg)) {
                    Close();
                }
            } else {
                Hide();
            }

            e.Handled = true;
        }

        void OnCloseCommand(object sender, RoutedEventArgs e) {
            Close();
            e.Handled = true;
        }

        void OnDeleteCommand(object sender, RoutedEventArgs e) {
            if (App.NoteViewers.Count == 1) {
                string title = $"{STR("strDeleteLastNoteTitle")} {Assembly.GetExecutingAssembly().GetName().Name}";
                string msg = $"{STR("strDeleteLastNoteConfirmPrompt")} ";

                if (ConfirmUserAction(title, msg)) {
                    ConfirmDeleteNote();
                    SaveSettings();
                }
            } else {
                ConfirmDeleteNote();
            }

            void ConfirmDeleteNote() {
                string title = $"{STR("strDeleteLastNoteTitle")} {Assembly.GetExecutingAssembly().GetName().Name}";
                string msg = $"{STR("strDeleteNoteConfirmPrompt")}";
                if (ConfirmUserAction(title, msg, MessageBoxButton.YesNoCancel, MessageBoxImage.Stop, MessageBoxResult.Cancel)) {
                    VM.Note.Delete();
                    VM.Note = null;
                    Close();
                    App.NoteViewers.Remove(this);
                }
            }

            e.Handled = true;
        }

        // TODO: Add User Option to suppress this message on the dialog box (@see MS Sticky Notes)
        static bool ConfirmUserAction(string title, string msg, MessageBoxButton button = MessageBoxButton.OKCancel, MessageBoxImage image = MessageBoxImage.Question, MessageBoxResult result = MessageBoxResult.Cancel) {
            MessageBoxResult mbr;
            mbr = MessageBox.Show($"{msg}", $"{title}", button, image, result);
            return mbr == MessageBoxResult.Yes || mbr == MessageBoxResult.OK;
        }

        string STR(string resourceKey) {
            if (TryFindResource(resourceKey) is Run run) {
                return run.Text;
            };
            return "*** NOT FOUND ***";
        }

        void OnExitApplicationCommand(object sender, RoutedEventArgs e) {
            IsExiting = true;
            SaveSettings();

            var nvs = new ArrayList(App.NoteViewers); // Clone to safely iterate
            foreach( NoteViewer nv in nvs) {
                nv.Close();
            }
        }

        void OnApplicationPrefsCommand(object sender, RoutedEventArgs e) {
            string title = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;

/*             string msg = $" Company: {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTrademarkAttribute>()?.Trademark} \n" +
                         $" Product: {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>()?.Product} \n" +
                         $" {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description} \n" +
                         $" {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright} \n" +
                         $" Version: {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version} \n"; 
*/
            string msg = $" Version: {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version} \n";

            MessageBox.Show(msg, title, MessageBoxButton.OK);
        }

        void OnSelectFontCommand(object sender, RoutedEventArgs e) {
            OnTextFormatButton_Click(sender, e);
        }

        void OnTogglePinCommand(object sender, RoutedEventArgs e) {
            Topmost = !Topmost;
            UpdatePinTabUX();
            uxSettingsPropertyGrid.Update();
        }

        void OnViewNoteReminderCommand(object sender, ExecutedRoutedEventArgs e) {
            // Event was triggered from Menu item or Accelerator (so we auto check the button)
            if (e.Source is RichTextBox || e.Source is NoteViewer || (e.Parameter is string s && s.Equals("MenuItem"))) { 
                uxViewNoteReminderMenuItem.IsChecked = !uxViewNoteReminderMenuItem.IsChecked;
                uxReminderButton.IsChecked = uxViewNoteReminderMenuItem.IsChecked;
            }
            ShowReminderPanel(uxReminderButton);
        }
        void OnViewNoteSettingsCommand(object sender, ExecutedRoutedEventArgs e) {
            // Event was triggered from Menu item or Accelerator (so we auto check the button)
            if (e.Source is RichTextBox || e.Source is NoteViewer || (e.Parameter is string s && s.Equals("MenuItem"))) {
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
                    if (nv.Visibility == Visibility.Visible) { OnHideCommand(sender, e); }
                }
            }
        }

        void OnShowPublicNotesCommand(object sender, RoutedEventArgs e) {
            foreach (var nv in App.NoteViewers) {
                if (nv.VM.Note.Security.Permissions != EntityPermissions.Private) {
                    if (nv.Visibility != Visibility.Visible) { nv.Show(); }
                } else {
                    if (nv.Visibility == Visibility.Visible) { OnHideCommand(sender, e); }
                }
            }
        }

        void uxRichTextBox_MouseWheel(object sender, MouseWheelEventArgs e) {
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
        
        // RND with SignalR Comms
        void uxRichTextBox_PreviewKeyUp(object sender, KeyEventArgs e) {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Enter) {               
                if (VM.Note.Task.Reminder.LongNotification == true) {
                    OnSendSignalR();
                }
            }
        }

        void OnZoomCommand(object sender, RoutedEventArgs e) {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        void OnToolBar_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                DragMove();
            }
        }

        void OnActivated(object sender, EventArgs e) {
            ToggleToolBar(Visibility.Visible);
            uxRichTextBox.Focus();
        }

        void OnDeactivated(object sender, EventArgs e) {
            ToggleToolBar(Visibility.Collapsed);
        }

        void OnWindow_MouseEnter(object sender, EventArgs e) {
            ToggleToolBar(Visibility.Visible);
        }

        void OnWindow_MouseLeave(object sender, EventArgs e) {
            if (!IsActive) {
                ToggleToolBar(Visibility.Collapsed);
            }
        }

        void ToggleToolBar(Visibility visibility) {
            uxToolBarStackPanel.Visibility = visibility == Visibility.Visible ? Visibility.Visible : Visibility.Hidden;
            uxNoteTitleLabel.Opacity = visibility == Visibility.Visible ? 1 : .50;
            uxCloseNoteButton.Visibility = visibility == Visibility.Visible ? Visibility.Visible : Visibility.Hidden;
            // uxToolBar.Visibility = visibility;
        }

        void OnButton_MouseEnter(object sender, EventArgs e) {
        }

        void OnButton_MouseLeave(object sender, EventArgs e) {
        }
        
    #endregion
        
    #region UX Control Event Handlers

        void OnTextFormatButton_Click(object sender, RoutedEventArgs e) {

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

        void SetFont(FontFamily fontFamily, double fontSize, Brush foreGround, FontStyle fontStyle, bool updateUXSettings = true) {
            if (foreGround is SolidColorBrush scb) {
                SetFont(fontFamily, fontSize, scb.Color, fontStyle, updateUXSettings);
            }
        }

        void SetFont(FontFamily fontFamily, double fontSize, Color fontColor, FontStyle fontStyle, bool updateUXSettings = true) {
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
                    uxRichTextBox.Foreground = new SolidColorBrush(fontColor);
                }
            }

            if (updateUXSettings) {
                VM.Note.UXSettings.FontFamily = uxRichTextBox.FontFamily;
                VM.Note.UXSettings.FontSize = uxRichTextBox.FontSize;
                VM.Note.UXSettings.FontColor = (uxRichTextBox.Foreground as SolidColorBrush).Color;
                VM.Note.UXSettings.FontStyle = uxRichTextBox.FontStyle;
            }
        }

        void OnFillBackgroundButton_Click(object sender, RoutedEventArgs e) {

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

        void SetBackgroundColor(Color color, bool updateUXSettings = true) {
            if (updateUXSettings) {
                VM.Note.UXSettings.BackgroundColor = color;
            };

            uxColorPicker.SelectedColor = color;
            Color colorScRgb = AdjustColor(color);

            if (uxRichTextBox.Document.Background is ImageBrush backgroundBrush) {
                backgroundBrush.Opacity = colorScRgb.A / 255.0f;
                uxRichTextBox.Background = null;
                Background = null;
            } else {
                Background = new SolidColorBrush(color);
                uxRichTextBox.Document.Background = new SolidColorBrush(color);
                if (colorScRgb.A == 0) { colorScRgb.A = 0x01; }
            }

            uxToolBar.Background = new SolidColorBrush(colorScRgb);
            uxRichTextBox.SelectionBrush = colorScRgb.ScA <= 0.75 ? new SolidColorBrush(Colors.Black) : new SolidColorBrush(colorScRgb);
        }

        static Color AdjustColor(Color color) {
            // Tweak the textbox color for a nice background accent color for the toolbar.
            U.Graphics.RgbToHls(color.R, color.G, color.B, out double h, out double l, out double s);
            if (l > 0.75f) l *= 0.70f; else if (l > 0.50f) l *= 0.45f; else if (l > 0.35f) l *= 0.25f; else if (l > 0.25f) l *= 1.50f; else if (l > 0.00f) l *= 1.75f; else l = 0.35f;
            if (l < 0.35f) l *= 1.75f; else if (l < 0.50f) l *= 1.50f;
            U.Graphics.HlsToRgb(h, l, s, out int r, out int g, out int b);
            float scA = color.A / 255.0f, scR = r / 255.0f, scG = g / 255.0f, scB = b / 255.0f;
            var colorScRgb = Color.FromScRgb(scA, scR, scG, scB);

            return colorScRgb;
        }

        void OpenNewWindow(Note note) {

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
                5. If Note is touching or across right side, new Note goes to left side + padW of next right screen
                6. If new Note would overlap another existing Note, place it below current Note

                double right = Left + Width;

                if (newLeft + Width > area.Right) {
                    if (right + padW <= area.Right) { newLeft = area.Right - Width - padW; } // 2a
                    if (right + padW >= area.Right) { newLeft = Left - Width - padW; } // 2b
                }

                if (right >= area.Right && IsScreenAdjacentToARightScreen(screen, allScreens)) {
                    newLeft = area.Right + padW; // 5
                }
            */

            // Just drop it down and to the right for now:
            double padW = area.Width * 0.01, padH = area.Height * 0.01;
            double newTop = Top + uxToolBar.ActualHeight + padH;
            double newLeft = Left + padW * 2;

            #pragma warning disable CA1806 // Never used - is OK due to weak ref
            new NoteViewer(note, new Rect(newLeft, newTop, Width, Height));
            #pragma warning restore CA1806            

            #pragma warning disable CS8321 // The function declared but never used
            static bool IsScreenAdjacentToARightScreen(System.Windows.Forms.Screen screen, System.Windows.Forms.Screen[] allScreens) {
                foreach (var s in allScreens) {
                    if (screen.WorkingArea.Right <= s.WorkingArea.Left) { return true; }
                }
                return false;
            }

            static bool IsScreenAdjacentToALeftScreen(System.Windows.Forms.Screen screen, System.Windows.Forms.Screen[] allScreens) {
                foreach (var s in allScreens) {
                    if (screen.WorkingArea.Left >= s.WorkingArea.Right) { return true; }
                }
                return false;
            }
#pragma warning restore CS8321

        }

        void OnNoteTitleLabel_MouseDoubleClick(object sender, RoutedEventArgs e) {
            if (WindowState == WindowState.Maximized) { WindowState = WindowState.Normal; }
            uxToolBar.LayoutTransform = uxToolBar.LayoutTransform == Transform.Identity ? new ScaleTransform(0.75, 0.75) : Transform.Identity;
        }

        void OnMouseDoubleClick(object sender, RoutedEventArgs e) {
            if (e.OriginalSource is DockPanel dp && dp == uxToolBar) {
                if (WindowState == WindowState.Maximized) { WindowState = WindowState.Normal; }
                uxToolBar.LayoutTransform = uxToolBar.LayoutTransform == Transform.Identity ? new ScaleTransform(0.75, 0.75) : Transform.Identity;
            }
            // Control-DoubleClick will auto copy selection into clipboard
            if (e.OriginalSource is Run run && Keyboard.Modifiers == ModifierKeys.Control) {
                System.Windows.Forms.Clipboard.SetText(run.Text);
            }
        }

        void OnAddNoteButton_Click(object sender, RoutedEventArgs e) {
            OnNewCommand(sender, e);
        }

        void OnNewCommand(object sender, RoutedEventArgs e) {
            OpenNewWindow(NoteViewModel.CreateNewNote(copy: VM.Note));
        }

        void OnDelNoteButton_Click(object sender, RoutedEventArgs e) {
            OnDeleteCommand(sender, e);
        }

        void OnOptionsExpander_Expanded(object sender, RoutedEventArgs e) {
            if (sender is Expander expander) {
                VM.Note.UXSettings.OptionsExpanded = expander.IsExpanded;
            }
        }

        void OnOptionsExpander_Collapsed(object sender, RoutedEventArgs e) {
            if (sender is Expander expander) {
                VM.Note.UXSettings.OptionsExpanded = expander.IsExpanded;
            }
        }

        void OnPinTabButton_Click(object sender, RoutedEventArgs e) {
            OnTogglePinCommand(sender, e);
        }

        void UpdatePinTabUX() {
            VM.Note.UXSettings.Topmost = Topmost;

            Image image = uxPinTab.Content as Image;
            /*             Image icon = Topmost ? (Image)FindResource("PinTabOn") : (Image)FindResource("PinTabOff");
                        image.Source = icon.Source; */

            image.RenderTransform = new RotateTransform(Topmost ? 0 : 90);
            image.RenderTransformOrigin = new Point(0.5, 0.5);
            uxTogglePinMenuItem.IsChecked = Topmost;
        }

        // Reminder and Settings PropertyGrid UX Management:
        void ShowReminderPanel(ToggleButton toggleButton) {
            uxReminderPanel.Visibility = toggleButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        void ShowSettingsPanel(ToggleButton toggleButton) {
            uxSettingsPanel.Visibility = toggleButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        void uxReminderPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (e.NewValue is bool visible) {
                uxViewNoteReminderMenuItem.IsChecked = visible;
                // Toggle Settings panel & button to be mutualy exclusive of the Reminder panel
                uxSettingsPanel.Visibility = visible ? Visibility.Collapsed : uxSettingsPanel.Visibility;
                uxSettingsButton.IsChecked = !visible && uxSettingsButton.IsChecked == true;
                uxViewNoteSettingsMenuItem.IsChecked = uxSettingsButton.IsChecked == true;
            }
        }

        void uxSettingsPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (e.NewValue is bool visible) {
                uxViewNoteSettingsMenuItem.IsChecked = visible;
                // Toggle Reminder panel & button to be mutualy exclusive of the Settings panel
                uxReminderPanel.Visibility = visible ? Visibility.Collapsed : uxReminderPanel.Visibility;
                uxReminderButton.IsChecked = !visible && uxReminderButton.IsChecked == true;
                uxViewNoteReminderMenuItem.IsChecked = uxReminderButton.IsChecked == true;
            }
        }

        // TODO: Print & PrintPreview NOT working (may only work with FixedDocument (not FlowDocument))
        void OnPrintCommand(object sender, RoutedEventArgs e) {
            var printDialog = new PrintDialog(); // Create a PrintDialog.

            // Show the dialog and print the document if successful
            if (printDialog.ShowDialog() == true) {
                printDialog.PrintDocument((((IDocumentPaginatorSource)uxRichTextBox.Document).DocumentPaginator), $"Printing ");
            }
        }
        // Dynamic Submenu Open Processing
        void OnSelectBackgroundColor_SubmenuOpened(object sender, RoutedEventArgs e) {
            uxSelectBackgroundMenuItem.Items.Clear();

            // Load the Colors if not already loaded
            if (BackgroundColors == null || BackgroundColors.Count == 0) {
                BackgroundColors = new List<PropertyInfo>(typeof(Colors).GetProperties());
            }

            // Add a Colors... Menu Item to bring up Colors Dialog box
            // TODO: Try to use the newer Wpf Toolkit ColorPicker
            MenuItem item = new MenuItem { Header = "Colors...", ToolTip = $"{STR("strSetBackgroundFromColorDialogTip")}"};
            item.Click += (object sender, RoutedEventArgs e) => {
                if (sender is MenuItem mi) { OnFillBackgroundButton_Click(sender, e); }
            };
            uxSelectBackgroundMenuItem.Items.Add(item);

            item = new MenuItem { Header = "Insert Image...", ToolTip = $"{STR("strSetBackgroundFromImageTip")}"};
            item.Click += (object sender, RoutedEventArgs e) => {
                // TODO: Add Open File Dialog to Select an Image to Insert
            };
            uxSelectBackgroundMenuItem.Items.Add(item);
            uxSelectBackgroundMenuItem.Items.Add(new Separator());

            foreach (PropertyInfo prop in BackgroundColors) {
                Color color = (Color)prop.GetValue(null);
                item = new MenuItem {
                    Header = prop.Name,
                    Tag = color,
                    Background = new SolidColorBrush(color),
                    Foreground = new SolidColorBrush(AdjustColor(color)),
                    ToolTip = $"{STR("strSetBackgroundToColorTip")} {prop.Name}",
            };

                // Handle color selection from auto generated submenu
                item.Click += (object sender, RoutedEventArgs e) => {
                    if (sender is MenuItem mi && mi.Tag is Color color) {
                        SetBackgroundColor(color);
                    }
                };

                uxSelectBackgroundMenuItem.Items.Add(item);
            }
        }

        void OnSelectFont_SubmenuOpened(object sender, RoutedEventArgs e) {
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

            uxShowNotesMenuItem.Items.Clear();

            uxShowNotesMenuItem.Items.Add(uxShowAllNotesMenuItem);
            uxShowNotesMenuItem.Items.Add(uxShowPrivateNotesMenuItem);
            uxShowNotesMenuItem.Items.Add(uxShowPublicNotesMenuItem);
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
                if (item.Background is SolidColorBrush scb) {
                    item.Foreground = new SolidColorBrush(AdjustColor(scb.Color));
                }

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
        void LoadSettings() {
            try {
                // Restore the Window State (minimized gets converted to be Normal to avoid user not seeing it)
                WindowState = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;

                // Use App Defaults for Note if not UX Settings exist
                if (VM.Note.UXSettings is null) {
                    // Restore Window position and size from user settings save of last session
                    if (S.Default?.RestoreBounds is Rect restoreBounds) {
                        Left = restoreBounds.Left; Top = restoreBounds.Top;
                        Width = restoreBounds.Width; Height = restoreBounds.Height;
                    }

                    SetFont(S.Default.Font, S.Default.FontSize, S.Default.FontColor, FontStyle, updateUXSettings: false);
                    uxColorPicker.SelectedColor = S.Default.BackgroundColor;
                    SetBackgroundColor(S.Default.BackgroundColor, updateUXSettings: false);
                    uxOptionsExpander.IsExpanded = S.Default.OptionsExpanded;
                    Topmost = S.Default.Topmost;
                }

                // Auto Save Settings
                if (S.Default.AutoSave is int seconds && seconds > 0) {
                    Timer = new DispatcherTimer();
                    Timer.Tick += new EventHandler((sender, e) => Save(saveAsync: true));
                    Timer.Interval = TimeSpan.FromSeconds(seconds);
                    Timer.Start();
                }
            } catch {
                Width = 320; Height = 320;
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            LoadUXSettings();
            if (uxRichTextBox.Document.Background is SolidColorBrush scba) {
                uxColorPicker.SelectedColor = scba.Color;
            }
        }

        // Restore the Note specific settings (which override the App level settings)
        void LoadUXSettings() {

            // Restore the Window State (minimized gets converted to be Normal to avoid user not seeing it)
            SetFont(VM.Note.UXSettings.FontFamily, VM.Note.UXSettings.FontSize, VM.Note.UXSettings.FontColor, FontStyle);
            uxColorPicker.SelectedColor = VM.Note.UXSettings.BackgroundColor;
            SetBackgroundColor(VM.Note.UXSettings.BackgroundColor);
            uxOptionsExpander.IsExpanded = VM.Note.UXSettings.OptionsExpanded;

            RTBFB.IsEnabled = !VM.Note.UXSettings.FormatBar;
            OnToggleFormatBarCommand(null, null); 
            uxRichTextBox.SpellCheck.IsEnabled = !VM.Note.UXSettings.SpellCheck;
            OnToggleSpellCheckCommand(null, null);
            Topmost = VM.Note.UXSettings.Topmost;
            UpdatePinTabUX();

            uxToolBar.LayoutTransform = new ScaleTransform(VM.Note.UXSettings.ToolBarScale, VM.Note.UXSettings.ToolBarScale);
            Visibility = VM.Note.UXSettings.Visibility;

            if (VM.Note.UXSettings.RestoreBounds is Rect rb && double.IsFinite(rb.Left) && double.IsFinite(rb.Top)) {
                Width = rb.Width; Height = rb.Height;
                if (rb.Top != 0 && rb.Left != 0) {
                    Top = rb.Top; Left = rb.Left; 
                } else
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            
            // Use current and saved monitor information to compare Screen bounds for changes
            // If screen no longer exists, (ie multi monitor configuration changed), place at CenterScreen
            if (U.Graphics.GetScreen(VM.Note.UXSettings.MonitorInfo) is System.Windows.Forms.Screen curScreen) {
                try {
                    var savedScreen = Json.GetObjectFromJson<dynamic>(VM.Note.UXSettings.MonitorInfo);
                    // If screen size bounds has changed, try to account for the new layout
                    // Bounds is a rectangle stored as X, Y, Width, Height string values
                    string boundsString = savedScreen.Bounds.Value as string;
                    System.Drawing.Rectangle savedBounds = (System.Drawing.Rectangle)new System.Drawing.RectangleConverter().ConvertFromString(boundsString);
                    if (curScreen.Bounds != savedBounds) {
                        Top += curScreen.Bounds.Top - savedBounds.Top;
                        Left += curScreen.Bounds.Left - savedBounds.Left;
                    }
                } catch {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
            } else {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        void SaveUXSettings() {
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
                if (uxRichTextBox.Document.Background is SolidColorBrush scba) {
                    VM.Note.UXSettings.BackgroundColor = scba.Color;
                }
                VM.Note.UXSettings.OptionsExpanded = uxOptionsExpander.IsExpanded;
                VM.Note.UXSettings.FormatBar = RTBFB.IsEnabled;
                VM.Note.UXSettings.SpellCheck = uxRichTextBox.SpellCheck.IsEnabled;
                VM.Note.UXSettings.Topmost = Topmost;
                if( uxToolBar.LayoutTransform is ScaleTransform st) {
                    VM.Note.UXSettings.ToolBarScale = st.ScaleX;
                }
                VM.Note.UXSettings.Visibility = Visibility;
                VM.Note.UXSettings.ZOrder = 1;
                VM.Note.UXSettings.MonitorInfo = U.Graphics.GetMonitorInfo(this);
            }
        }

        internal void SaveSettings() {

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

            if (uxRichTextBox.Document.Background is SolidColorBrush scb)
            {
                S.Default.BackgroundColor = scb.Color;
            }

            S.Default.OptionsExpanded = uxOptionsExpander.IsExpanded;
            S.Default.Topmost = Topmost;

            S.Default.Save();
        }

    #endregion
        
    #region SignalR
        // TODO: Setup proper Collaboration settings
        void InitSignalR() {
            if (VM.Note.Task.Reminder.LongNotification == true) {
                App.CollaborateHubConnection.On<string, string>("broadcastMessage", (user, message) => {
                    this.Dispatcher.Invoke(() => {
                        var newMessage = $"{user}: {message}";
                        FlowDocument doc = uxRichTextBox.Document;
                        TextRange tr = new TextRange(doc.ContentStart, doc.ContentEnd);
                        TextPointer tp = uxRichTextBox.CaretPosition;
                        var para = new Paragraph(new Run(newMessage));
                        doc.Blocks.Add(para);
                    });
                });
            }
        }
        
        async static void OnSendSignalR() {
            // RND Trying to get a icon to display message count
            /* TaskbarManager tm = TaskbarManager.Instance;
            tm.SetOverlayIcon(U.Shell.GetShellIcon(new FileInfo(@"C:\Chrome.ico")), "Icon Text"); 
            */

            DispatcherTimer dt = new DispatcherTimer();
            dt.Tick += new EventHandler((sender, e) => {
                TaskbarManager tm = TaskbarManager.Instance;
                tm.SetProgressValue(2, 10);
            });
            dt.Interval = TimeSpan.FromMilliseconds(100);
            dt.Start();

            if (App.CollaborateHubConnection.State == HubConnectionState.Connected) {
                await App.CollaborateHubConnection.InvokeAsync("Send", "NoteViewer", "TEST 123");
            } else {
                MessageBox.Show("Cannot Send. No Connection Exists to Server");
            }
        }


    #endregion
        
    #region UX_Handlers
        void uxRichTextBox_TextChanged(object sender, TextChangedEventArgs e) {
        }

        void uxColorPicker_SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) {
            SetBackgroundColor((Color)e.NewValue);
            uxSettingsPropertyGrid.Update();
        }

        void uxReminderPanel_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                uxReminderPanel.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        void uxSettingsPanel_KeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Escape) {
                uxSettingsPanel.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        void uxReminderPropertyGrid_PropertyValueChanged(object sender, PropertyValueChangedEventArgs e) {
            if (e.OriginalSource is PropertyItem item) {
                switch (item.PropertyName) {
                    case "LongNotification":
                        break;
                    default: break;
                }
            }
        }

        void uxSettingsPropertyGrid_PropertyValueChanged(object sender, PropertyValueChangedEventArgs e) {
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
                        UpdatePinTabUX();
                        break;
                    case "Title":
                        Title = (string)e.NewValue;
                        uxNoteTitleLabel.Content = Title;
                        break;

                    default: break;
                }
            }
        }

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

            if (args.Data.GetDataPresent(DataFormats.Text, true)) {
                TextPointer tp = uxRichTextBox.CaretPosition;
                try {
                    string uri = args.Data.GetData(DataFormats.Text, true) as string;
                    TextRange range = uxRichTextBox.Selection;
                    TextPointer tpStart = range.IsEmpty? tp : range.Start; 
                    TextPointer tpEnd = range.IsEmpty? tp : range.End;
                    CreateHyperLink(tpStart, tpEnd, new Uri(uri));
                } catch { }
            }

            // Handle File based Drag & Drop Operation:
            void DropFile(FileInfo fi, DragEventArgs args) {

                FlowDocument doc = uxRichTextBox.Document;
                TextRange tr = new TextRange(doc.ContentStart, doc.ContentEnd);
                TextPointer tp = uxRichTextBox.CaretPosition;

                // Insert the contents of supported dropped file:
                if (args.KeyStates == DragDropKeyStates.ControlKey) {
                    switch (fi.Extension.ToLower()) {
                        // Create an MediaElement and set the Source to the dropped file path
                        case ".png": case ".jpg": case ".jpeg": case ".gif": case ".bmp": case ".tiff": case ".ico":
                        case ".mp4": case ".mpg": case ".mp3": case ".wma": case ".wmv": case ".avi": case ".mkv":
                            var me = new MediaElement { Source = new Uri(fi.FullName), ToolTip = fi.FullName };
                            var iuic_me = new InlineUIContainer(me, tp);
                            break;
                        default: {
                                // Try to insert it into the Note as Text:
                                // Detect byte order marks at the beginning of the file to see if we have text
                                using StreamReader sr = new StreamReader(fi.FullName, true);
                                if (sr.CurrentEncoding.EncodingName.Contains("utf", StringComparison.InvariantCultureIgnoreCase)) {
                                    tp.InsertTextInRun(sr.ReadToEnd());     // TODO: Determine MAX size text file supported
                                    sr.Close();
                                    SetFont(VM.Note.UXSettings.FontFamily, VM.Note.UXSettings.FontSize, VM.Note.UXSettings.FontColor, FontStyle);
                                }
                                break;
                            }
                    }
                } else if (args.KeyStates == DragDropKeyStates.AltKey) {
                    // Set the Document background from dropped file:
                    switch (fi.Extension.ToLower()) {
                        case ".png": case ".jpg": case ".jpeg": case ".gif": case ".bmp": case ".tiff": case ".ico":
                            var image = CreateImage(fi);
                            var imageBrush = new ImageBrush(image.Source);
                            if (uxRichTextBox.Document.Background is SolidColorBrush scb && scb.Color.A < 255) {
                                imageBrush.Opacity = scb.Color.A / 255.0f;
                            }
                            uxRichTextBox.Document.Background = imageBrush;
                            break;
                            // RND: Make a MediaElement the Background
                    }
                } else {
                    try {
                        CreateHyperLink(tp, tp, new Uri(fi.FullName));
                    } catch (Exception ex) {
                        if (ex.HResult == -2146233079) {
                            // Add the new Hyperlink to the end of the current Hyperlink
                            tp = tp.GetNextInsertionPosition(LogicalDirection.Forward);
                            try {
                                CreateHyperLink(tp, tp, new Uri(fi.FullName));
                            } catch {
                                tp = tp.Paragraph != null ? tp.Paragraph.ElementEnd : tp.DocumentEnd ;
                                tp.InsertLineBreak();
                                CreateHyperLink(tp, tp, new Uri(fi.FullName));
                            }
                        }
                    }
                }
                // Automatically set the Note Title to the Dropped file's name:
                if (VM.Note.Title.Contains("New Note", StringComparison.InvariantCultureIgnoreCase)) {
                    VM.Note.Title = fi.Name; Title = fi.Name;
                    uxNoteTitleLabel.Content = fi.Name;
                    uxSettingsPropertyGrid.Update();
                }

            }
            // Create Hyperlink at given TextPointer position for given URI
            Hyperlink CreateHyperLink(TextPointer tpStart, TextPointer tpEnd, Uri uri) {
                // Create a Hyperlink to the dropped file/folder
                Hyperlink hyperlink = new Hyperlink(tpStart, tpEnd) { NavigateUri = uri, };
                if (tpStart.CompareTo(tpEnd) == 0) {
                    // Add an Image and Display Name text inside the Hyperlink
                    double height = hyperlink.NavigateUri.IsFile ? DefaultThumbnailSize.Medium.Height : DefaultIconSize.Large.Height;
                    double width =  hyperlink.NavigateUri.IsFile ? DefaultThumbnailSize.Medium.Width: DefaultIconSize.Large.Width;
                    AddImageToHyperLink(hyperlink, height, width, addText: true);
                }
                return hyperlink;
            }
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
            } catch(Exception ex) { U.Exceptions.LogException(ex); }
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
            if (addText) { hyperlink.Inlines.Add($"{ name} ");}
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
            if (sender is Image im ) {
                // Images copy the source uri into a temp file to prevent file locking
                // If the temp file is no longer available, try recreating with original uri
                if( im.Source == null && im.Tag is Uri uri) {
                    if (File.Exists(uri.AbsolutePath)) {
                        var i = CreateImage(new FileInfo(uri.AbsolutePath));
                        im.Source = i.Source;
                    } else {
                        // Inform the user that the image file no longer found
                        var error = $"{STR("strImageFailedMsg")} {uri.AbsolutePath}";
                        var iuic = im.Parent as InlineUIContainer;
                        if (iuic.ContentStart.Paragraph.Inlines.Count < 2 ) {
                            iuic.ContentStart.Paragraph.Inlines.Add(new Run($"{error}"));
                        }
                    }
                }
            }
        }
        public void OnHyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
            Debug.WriteLine($"OnHyperlink_RequestNavigate for {sender} with {e}");
            if (sender is Hyperlink hyperlink) {
                var uriPath = Uri.UnescapeDataString( hyperlink.NavigateUri.IsFile ?
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
                if (!hyperlink.NavigateUri.IsFile) {return;}

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
    }
    #endregion
    
    #region Converters
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
#endregion
}

