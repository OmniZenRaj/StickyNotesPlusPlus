namespace OmniZenNotes;

public class NoteViewModel
{
    internal NoteViewer NoteViewer;
    public Note Note { get; set; }
    public string ToolTip => $@"{Note.Security.CreatedBy}";

    public U.SystemIconResources Icons { get; set; } = new();

    public NoteViewModel(NoteViewer noteViewer, Note note) {
        NoteViewer = noteViewer;  // Wire up the View to this ViewModel
        Note = note;
    }
    
    public static Note CreateNewNote(Note copy) {
        return Repository.CreateNote(copy);
    }
}