using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Collections;
using System.Reflection;

namespace OmniZenNotes
{
    // Application Level Commands // NLS: 
    public static class AppCommands
    {
        public static RoutedUICommand RefreshCommand = new ("Refresh", "Refresh", typeof(AppCommands));
        public static RoutedUICommand SpellCheckCommand = new ("Spell Check", "SpellCheck", typeof(AppCommands));
        public static RoutedUICommand FormatBarCommand = new ("Format Bar", "FormatBar", typeof(AppCommands));
        public static RoutedUICommand SelectFontCommand = new ("Font...", "SelectNoteFont", typeof(AppCommands));
        public static RoutedUICommand ViewNoteReminderCommand = new ("Reminder...", "ViewNoteReminder", typeof(AppCommands));
        public static RoutedUICommand ViewNoteSettingsCommand = new ("Properties...", "ViewNoteSettings", typeof(AppCommands));
        public static RoutedUICommand TogglePinCommand = new ("Toggle Pin On Off", "TogglePin", typeof(AppCommands));
        public static RoutedUICommand HideCommand = new ("Hide Note", "HideNote", typeof(AppCommands));
        public static RoutedUICommand CloseCommand = new ("Close", "Close", typeof(AppCommands));
        public static RoutedUICommand DeleteCommand = new ("Delete Note", "DeleteNote", typeof(AppCommands));
        public static RoutedUICommand ShowAllNotesCommand = new ("Show All", "ShowAllNotes", typeof(AppCommands));
        public static RoutedUICommand ShowPrivateNotesCommand = new ("Show Private", "ShowPrivateNote", typeof(AppCommands));
        public static RoutedUICommand ShowPublicNotesCommand = new ("Show Public", "ShowPublicNotes", typeof(AppCommands));
        public static RoutedUICommand ApplicationPrefsCommand = new ("Preferences", "ApplicationPrefs", typeof(AppCommands));
        public static RoutedUICommand ExitApplicationCommand = new ("Exit App", "ExitApplication", typeof(AppCommands));
    }
    
    public partial class NoteViewer : Window
    {
        // Create, configure and bind Application Commands
        void InitializeCommands() {

            // Show Notes Commands
            AddCommandBinding(AppCommands.ShowAllNotesCommand, OnShowAllNotesCommand);
            InputBindings.Add(new (AppCommands.ShowAllNotesCommand, new KeyGesture(Key.X, ModifierKeys.Alt, "Alt-X")));
            AddCommandBinding(AppCommands.ShowPrivateNotesCommand, OnShowPrivateNotesCommand);
            InputBindings.Add(new (AppCommands.ShowPrivateNotesCommand, new KeyGesture(Key.Y, ModifierKeys.Alt, "Alt-Y")));
            AddCommandBinding(AppCommands.ShowPublicNotesCommand, OnShowPublicNotesCommand);
            InputBindings.Add(new (AppCommands.ShowPublicNotesCommand, new KeyGesture(Key.Z, ModifierKeys.Alt, "Alt-X")));

            // Set Note Font Command
            AddCommandBinding(AppCommands.SelectFontCommand, OnSelectFontCommand);

            // Format Bar Command
            AddCommandBinding(AppCommands.FormatBarCommand, OnToggleFormatBarCommand);
            InputBindings.Add(new (AppCommands.FormatBarCommand, new KeyGesture(Key.F4, ModifierKeys.None, "F4")));
            // Spellcheck Command
            AddCommandBinding(AppCommands.SpellCheckCommand, OnToggleSpellCheckCommand);
            InputBindings.Add(new (AppCommands.SpellCheckCommand, new KeyGesture(Key.F7, ModifierKeys.None, "F7")));

            // View Note Reminder Command
            AddCommandBinding(AppCommands.ViewNoteReminderCommand, OnViewNoteReminderCommand);
            InputBindings.Add(new (AppCommands.ViewNoteReminderCommand, new KeyGesture(Key.R, ModifierKeys.Alt, "Alt-R")));
            // View Note Settings Command
            AddCommandBinding(AppCommands.ViewNoteSettingsCommand, OnViewNoteSettingsCommand);
            InputBindings.Add(new (AppCommands.ViewNoteSettingsCommand, new KeyGesture(Key.S, ModifierKeys.Alt, "Alt-S")));
            // Toggle Pin On/Off Command
            AddCommandBinding(AppCommands.TogglePinCommand, OnTogglePinCommand);
            InputBindings.Add(new (AppCommands.TogglePinCommand, new KeyGesture(Key.P, ModifierKeys.Alt, "Alt-P")));

            // New Command
            AddCommandBinding(ApplicationCommands.New, OnNewCommand);
            // Refresh Command
            AddCommandBinding(NavigationCommands.Refresh, OnRefreshCommand);
            // Save Note
            AddCommandBinding(ApplicationCommands.Save, OnSaveCommand);
            // Hide Command
            AddCommandBinding(AppCommands.HideCommand, OnHideCommand);
            InputBindings.Add(new (AppCommands.HideCommand, new KeyGesture(Key.H, ModifierKeys.Alt, "Alt-H")));
            // Close Command
            AddCommandBinding(AppCommands.CloseCommand, OnCloseCommand);
            InputBindings.Add(new (AppCommands.CloseCommand, new KeyGesture(Key.F4, ModifierKeys.Alt, "Alt-F4")));
            // Delete Command
            AddCommandBinding(AppCommands.DeleteCommand, OnDeleteCommand);
            InputBindings.Add(new (AppCommands.DeleteCommand, new KeyGesture(Key.D, ModifierKeys.Alt, "Alt-D")));
            // Full Screen Toggle
            AddCommandBinding(NavigationCommands.Zoom, OnZoomCommand);
            InputBindings.Add(new (NavigationCommands.Zoom, new KeyGesture(Key.F11, ModifierKeys.None, "F11")));

            // Print / Print Preview Note TODO: Not working - might need to conver to FixedDocument to print
            // AddCommandBinding(ApplicationCommands.Print, OnPrintCommand);
            // AddCommandBinding(ApplicationCommands.PrintPreview , OnPrintPreviewCommand);

            // Config Application Command
            AddCommandBinding(AppCommands.ApplicationPrefsCommand, OnApplicationPrefsCommand);
            InputBindings.Add(new (AppCommands.ApplicationPrefsCommand, new KeyGesture(Key.F1, ModifierKeys.Alt, "Alt-F1")));

            // Exit Application Command
            AddCommandBinding(AppCommands.ExitApplicationCommand, OnExitApplicationCommand);
            InputBindings.Add(new (AppCommands.ExitApplicationCommand, new KeyGesture(Key.F4, ModifierKeys.Alt, "Alt-F4")));

            // Local Function to Simplify Default Command Binding
            void AddCommandBinding(ICommand command, ExecutedRoutedEventHandler handler, CanExecuteRoutedEventHandler enabler = null) {
                CommandBinding cb = new (command);
                cb.Executed += new (handler);
                cb.CanExecute += new (enabler ??= (sender, e) => e.CanExecute = true);
                CommandBindings.Add(cb);
            }

        }

        void OnSaveCommand(object sender, RoutedEventArgs e) {
            Save(saveAsync: false);
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

        void OnExitApplicationCommand(object sender, RoutedEventArgs e) {
            IsExiting = true;
            SaveSettings();

            ArrayList nvs = new (App.NoteViewers); // Clone to safely iterate
            foreach( NoteViewer nv in nvs) {
                nv.Close();
            }
        }

        void OnApplicationPrefsCommand(object sender, RoutedEventArgs e) {

            /*          RND: Use Tray Utils to show balloontip (cannot get to work due to COMObject required)   
                        CommonUtils cu = new ((new System.Windows.Interop.WindowInteropHelper(this).Handle));
                        TrayUtils tu = cu.Tray;
                        tu.ShowBalloonTip(10000, "Tip Title", "Tip Text", System.Windows.Forms.ToolTipIcon.Info);
             */
            bool rc = Utilities.Shell.AddTaskBarIcon(this, 1, Utilities.Graphics.ExtractIcon(Utilities.Shell.ACCICONS_EXE, 1, Utilities.Graphics.IconSize.Small), "TEST TIP 1");
            if (rc == true) {
                Utilities.Shell.ModifyTaskBarIcon(this, 1, Utilities.Graphics.ExtractIcon(Utilities.Shell.ACCICONS_EXE, 1, Utilities.Graphics.IconSize.Small), "TEST TIP 2");
                Utilities.Shell.GetTaskBarIconLocation(this, 1);
                System.Console.WriteLine($"rc = {rc}");
            }

            /*             string msg = $" Company: {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTrademarkAttribute>()?.Trademark} \n" +
                                     $" Product: {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>()?.Product} \n" +
                                     $" {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description} \n" +
                                     $" {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright} \n" +
                                     $" Version: {Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version} \n"; 
            */
            string title = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
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

        void OnZoomCommand(object sender, RoutedEventArgs e) {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        

        // TODO: Print & PrintPreview NOT working (may only work with FixedDocument (not FlowDocument))
        void OnPrintCommand(object sender, RoutedEventArgs e) {
            PrintDialog printDialog = new (); // Create a PrintDialog.

            // Show the dialog and print the document if successful
            if (printDialog.ShowDialog() == true) {
                printDialog.PrintDocument((((IDocumentPaginatorSource)uxRichTextBox.Document).DocumentPaginator), $"Printing ");
            }
        }
    }
}

