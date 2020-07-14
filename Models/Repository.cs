using System;
using System.IO;
using System.Collections.Generic;

using System.Data;
using System.Data.SQLite;

using EX = Utilities.Exceptions;

namespace OmniZenNotes.Models
{
    internal class Repository
    {
        private static SQLiteConnection _Connection;
        public static List<Notebook> NoteBooks { get; set; } = new List<Notebook>();
        public static List<Note> Notes { get; set; } = new List<Note>();
        public static List<Task> Tasks { get; set; } = new List<Task>();

        public static Notebook DefaultNotebook { get; set; }

        public static void LoadModel() {
            DefaultNotebook = NoteBooks[0];
            LoadNotes(DefaultNotebook);

            // If zero Notes, create a new one to start things off
            if (Notes.Count == 0) { CreateNote(); }
        }

        public static void LoadNotes(Notebook notebook) {

            try {
                SQLiteConnection conn = GetDBConnection();

                string query = $@"SELECT * FROM Note";
                using SQLiteCommand cmd = new SQLiteCommand(query, conn);
                using SQLiteDataReader reader = cmd.ExecuteReader();

                while (reader.Read()) {
                    var note = new Note();
                    note.LoadFromSQL(reader);
                    notebook.Notes.Add(note);   // Notes belonging to notebook
                    Notes.Add(note);            // App wide Notes collection
                }

            } catch (Exception ex) {
                EX.LogException(ex, $"SQLITE ERROR: ");
            }
        }

        public async static System.Threading.Tasks.Task<int> SaveNote(Note note) {

            try {
                SQLiteConnection conn = GetDBConnection();

                string query = $@"INSERT INTO Note
                    (ID, Name, Description , Title, Document, TaskID, UXSettings, OwnerSID, Permissions, CreatedBy, UpdatedBy )
                    VALUES(@ID, @Name, @Description, @Title, @Document, @TaskID, @UXSettings, @OwnerSID, @Permissions, @CreatedBy, @UpdatedBy)
                    ON CONFLICT(ID) DO UPDATE SET
                        Name=excluded.Name,
                        Description=excluded.Description,
                        Title=excluded.Title,
                        Document=excluded.Document,
                        TaskID=excluded.TaskID,
                        UXSettings=excluded.UXSettings,
                        OwnerSID=excluded.OwnerSID,
                        Permissions=excluded.Permissions,
                        CreatedBy=excluded.CreatedBy,
                        UpdatedBy=excluded.UpdatedBy";

                using SQLiteCommand cmd = new SQLiteCommand(query, conn);
                note.LoadToSQL(cmd);
                return await cmd.ExecuteNonQueryAsync();
            } catch (Exception ex) {
                EX.LogException(ex, $"SQLITE ERROR: ");
            }

            return 0;
        }

        public static Note GetNote(Guid GUID) {

            var note = new Note(GUID);

            try {
                SQLiteConnection conn = GetDBConnection();

                string query = $@"SELECT * FROM Note WHERE ID = '{GUID.ToString()}'";
                using SQLiteCommand cmd = new SQLiteCommand(query, conn);
                using SQLiteDataReader reader = cmd.ExecuteReader();

                if (reader.Read()) {
                    note.LoadFromSQL(reader);
                }

            } catch (Exception ex) {
                EX.LogException(ex, $"SQLITE ERROR: ");
            }

            return note;
        }

        public static void DeleteNote(Note note, Notebook notebook = null) {
            notebook ??= DefaultNotebook;
            try {
                SQLiteConnection conn = GetDBConnection();

                string query = $"DELETE FROM Note WHERE ID == '{note.ID.ToString()}'";
                using SQLiteCommand cmd = new SQLiteCommand(query, conn);
                int result = cmd.ExecuteNonQuery();

            } catch (Exception ex) {
                EX.LogException(ex, $"SQLITE ERROR: ");
            } finally {
                notebook.Notes.Remove(note);   // Remove from owner notebook
                Notes.Remove(note);            // Remove from App wide Notes collection
            }
        }

        public static Note CreateNote(Note copy = null, Notebook notebook = null) {

            notebook ??= DefaultNotebook;

            var note = new Note() { Title = $"New Note {Notes.Count+1}", };

            if (copy != null) {
                note.UXSettings.CloneFrom(copy.UXSettings);
                note.Document.Background = copy.Document.Background;
            }

            notebook.Notes.Add(note);           // Add to owner notebook
            Notes.Add(note);                    // Add to App wide Notes collection
            Tasks.Add(note.Task);               // Create associated Task

            return note;
        }

        private static SQLiteConnection GetDBConnection(Notebook notebook = null) {

            if( _Connection?.State == ConnectionState.Open) { return _Connection; }

            try {
                // Verify the Uri db Path location is valid and reachable:
                notebook ??= DefaultNotebook;
                FileInfo dbPath = new FileInfo(notebook.DbPathUri);
                if (!Directory.Exists(dbPath.DirectoryName)) {
                    throw new DirectoryNotFoundException(dbPath.FullName);
                }

                SQLiteConnectionStringBuilder connectString = new SQLiteConnectionStringBuilder();
                connectString.DataSource = dbPath.FullName;

                _Connection = new SQLiteConnection(connectString.ToString());
                _Connection.Open();
                return _Connection;

            } catch (Exception ex) {
                EX.LogException(ex, $"SQLITE ERROR: ");
            }

            return null;
        }

        public static void LoadTasks(Notebook notebook) {

            try {
                SQLiteConnection conn = GetDBConnection();

                string query = $@"SELECT * FROM Task";
                using SQLiteCommand cmd = new SQLiteCommand(query, conn);
                using SQLiteDataReader reader = cmd.ExecuteReader();

                while (reader.Read()) {
                    var task = new Task();
                    task.LoadFromSQL(reader);
                    Tasks.Add(task);            // App wide Notes collection
                }

            } catch (Exception ex) {
                EX.LogException(ex, $"SQLITE ERROR: ");
            }
        }

        public async static System.Threading.Tasks.Task<int> SaveTask(Task task) {

            try {
                SQLiteConnection conn = GetDBConnection();

                string query = $@"INSERT INTO Task
                    (ID, Name, Description, Subject, Status, Priority, StartDTS, DueDTS, CompletedDTS, TotalWork, ActualWork, Reminder, UXSettings, OwnerSID, CreatedBy, UpdatedBy )
                    VALUES(@ID, @Name, @Description, @Subject, @Status, @Priority, @StartDTS, @DueDTS, @CompletedDTS, @TotalWork, @ActualWork, @Reminder, @UXSettings, @OwnerSID, @CreatedBy, @UpdatedBy)
                    ON CONFLICT(ID) DO UPDATE SET
                        Name=excluded.Name,
                        Description=excluded.Description,
                        Subject=excluded.Subject,
                        Status=excluded.Status,
                        Priority=excluded.Priority,
                        StartDTS=excluded.StartDTS,
                        DueDTS=excluded.DueDTS,
                        CompletedDTS=excluded.CompletedDTS,
                        TotalWork=excluded.TotalWork,
                        ActualWork=excluded.ActualWork,
                        Reminder=excluded.Reminder,
                        UXSettings=excluded.UXSettings,
                        OwnerSID=excluded.OwnerSID,
                        CreatedBy=excluded.CreatedBy,
                        UpdatedBy=excluded.UpdatedBy";

                using SQLiteCommand cmd = new SQLiteCommand(query, conn);
                task.LoadToSQL(cmd);
                return await cmd.ExecuteNonQueryAsync();
            } catch (Exception ex) {
                EX.LogException(ex, $"SQLITE ERROR: ");
            }

            return 0;
        }

        public static Task GetTask(Guid GUID) {
            var task = new Task(GUID);
            try {
                SQLiteConnection conn = GetDBConnection();
                string query = $@"SELECT * FROM Task WHERE ID = '{GUID.ToString()}'";
                using SQLiteCommand cmd = new SQLiteCommand(query, conn);
                using SQLiteDataReader reader = cmd.ExecuteReader();
                if (reader.Read()) {
                    task.LoadFromSQL(reader);
                }
            } catch (Exception ex) {
                EX.LogException(ex, $"SQLITE ERROR: ");
            }
            return task;
        }

        public static void DeleteTask(Task task)
        {
            try
            {
                SQLiteConnection conn = GetDBConnection();

                string query = $"DELETE FROM Task WHERE ID = '{task.ID.ToString()}'";
                using SQLiteCommand cmd = new SQLiteCommand(query, conn);
                int result = cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                EX.LogException(ex, $"SQLITE ERROR: ");
            }
            finally
            {
                Tasks.Remove(task);            // App wide Notes collection
            }
        }
    }
}