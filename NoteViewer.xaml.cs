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

namespace OmniZenNotes;

public partial class NoteViewer : Window
{
    const string MS_XAML_SCHEME = @"http://schemas.microsoft.com/winfx/2006/xaml/";
    internal HubConnection CollaborateHubConnection;
        
    internal NoteViewModel VM { get; set; }
    static List<FontFamily> FontFamilies;
    static List<PropertyInfo> BackgroundColors;
    static bool IsExiting = false;

    private NoteViewer(Note note, Rect placement = new()) {
        InitializeComponent();
        VM = new(this, note);

        InitializeControls();
        InitializeCommands();

        LoadSettings();
        // SignalRClient.Init(this);

        if (!placement.IsEmpty && placement.Height != 0 && placement.Width != 0) {
            WindowStartupLocation = WindowStartupLocation.Manual;
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

        Title = VM.Note.Title ?? "New Note";
        if (FindResource("AppIcon") is Image i) { Icon = i.Source; }
    }

    public static void Create(Note note, Rect placement = new()) {
        App.NoteViewers.Add(new(note, placement));
    }   
    #region Window Initalization

    void OnLoaded(object sender, EventArgs e) {
        DataContext = VM.Note;

        uxSettingsPropertyGrid.SelectedObject = VM.Note;
        uxReminderPropertyGrid.SelectedObject = VM.Note.Task;
        uxRichTextBox.Document = VM.Note.Document;
        UpdatePinTabUX();

        VM.Note.UXSettings.RestoreBounds = RestoreBounds;
        if (uxRichTextBox.Document.Blocks.Count == 0) {
            uxRichTextBox.Document.Blocks.Add(new Paragraph(new Run("")));
            LoadUXSettings();
        }
    }


    void OnClosed(object sender, EventArgs e) {
        App.NoteViewers.Remove(this);
    }

    void OnClosing(object sender, System.ComponentModel.CancelEventArgs e) {
        if (!IsExiting && App.NoteViewers.Count == 1 && VM.Note != null) {
            SystemCommands.MinimizeWindow(this);
            e.Cancel = true;
            /*             string title = $"{SK("strCloseLastNoteTitle")} {Assembly.GetExecutingAssembly().GetName().Name}";
                        string msg = $"{SK("strCloseLastNoteConfirmPrompt")}";
                        e.Cancel = !ConfirmUserAction(title, msg);
             */
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

    void OnToolBar_MouseDown(object sender, MouseButtonEventArgs e) {
        if (e.LeftButton == MouseButtonState.Pressed) {
            DragMove();
        }
    }

    void OnActivated(object sender, EventArgs e) {
        ToggleToolBar(Visibility.Visible);
        uxRichTextBox.Focus();
        SystemCommands.RestoreWindow(this);
        SignalRClient.UpdateTaskBar(this, 0); // Cancel any notifications
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

        TextRange range = uxRichTextBox.Selection;
        if (string.IsNullOrEmpty(range.Text)) {
            range = new(uxRichTextBox.Document.ContentStart, uxRichTextBox.Document.ContentEnd);
        }

        // TODO: Create Utility routines to convert from System.Windows.Media Fonts to System.Drawing.Fonts etc.
        Trace.Write($"FontFamilyProperty={range.GetPropertyValue(FontFamilyProperty)} ");
        Trace.Write($"ForegroundProperty={range.GetPropertyValue(ForegroundProperty)} ");
        Trace.Write($"FontSizeProperty={range.GetPropertyValue(FontSizeProperty)} ");
        Trace.Write($"FontStyleProperty={range.GetPropertyValue(FontStyleProperty)} \n");
        Trace.Write($"FontWeightProperty={range.GetPropertyValue(FontWeightProperty)} \n");
        Trace.Write($"FontWeightProperty={range.GetPropertyValue(FontWeightProperty)} \n");
        // TODO: Add support for reading TextDecorations Underline & Strikethrough
        
        FontFamily fontFamilyProperty = (FontFamily)range.GetPropertyValue(FontFamilyProperty);
        var foreGroundProperty = range.GetPropertyValue(ForegroundProperty);
        var fontStyle = range.GetPropertyValue(FontStyleProperty);
        System.ComponentModel.TypeConverter cv = System.ComponentModel.TypeDescriptor.GetConverter(typeof(System.Drawing.Font));
        System.Drawing.Font font = (System.Drawing.Font)cv.ConvertFromInvariantString($"{fontFamilyProperty}, {range.GetPropertyValue(FontSizeProperty)}pt");
        Color c = (Color)ColorConverter.ConvertFromString(foreGroundProperty.ToString());
        System.Drawing.Color color = System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B);
        System.Drawing.FontStyle fs = fontStyle.ToString() switch {
            "Italic" => System.Drawing.FontStyle.Italic,
            "Oblique" => System.Drawing.FontStyle.Italic,            
            "Normal" => System.Drawing.FontStyle.Regular,
            _ => System.Drawing.FontStyle.Regular
        };

        if (range.GetPropertyValue(FontWeightProperty) is object fw && fw.ToString().Equals("Bold")) {
            fs |= System.Drawing.FontStyle.Bold;
        }
        
        Trace.Write($"FontDialog -> font={font} fontStyle={fs} c={c} color={color} \n");
        using var fd = new System.Windows.Forms.FontDialog {
            Font = new(font, fs), 
            Color = color, ShowColor = true,
            ShowEffects = true, ShowApply = true
        };

        var dr = fd.ShowDialog();
        if (dr == System.Windows.Forms.DialogResult.OK) {
            FontFamily fontFamily = new(fd.Font.FontFamily.ToString());
            Color fontColor = Color.FromArgb(fd.Color.A, fd.Color.R, fd.Color.G, fd.Color.B);
            float fontSize = fd.Font.Size;
            FontStyleConverter fsConverter = new ();
            FontStyle fdfs =  fd.Font.Style switch {
                System.Drawing.FontStyle.Italic => (FontStyle)fsConverter.ConvertFromString("Italic"),
                System.Drawing.FontStyle.Regular => (FontStyle)fsConverter.ConvertFromString("Normal"),
                // TODO: Add more Font support for FontWeight(Bold) and TextDecorators (Underline/Strikethrough)
                _ => (FontStyle)fsConverter.ConvertFromString("Normal"),
            };

            Trace.WriteLine($"SetFont( fontFamily={fontFamily}, fontSize={fontSize}, fontColor={fontColor}, fontStyle={fontStyle})");
            SetFont(fontFamily, fontSize, fontColor: fontColor, fdfs);
            uxSettingsPropertyGrid.Update();
        }
    }

    void SetFont(FontFamily fontFamily, double fontSize, Brush foreGround, FontStyle fontStyle, bool updateUXSettings = true) {
        if (foreGround is SolidColorBrush scb) {
            SetFont(fontFamily, fontSize, scb.Color, fontStyle, updateUXSettings);
        }
    }

    void SetFont(FontFamily fontFamily, double fontSize, Color fontColor, FontStyle fontStyle, bool updateUXSettings = true) {
        uxRichTextBox.SetFont(fontFamily, fontSize, fontColor, fontStyle, updateUXSettings);
    }
   
    void SetTopMost(bool topMost) {
        Topmost = topMost; 
        UpdatePinTabUX();        
    }
    void SetNoteTitle(string title) {
        Title = title;
        VM.Note.Title = title;
        uxNoteTitleLabel.Content = Title;
        uxSettingsPropertyGrid.Update();
    }
    // Set the Note Title from given file if NOT already set
    public void SetNoteTitle(FileInfo fi) {
        if (VM.Note.Title.Contains("New Note", StringComparison.InvariantCultureIgnoreCase)) {
            SetNoteTitle(fi.Name);
        }
    }    
    
    void OnFillBackgroundButton_Click(object sender, RoutedEventArgs e) {

        using System.Windows.Forms.ColorDialog cd = new();
        if (uxRichTextBox.Background is SolidColorBrush scb) {
            cd.Color = System.Drawing.Color.FromArgb(scb.Color.A, scb.Color.R, scb.Color.G, scb.Color.B);
        }

        var dr = cd.ShowDialog();
        if (dr == System.Windows.Forms.DialogResult.OK) {
            Color color = Color.FromArgb(cd.Color.A, cd.Color.R, cd.Color.G, cd.Color.B);
            SetBackgroundColor(color);
            uxSettingsPropertyGrid.Update();
            PropogateBackgroundColor(color);
        }
    }

    // Update Background and propogate to Children that inherit from this
    void PropogateBackgroundColor(Color color) {
        foreach (var nv in App.NoteViewers) {
            if (nv.VM.Note.InheritsFrom(VM.Note)) {
                nv.SetBackgroundColor(color);
                nv.uxSettingsPropertyGrid.Update();
            }
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

        // Create new NoteViewer at same size & slightly to left & lower than this one
        /* TODO: Use MS StickyNotes 3.7.71 model (with enhancements) for new Note placements. 
            1. New Notes are created to the right of existing note (with padW) (same top)
            2. If not enough space on the right to fit entire note, 
                a) If more than padW avail, align right side of new note to edge of screen
                b) If less than padW avail, create to left of new note with padW 
            3. If enough space on right, place new note padW from right side of other note
            4. TODO: Fill in rest of algorithm (when notes get placed on left )
            Enhancements:
            4. New Note goes slightly lower then current Note placement
            5. If new Note would overlap another existing Note, place it below current Note
            6. If not enough space on either side, then go lower and to the right
                If another note occupies the wanted space, then try to go to left side
            7. Use adjacent screens (MS only uses one screen for placement algorithm) when out of room
                a). If Note is touching or across right side, new Note goes to left side + padW of next right screen
            8. If note goes below bottom of screen, move up to match bottom of Note to bottom of Screen
        */

        NoteViewer overLappingNV = null;
        Rect newLocation = GetNewWindowPlacement(this, Rect.Empty);
        do {
            Rect prevLocation = newLocation;
            overLappingNV = App.GetOverlappingWindow(newLocation);
            if (overLappingNV != null) { newLocation = GetNewWindowPlacement(overLappingNV, newLocation);}
            if (prevLocation == newLocation) { break; } // BUG: Gaurd infinite loop if newLocation doesn't change
        } while (overLappingNV != null);
        
        Debug.WriteLine($"Window Placement @ Top:{newLocation.Top} Left:{newLocation.Left} based on Top:{Top} Left:{Left} ");
        Create(note, newLocation);

        static Rect GetNewWindowPlacement(NoteViewer nv, Rect placement) {
            var screen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(nv).Handle);
            var vs = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);

            // 1. MS Strategy to place note to the right of existing Note with padding
            double padW = vs.Width * 0.005, padH = vs.Height * 0.005;
            double newLeft = Math.Round(nv.Left + nv.Width + padW, 0); double newTop = nv.Top;
            double right = Math.Round(nv.Left + nv.Width, 0);

            if (newLeft + nv.Width > vs.Right) {
                Debug.WriteLine($"Window Placement @ Left:{newLeft} + Width:{nv.Width} > Right of VirtualScreen {vs.Right}");
                if (right + padW <= vs.Right) { newLeft = vs.Right - nv.Width - padW; }     // 2a. - align to right of vscreen
                if (right + padW >= vs.Right) { newLeft = nv.Left - nv.Width - padW; }      // 2b. - place left of note
            }
            
            // StickyNote++ Ehancments to MS Sticky Notes placement strategy algorithm
            if (S.Default.NewWindowPlacementStrategy == "MSStickyNotes+Enhancements") {
                newTop = nv.Top + nv.uxToolBar.ActualHeight + padH; // 4. - lower than current note
                if (nv.Top + nv.Height > vs.Bottom) {               // 8. - if new placement goes below screen,
                    newTop = vs.Bottom - nv.Height - padH;          // 8. - align Note bottom with bottom of screen
                }
            }
            // 5. Assure new Location is NOT the same as placment passed in (to prevent overlapping)
            Rect newLocation = new Rect(newLeft, newTop, nv.Width, nv.Height);
            newLocation.Inflate(1, 1); // Accounts for rounding            
            if (newLocation.Contains(placement)) { newLocation.Offset(-5, -5);}
            
            return newLocation;
        }
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
        bool inherit = Keyboard.Modifiers == ModifierKeys.Control;
        OpenNewWindow(NoteViewModel.CreateNewNote(copy: VM.Note, inherit: inherit));
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
        image.RenderTransformOrigin = new(0.5, 0.5);
        uxTogglePinMenuItem.IsChecked = Topmost;
    }

    // Dynamic Submenu Open Processing
    void OnSelectBackgroundColor_SubmenuOpened(object sender, RoutedEventArgs e) {
        uxSelectBackgroundMenuItem.Items.Clear();

        // Load the Colors if not already loaded
        if (BackgroundColors == null || BackgroundColors.Count == 0) {
            BackgroundColors = new(typeof(Colors).GetProperties());
        }

        // Add a Colors... Menu Item to bring up Colors Dialog box
        // TODO: Try to use the newer Wpf Toolkit ColorPicker
        MenuItem item = new() { Header = "Colors...", ToolTip = $"{SK("strSetBackgroundFromColorDialogTip")}" };
        item.Click += (object sender, RoutedEventArgs e) => {
            if (sender is MenuItem mi) { OnFillBackgroundButton_Click(sender, e); }
        };
        uxSelectBackgroundMenuItem.Items.Add(item);

        item = new() { Header = "Insert Image...", ToolTip = $"{SK("strSetBackgroundFromImageTip")}" };
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
                ToolTip = $"{SK("strSetBackgroundToColorTip")} {prop.Name}",
            };

            // Handle color selection from auto generated submenu
            item.Click += (object sender, RoutedEventArgs e) => {
                if (sender is MenuItem mi && mi.Tag is Color color) {
                    PropogateBackgroundColor(color);
                }
            };

            uxSelectBackgroundMenuItem.Items.Add(item);
        }
    }

    void OnSelectFont_SubmenuOpened(object sender, RoutedEventArgs e) {
        uxSelectFontMenuItem.Items.Clear();

        // Load the Font Families if not already loaded
        if (FontFamilies == null || FontFamilies.Count == 0) {
            FontFamilies = new(Fonts.SystemFontFamilies);
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
                    SetFont(fontFamily, uxRichTextBox.FontSize, uxRichTextBox.Foreground as SolidColorBrush, uxRichTextBox.FontStyle);
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
                if( noteViewer.uxRichTextBox.Foreground is SolidColorBrush fscb) {
                    item.Foreground = new SolidColorBrush(fscb.Color);
                }
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
    
    #region SignalR Client Methods
        
    public string AddCollaborationMessage(DateTime date, string id, string user, string message) {

        Paragraph paragraph = new();
        // If the message is a MS XAML Scheme (ie from another StickyNotes++ User)
        if (message.Contains(MS_XAML_SCHEME)) {
            paragraph = (Paragraph)XamlReader.Parse(message); // XAML content
        } else {
            paragraph.Inlines.Add(message); // Simple text (ie from Collaborate web page)
        }

        string text = AddAvatarImage(paragraph, user, date);
        // Wrap the paragraph in a UI TextBlock to keep it non editable and safe
        TextBlock textBlock = new TextBlock {
            Background = paragraph.Background ?? uxRichTextBox.Background ?? Background,
            FontFamily = paragraph.FontFamily,
            FontSize = paragraph.FontSize,
            FontStretch = paragraph.FontStretch,
            FontStyle = paragraph.FontStyle,
            FontWeight = paragraph.FontWeight,
            Foreground = paragraph.Foreground,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.WordEllipsis,
        };

        System.Collections.ArrayList inlines = new(paragraph.Inlines); // clone to safely manipulate
        foreach (Inline inline in inlines) {
            textBlock.Inlines.Add(inline);
        }

        Border textBlockBorder = new() {
            BorderBrush = textBlock.Foreground,
            BorderThickness = new Thickness(0.5f),
            CornerRadius = new CornerRadius(1.0f),
            Child = textBlock
        };
        
        BlockUIContainer buc = new (textBlockBorder);
        uxRichTextBox.Document.Blocks.Add(buc);
        uxRichTextBox.CaretPosition = buc.ContentEnd;
        uxRichTextBox.CaretPosition.InsertTextInRun("");
        uxRichTextBox.CaretPosition = uxRichTextBox.Document.ContentEnd;

        return text;
    }
    
    // Add an Avatar Image for given Paragraph with border, image & tool tip 
    static string AddAvatarImage(Paragraph paragraph, string user, DateTime date, bool doToolTip = true) {

        // Get the raw text (used for alerts and tool tip etc)
        var range = new TextRange(paragraph.ContentStart, paragraph.ContentEnd);
        var text = range.Text;

        // Create a Circular Viewbox to contain the Avatar Image 
        if (GetAvatarImage(user) is Image i) {
            double iconWidth = SystemParameters.IconWidth;
            double iconHeight = SystemParameters.IconHeight;
            double padding = SystemParameters.BorderWidth * 2;
            double radius = iconHeight / 2;
            Point center = new Point(radius + padding, radius + padding);

            Viewbox viewbox = new Viewbox {
            Clip = new EllipseGeometry(center, radius, radius), ClipToBounds = true,
                Width = iconWidth + (padding*2), Height = iconHeight + (padding* 2),
                Child = i,
        };

            if (doToolTip) {
                viewbox.ToolTip = $@"{user} @ {date.ToLocalTime().FriendlyText()} : {text}";
                viewbox.ToolTipOpening += (object sender, ToolTipEventArgs e) => {
                    if (sender is Border b) {
                        b.ToolTip = $@"{user} @ {date.ToLocalTime().FriendlyText()} : {text}";
                    }
                };
            }

            InlineUIContainer iuc = new(viewbox);
            paragraph.Inlines.InsertBefore(paragraph.Inlines.FirstInline, iuc);
        }
        return text;
    }

    // Get the User's Avatar Image from Collaboration Hub or from local Assets resources
    internal static Image GetAvatarImage(string user) {

        Image image = new();
        // 1st look for user specific avatar image in Collaborate area
        string userName = user.Replace('\\', '-'); // Replace invalid URI characters
        UriBuilder hubUri = new UriBuilder(SignalRClient.HubURL) { Path = $"/Collaborate/AvatarImage/{userName}"};
        HttpClient hc = new();
        var hr = hc.Send(new HttpRequestMessage(HttpMethod.Get, hubUri.Uri));
        if (hr.IsSuccessStatusCode) {
            image.Source = new BitmapImage(hubUri.Uri);
        } else {
            // 2nd look for for the avatar image in the local resources  (if above not found/accessable)
            Uri assetsUri = new(Properties.Settings.Default.Avatar_Uri, UriKind.Relative);
            image.Source = new BitmapImage(assetsUri);
        }

        return image;
    }
    
    #endregion

    #region Window Dialog Box Utilities

    // TODO: Add User Option to suppress this message on the dialog box (@see MS Sticky Notes)
    public static bool ConfirmUserAction(string title, string msg, MessageBoxButton button = MessageBoxButton.OKCancel, MessageBoxImage image = MessageBoxImage.Question, MessageBoxResult result = MessageBoxResult.Cancel) {
        MessageBoxResult mbr;
        mbr = MessageBox.Show($"{msg}", $"{title}", button, image, result);
        return mbr == MessageBoxResult.Yes || mbr == MessageBoxResult.OK;
    }

    public string SK(string resourceKey) {
        return this.STR(resourceKey);
    }
    #endregion
}