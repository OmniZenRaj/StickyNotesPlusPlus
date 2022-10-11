global using System;
global using System.IO;

global using OmniZenNotes.Models;
global using U = Utilities;
global using S = OmniZenNotes.Properties.Settings;
global using G = Utilities.Graphics;
global using SH = Utilities.Shell;
global using EX = Utilities.Exceptions;

using System.Windows;
using System.Windows.Resources;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;

namespace OmniZenNotes;

public partial class App : Application
{
    internal static readonly List<NoteViewer> NoteViewers = new();
    internal static readonly List<Process> PlugIns = new();

    internal static Guid GUID;

    protected override void OnStartup(StartupEventArgs e) {

        try {
            LoadSettings();
            InitPlugIns();
            Repository.LoadModel();
            foreach (Notebook notebook in Repository.NoteBooks) {
                foreach (Note note in notebook.Notes) {
                    NoteViewer.Create(note);
                }
            }
            InitNotifyIconArea();
        } catch (Exception ex) {
            EX.LogException(ex, "App START Error");
        }

    }

    public void InitNotifyIconArea() {
        try {
            Assembly assembly = Assembly.GetEntryAssembly();
            System.Drawing.Icon appIcon = System.Drawing.Icon.ExtractAssociatedIcon(assembly.Location);
            bool rc = SH.AddNotifyIcon(MainWindow, GUID, appIcon, assembly.FullName);
            if (rc is true) { SH.ModifyNotifyIcon(MainWindow, GUID, appIcon, appIcon);}
        } catch (Exception ex) {
            EX.LogException(ex, "App InitNotifyIconArea Error");
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
                Uri resourcePath = new("DB/OmniZenNotes.sqlite3", UriKind.Relative);
                StreamResourceInfo sri = GetResourceStream(resourcePath);
                using FileStream fs = File.Create(nbDBFilePath);
                sri.Stream.CopyTo(fs);
                fs.Flush(true);
                fs.Close();
            }

            Repository.NoteBooks.Add(new(nbDBFilePath));
        }
        // The Application GUID is used currently only for Shell NotifyIcon API (@see Utilities.Shell.AddNotifyIcon)
        // The FileSettingsProvider we use creates a new user.config per Application path location
        // This works OK since NotifyIcon API requires unique GUID per App path location
        // We only need to create a new one if the current GUID settings is null,empty or non-parsible
        if( S.Default.GUID is not string gs || string.IsNullOrEmpty(gs) || ! Guid.TryParse(gs, out GUID)) {
            GUID = Guid.NewGuid();
        }
        S.Default.GUID = GUID.ToString();
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
