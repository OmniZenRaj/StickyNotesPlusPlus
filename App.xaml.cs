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
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace OmniZenNotes;

public partial class App : Application
{
    public static List<NoteViewer> NoteViewers = new();
    static readonly List<Process> PlugIns = new();

    public static HubConnection CollaborateHubConnection;

    protected override void OnStartup(StartupEventArgs e) {

        try {
            LoadSettings();
            //InitSignalR();
            InitPlugIns();
            Repository.LoadModel();

            foreach (Notebook notebook in Repository.NoteBooks) {
                foreach (Note note in notebook.Notes) {
                    NoteViewer.Create(note);
                }
            }

        } catch (Exception ex) {
            EX.LogException(ex, "App START Error");
        }

    }

    public static async void InitSignalR() {
        try {
            CollaborateHubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5000/collaborate")
            .ConfigureLogging(logging => { logging.SetMinimumLevel(LogLevel.Trace); })
            .Build();

            try {
                await CollaborateHubConnection.StartAsync();
            } catch (Exception ex1) {
                Console.WriteLine(ex1);
            }
        } catch (Exception ex) {
            EX.LogException(ex);
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
