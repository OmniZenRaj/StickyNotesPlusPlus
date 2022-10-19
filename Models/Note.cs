using System.Data;
using System.Data.SQLite;
using System.Windows.Documents;
using System.Windows.Markup;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace OmniZenNotes.Models;

public class Note : Entity
{
    public string Title { get; set; }
    public FlowDocument Document { get; private set; }
    public Task Task { get; set; }

    public Note() : this(Guid.NewGuid()) {
    }

    public Note(string guid) : this(Guid.Parse(guid)) {

    }

    public Note(Guid guid) : base(guid) {
        Document = new FlowDocument(new Paragraph(new Run("")));
        Task = new();
    }

    public async void Save(bool saveAsync = true) {
        if (saveAsync) {
            await Repository.SaveNote(this);
            await Repository.SaveTask(Task);
        } else {
            Repository.SaveNote(this);
            Repository.SaveTask(Task);
        }
    }

    public void Delete() {
        Repository.DeleteTask(Task);
        Repository.DeleteNote(this);
    }

    public new void LoadFromSQL(SQLiteDataReader reader) {
        base.LoadFromSQL(reader);

        try {
            Title = GetString(reader, "Title");
            Document = (FlowDocument)XamlReader.Parse(GetString(reader, "Document"));
            Task = Repository.GetTask(GetGuid(reader, "TaskID"));
        } catch (Exception ex) {
            EX.LogException(ex, "Note LoadFromSQL FAILED:");
        }
    }

    public new void LoadToSQL(SQLiteCommand cmd) {
        base.LoadToSQL(cmd);

        try {
            cmd.Parameters.AddWithValue("@Title", Title);
            cmd.Parameters.Add("@Document", DbType.Xml).Value = XamlWriter.Save(Document);
            cmd.Parameters.AddWithValue("@TaskID", Task.ID.ToString());

        } catch (Exception ex) {
            EX.LogException(ex, $"Note LoadToSQL FAILED:");
        }
    }

#pragma warning disable IDE0051
    private void LoadDocumentBlob(SQLiteDataReader reader) {
        using MemoryStream ms = new();
        GetBlob(reader, reader.GetOrdinal("Document"), ms);
        TextRange range = new(Document.ContentStart, Document.ContentEnd);
        range.Load(ms, System.Windows.DataFormats.XamlPackage);
        ms.Close();
    }

    private void SaveDocumentBlob(SQLiteCommand cmd) {
        using MemoryStream ms = new();
        TextRange range = new(Document.ContentStart, Document.ContentEnd);
        range.Save(ms, System.Windows.DataFormats.XamlPackage, true);
        cmd.Parameters.Add("@Document", DbType.Binary).Value = ms.ToArray();
        ms.Close();
    }
#pragma warning restore IDE0051

}