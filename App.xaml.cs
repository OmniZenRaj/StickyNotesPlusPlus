using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using System.Reflection;

using U = Utilities;
using OmniZenNotes.Models;

namespace OmniZenNotes
{
    using S = Properties.Settings;

    public partial class App : Application
    {
        public static List<NoteViewer> NoteViewers = new List<NoteViewer>();
        protected override void OnStartup(StartupEventArgs e) {
            try {
                LoadSettings();

                Repository.LoadModel();

                foreach (Notebook notebook in Repository.NoteBooks) {
                    foreach (Note note in notebook.Notes) {
                        new NoteViewer(note).Show();
                    }
                }

            } catch (Exception ex) {
                U.Exceptions.LogException(ex, "App START Error");
            }

        }

        protected override void OnExit(ExitEventArgs e) {
            SaveSettings();
            foreach (Notebook notebook in Repository.NoteBooks) {
                foreach (Note note in notebook.Notes) {
                    note.Save();
                }
            }
        }

        protected override void OnSessionEnding(SessionEndingCancelEventArgs e) {
            SaveSettings();
        }

        private void LoadSettings() {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var assemblyName = Assembly.GetEntryAssembly().GetName().Name;
            var userName = Environment.UserName;

            foreach (string noteBookDBFilePath in S.Default?.NoteBooks) {
                string nbDBFilePath = noteBookDBFilePath;
                nbDBFilePath = nbDBFilePath.Replace("{LocalApplicationData}", localAppData);
                nbDBFilePath = nbDBFilePath.Replace("{RoamingApplicationData}", roamingAppData);
                nbDBFilePath = nbDBFilePath.Replace("{AssemblyName}", assemblyName);
                nbDBFilePath = nbDBFilePath.Replace("{UserName}", userName);

                // Create the notebook sqlite db file if it doesn't exist :
                if (!File.Exists(nbDBFilePath)) {
                    FileInfo asmFileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
                    var db = Path.Combine(asmFileInfo.Directory.FullName, "DB", "OmniZenNotes.sqlite3");
                    var dir = Path.GetDirectoryName(nbDBFilePath);
                    Directory.CreateDirectory(dir); // Create the settings directory first time
                    File.Copy(db, nbDBFilePath);    // Copy from product location
                }

                Repository.NoteBooks.Add(new Notebook(nbDBFilePath));
            }
        }

        private void SaveSettings() {
            S.Default.NoteBooks.Clear();
            foreach (Notebook nb in Repository.NoteBooks) {
                S.Default.NoteBooks.Add(nb.DbPathUri);
            }
        }
    }
    // Application Level Commands // NLS: 
    public static class AppCommands
    {
        public static RoutedUICommand RefreshCommand = new RoutedUICommand("Refresh", "Refresh", typeof(AppCommands));
        public static RoutedUICommand SpellCheckCommand = new RoutedUICommand("Spell Check", "SpellCheck", typeof(AppCommands));
        public static RoutedUICommand FormatBarCommand = new RoutedUICommand("Format Bar", "FormatBar", typeof(AppCommands));
        public static RoutedUICommand SelectFontCommand = new RoutedUICommand("Font...", "SelectNoteFont", typeof(AppCommands));
        public static RoutedUICommand ViewNoteReminderCommand = new RoutedUICommand("Reminder...", "ViewNoteReminder", typeof(AppCommands));
        public static RoutedUICommand ViewNoteSettingsCommand = new RoutedUICommand("Properties...", "ViewNoteSettings", typeof(AppCommands));
        public static RoutedUICommand HideCommand = new RoutedUICommand("Hide Note", "HideNote", typeof(AppCommands));        
        public static RoutedUICommand DeleteCommand = new RoutedUICommand("Delete Note", "DeleteNote", typeof(AppCommands));
        public static RoutedUICommand ExitApplicationCommand = new RoutedUICommand("Exit App", "ExitApplication", typeof(AppCommands));
    }
}
