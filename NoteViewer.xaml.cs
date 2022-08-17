using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Threading;
using System.Windows.Interop;

namespace OmniZenNotes
{
    using OmniZenNotes.Models;
    using U = Utilities;

    public partial class NoteViewer : Window
    {
        NoteViewModel VM { get; set; }

        static List<FontFamily> FontFamilies;
        static List<PropertyInfo> BackgroundColors;
        static bool IsExiting = false;
        
        public NoteViewer(Note note, Rect placement = new Rect()) {
            InitializeComponent();
            VM = new NoteViewModel(this, note);
            App.NoteViewers.Add(this);

            InitializeControls();
            InitializeCommands();

            LoadSettings();
            SignalRClient.Init(this, VM.Note);

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
        }

        #region Window Initalization

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

        void Save(bool saveAsync = true) {
            SaveSettings();
            if (VM.Note != null) {
                VM.Note.Save(saveAsync);
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
                    SignalRClient.OnSendSignalR();
                }
            }
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

        // Dynamic Submenu Open Processing
        void OnSelectBackgroundColor_SubmenuOpened(object sender, RoutedEventArgs e) {
            uxSelectBackgroundMenuItem.Items.Clear();

            // Load the Colors if not already loaded
            if (BackgroundColors == null || BackgroundColors.Count == 0) {
                BackgroundColors = new List<PropertyInfo>(typeof(Colors).GetProperties());
            }

            // Add a Colors... Menu Item to bring up Colors Dialog box
            // TODO: Try to use the newer Wpf Toolkit ColorPicker
            MenuItem item = new MenuItem { Header = "Colors...", ToolTip = $"{STR("strSetBackgroundFromColorDialogTip")}" };
            item.Click += (object sender, RoutedEventArgs e) => {
                if (sender is MenuItem mi) { OnFillBackgroundButton_Click(sender, e); }
            };
            uxSelectBackgroundMenuItem.Items.Add(item);

            item = new MenuItem { Header = "Insert Image...", ToolTip = $"{STR("strSetBackgroundFromImageTip")}" };
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

        
        #region Window Dialog Box Utilities

        // TODO: Add User Option to suppress this message on the dialog box (@see MS Sticky Notes)
        public static bool ConfirmUserAction(string title, string msg, MessageBoxButton button = MessageBoxButton.OKCancel, MessageBoxImage image = MessageBoxImage.Question, MessageBoxResult result = MessageBoxResult.Cancel) {
            MessageBoxResult mbr;
            mbr = MessageBox.Show($"{msg}", $"{title}", button, image, result);
            return mbr == MessageBoxResult.Yes || mbr == MessageBoxResult.OK;
        }

        public string STR(string resourceKey) {
            if (TryFindResource(resourceKey) is System.Windows.Documents.Run run) {
                return run.Text;
            };
            return "*** NOT FOUND ***";
        }
        #endregion
    }
}