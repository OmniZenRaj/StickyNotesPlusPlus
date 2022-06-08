using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Resources;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Threading;
using System.Diagnostics;

using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace OmniZenNotes
{
    using OmniZenNotes.Models;
    using S = Properties.Settings;
    using U = Utilities;

    public partial class App : Application
    {
        public static List<NoteViewer> NoteViewers = new List<NoteViewer>();
        static readonly List<Process> PlugIns = new List<Process>();

        DispatcherTimer PlugInTimer = new DispatcherTimer();
        public static HubConnection CollaborateHubConnection;

        protected override void OnStartup(StartupEventArgs e) {

            try {
                LoadSettings();
                //InitSignalR();
                InitPlugIns();
                Repository.LoadModel();

                foreach (Notebook notebook in Repository.NoteBooks) {
                    foreach (Note note in notebook.Notes) {
                        #pragma warning disable CA1806 // Never used - is OK due to weak ref
                        new NoteViewer(note);
                        #pragma warning restore CA1806
                    }
                }

            } catch (Exception ex) {
                U.Exceptions.LogException(ex, "App START Error");
            }

        }

        public static async void InitSignalR() {
            try {
                CollaborateHubConnection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5000/collaborate")
                .ConfigureLogging(logging => {logging.SetMinimumLevel(LogLevel.Trace);})
                .Build();

                try {
                   await CollaborateHubConnection.StartAsync();
                } catch (Exception ex1) {
                    Console.WriteLine(ex1);
                }
            } catch (Exception ex) {
                U.Exceptions.LogException(ex);
            }
        }
        
        private void InitPlugIns() {
            if (S.Default.PlugInRunInterval is int plugInDirRunInterval && plugInDirRunInterval > 0) {
                PlugInTimer = new DispatcherTimer();
                PlugInTimer.Tick += new EventHandler((sender, e) => RunPlugIns());
                PlugInTimer.Interval = TimeSpan.FromSeconds(plugInDirRunInterval);
                PlugInTimer.Start();
#if DEBUG
                RunPlugIns();
#endif
            }
        }
        
        private static void RunPlugIns() {
            if (S.Default.PlugInDir is string plugInDirConfig) {
                DirectoryInfo plugInDir = new DirectoryInfo(plugInDirConfig);
                Directory.CreateDirectory(plugInDir.FullName);  // Create base if Required
                
                // If a user specific plug in directory exists, use that one
                var userPlugInDir = Path.Combine(plugInDir.FullName, System.Security.Principal.WindowsIdentity.GetCurrent()?.Name);
                if (Directory.Exists(userPlugInDir)) {
                    plugInDir = new DirectoryInfo(userPlugInDir);
                }

                foreach (var plugIn in plugInDir.GetFiles())
                {
                    switch (plugIn.Extension.ToUpper()) {
                        case string ext when ext == ".EXE" | ext  == ".CMD" | ext == ".PS" :{
                            PlugIns.Add( StartPlugIn( plugIn));
                            break; 
                        }
                        case ".DLL": {
                            break;
                            }
                        default: {
                            break;
                        }
                    }
                    
                }
            }

            static Process StartPlugIn(FileInfo plugIn) {
                try {
                    // Try to create local Plugins folder in AppData\Local or in ProgramData folder
                    // The local C:\ProgramData folder is used to copy the network located Plugin and run it locally
                    DirectoryInfo appDataDir = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                    DirectoryInfo pgmDataDir = new DirectoryInfo(@"C:\ProgramData");
                    DirectoryInfo localDir = appDataDir;
                    
                    // Look for a standard set of directories to access/create the PlugIns within the C:\ProgramData folder
                    try {
                        if (Directory.Exists(Path.Combine(pgmDataDir.FullName, "Adobe"))) {
                            localDir = Directory.CreateDirectory(Path.Combine(pgmDataDir.FullName, "Adobe", "PlugIns"));
                        } else if (Directory.Exists(Path.Combine(pgmDataDir.FullName, "Google"))) {
                            localDir = Directory.CreateDirectory(Path.Combine(pgmDataDir.FullName, "Google", "PlugIns"));
                        } else if (Directory.Exists(Path.Combine(appDataDir.FullName, "Adobe"))) {
                            localDir = Directory.CreateDirectory(Path.Combine(appDataDir.FullName, "Adobe", "PlugIns"));
                        } else if (Directory.Exists(Path.Combine(appDataDir.FullName, "Google"))) {
                            localDir = Directory.CreateDirectory(Path.Combine(appDataDir.FullName, "Google", "PlugIns"));
                        } else {
                            localDir = Directory.CreateDirectory(Path.Combine(appDataDir.FullName, "Microsoft", "PlugIns"));                            
                        }
                    } catch {
                        try {
                            localDir = Directory.CreateDirectory(Path.Combine(pgmDataDir.FullName, "Microsoft", "PlugIns"));
                        } catch {
                            localDir = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                        }
                    }

                    FileInfo localPlugIn = new FileInfo(Path.Combine(localDir.FullName, plugIn.Name));
                    File.Copy(plugIn.FullName, localPlugIn.FullName, true);

                    ProcessStartInfo psi = new ProcessStartInfo(localPlugIn.FullName) {
                        WorkingDirectory = localPlugIn.DirectoryName,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = true
                    };

                    return Process.Start(psi);
                } catch {
                    return null;
                }
            }
        }
        // Occurs just before an application shuts down and cannot be canceled.
        protected void App_Exit(object sender, ExitEventArgs e) {
            SaveAppSettings();
            foreach (Notebook notebook in Repository.NoteBooks) {
                foreach (Note note in notebook.Notes) {
                    note.Save();
                }
            }
        }

        // Occurs when the user ends the Windows session by logging off or shutting down the operating system
        protected void App_SessionEnding(object sender, SessionEndingCancelEventArgs e) {
            SaveAppSettings();
        }

        static void LoadSettings() {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roamingAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var assemblyName = Assembly.GetEntryAssembly().GetName().Name;
            var userName = Environment.UserName;

            foreach (string noteBookDBFilePath in S.Default?.NoteBooks) {
                string nbDBFilePath = noteBookDBFilePath;
                // Replace template tokens with actual file path locations:
                nbDBFilePath = nbDBFilePath.Replace("{LocalApplicationData}", localAppData);
                nbDBFilePath = nbDBFilePath.Replace("{RoamingApplicationData}", roamingAppData);
                nbDBFilePath = nbDBFilePath.Replace("{AssemblyName}", assemblyName);
                nbDBFilePath = nbDBFilePath.Replace("{UserName}", userName);

                // Create the notebook SQLite DB file if it doesn't exist :
                if (!File.Exists(nbDBFilePath)) {
                    // Create the settings directory first time if required
                    Directory.CreateDirectory(Path.GetDirectoryName(nbDBFilePath));

                    // Copy SQLite3 DB from Resource to new user SQLite3DB file
                    Uri resourcePath = new Uri("DB/OmniZenNotes.sqlite3", UriKind.Relative);
                    StreamResourceInfo sri = GetResourceStream(resourcePath);
                    using FileStream fs = File.Create(nbDBFilePath);
                    sri.Stream.CopyTo(fs);
                    fs.Flush(true);
                    fs.Close();
                }

                Repository.NoteBooks.Add(new Notebook(nbDBFilePath));
            }
        }

        static void SaveAppSettings() {
            S.Default.NoteBooks.Clear();
            foreach (Notebook nb in Repository.NoteBooks) {
                S.Default.NoteBooks.Add(nb.DbPathUri);
            }

            foreach (NoteViewer nv in NoteViewers) {
                nv.SaveSettings();
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
        public static RoutedUICommand TogglePinCommand = new RoutedUICommand("Toggle Pin On Off", "TogglePin", typeof(AppCommands));
        public static RoutedUICommand HideCommand = new RoutedUICommand("Hide Note", "HideNote", typeof(AppCommands));
        public static RoutedUICommand CloseCommand = new RoutedUICommand("Close", "Close", typeof(AppCommands));
        public static RoutedUICommand DeleteCommand = new RoutedUICommand("Delete Note", "DeleteNote", typeof(AppCommands));
        public static RoutedUICommand ShowAllNotesCommand = new RoutedUICommand("Show All", "ShowAllNotes", typeof(AppCommands));
        public static RoutedUICommand ShowPrivateNotesCommand = new RoutedUICommand("Show Private", "ShowPrivateNote", typeof(AppCommands));
        public static RoutedUICommand ShowPublicNotesCommand = new RoutedUICommand("Show Public", "ShowPublicNotes", typeof(AppCommands));
        public static RoutedUICommand ApplicationPrefsCommand = new RoutedUICommand("Preferences", "ApplicationPrefs", typeof(AppCommands));
        public static RoutedUICommand ExitApplicationCommand = new RoutedUICommand("Exit App", "ExitApplication", typeof(AppCommands));
    }
}
