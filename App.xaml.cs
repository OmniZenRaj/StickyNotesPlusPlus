using System;
using System.Windows;
using System.Collections.Generic;
using System.Reflection;

using OmniZenNotes.Models;

namespace OmniZenNotes
{
    using S = Properties.Settings;

    public partial class App : Application
    {
        public static List<NoteViewer> NoteViewers = new List<NoteViewer>();
        protected override void OnStartup(StartupEventArgs e) {

            LoadSettings();

            Repository.LoadModel();

            foreach (Notebook notebook in Repository.NoteBooks) {
                foreach(Note note in notebook.Notes) {
                    new NoteViewer(note).Show();
                }
            }
        }

        protected override void OnExit(ExitEventArgs e) {
            SaveSettings();
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
}
