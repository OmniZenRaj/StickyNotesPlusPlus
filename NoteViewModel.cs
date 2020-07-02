using System;
using OmniZenNotes.Models;
using Utilities;

namespace OmniZenNotes
{
    public class NoteViewModel
    {

        internal NoteViewer NoteViewer;
        public Note Note { get; set; }

        public SystemIconResources Icons { get; set; } = new SystemIconResources();

        public NoteViewModel(NoteViewer noteViewer, Note note) {
            NoteViewer = noteViewer;  // Wire up the View to this ViewModel
            Note = note;
        }

        public Note CreateNewNote(Note copy) {
            return Repository.CreateNote(copy);
        }
    }
}
